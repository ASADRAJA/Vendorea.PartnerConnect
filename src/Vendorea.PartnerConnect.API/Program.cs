using Microsoft.ApplicationInsights.Extensibility;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Vendorea.PartnerConnect.Api.Authentication;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Api.RateLimiting;
using Vendorea.PartnerConnect.Billing;
using Vendorea.PartnerConnect.Infrastructure.DependencyInjection;
using Vendorea.PartnerConnect.Infrastructure.Middleware;
using Vendorea.PartnerConnect.Merchant360Connector;
using Vendorea.PartnerConnect.PartnerAdapters;
using Vendorea.PartnerConnect.Persistence;
using Vendorea.PartnerConnect.Storage;
using Vendorea.PartnerConnect.Transport;
using Vendorea.PartnerConnect.Webhooks;
using Vendorea.PartnerConnect.WorkerProcesses;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for long-running import operations
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(60);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(60);
});

// Configure Serilog. When an Application Insights connection string is present (Azure App Service
// sets APPLICATIONINSIGHTS_CONNECTION_STRING), also ship logs there so they're queryable — the
// console-only sink does not reliably surface in the App Service log stream.
var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration);

var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
    telemetryConfiguration.ConnectionString = appInsightsConnectionString;
    loggerConfiguration = loggerConfiguration.WriteTo.ApplicationInsights(
        telemetryConfiguration, TelemetryConverter.Traces);
}

Log.Logger = loggerConfiguration.CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

// Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Vendorea PartnerConnect API",
        Version = "v1",
        Description = "API for managing trading partner integrations and data synchronization with Merchant360"
    });

    // Declare API-key auth so Swagger UI and generated clients prompt for the X-API-Key header.
    var apiKeyScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationHandler.ApiKeyHeaderName,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "API key. Send your key in the X-API-Key header.",
        Reference = new Microsoft.OpenApi.Models.OpenApiReference
        {
            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
            Id = "ApiKey"
        }
    };
    options.AddSecurityDefinition("ApiKey", apiKeyScheme);
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [apiKeyScheme] = Array.Empty<string>()
    });
});

// PartnerConnect services
builder.Services.AddPartnerConnectServices();
builder.Services.AddPartnerConnectInfrastructure();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddPartnerConnectPersistence(connectionString);

// Storage and Transport
builder.Services.AddDocumentStorage(builder.Configuration);
builder.Services.AddTransport();

builder.Services.AddPartnerAdapters();

// SPR inbound-simulation toggles (default OFF; see SprSimulationOptions).
builder.Services.Configure<Vendorea.PartnerConnect.Application.Services.SprSimulationOptions>(
    builder.Configuration.GetSection(Vendorea.PartnerConnect.Application.Services.SprSimulationOptions.SectionName));

// Merchant360 connector with API key or OAuth2 authentication
builder.Services.AddMerchant360Connector(options =>
{
    var section = builder.Configuration.GetSection("Merchant360");
    options.BaseUrl = section.GetValue<string>("BaseUrl") ?? "http://localhost:5003";
    options.ApiKey = section.GetValue<string>("ApiKey");
    options.TokenEndpoint = section.GetValue<string>("TokenEndpoint") ?? "http://localhost:5003/oauth2/token";
    options.ClientId = section.GetValue<string>("ClientId") ?? "";
    options.ClientSecret = section.GetValue<string>("ClientSecret") ?? "";
});

// Webhooks
builder.Services.AddWebhooks();

// Billing
builder.Services.AddBilling(builder.Configuration);

// Worker Processes (for FTP ingestion services, without background worker)
builder.Services.AddWorkerProcesses(builder.Configuration);

// JWT settings for the API's own tokens (customer-portal user tokens). The signing key is shared
// between issuance (OrgUserTokenService) and validation (the JwtBearer scheme below).
builder.Services.Configure<Vendorea.PartnerConnect.Api.Authentication.JwtSettings>(
    builder.Configuration.GetSection(Vendorea.PartnerConnect.Api.Authentication.JwtSettings.SectionName));
builder.Services.AddScoped<Vendorea.PartnerConnect.Api.Authentication.IOrgUserTokenService,
    Vendorea.PartnerConnect.Api.Authentication.OrgUserTokenService>();

// Shared org-onboarding path (activate + subscribe + invite first OrgAdmin). Used by both the
// registration-approval flow and the operator-led onboarding endpoint. Lives in the API project
// because it depends on IBillingService.
builder.Services.AddScoped<Vendorea.PartnerConnect.Api.Services.IOrganizationOnboardingService,
    Vendorea.PartnerConnect.Api.Services.OrganizationOnboardingService>();

// Transactional email (activation links etc). SMTP-backed, bound from the "Email" config section;
// degrades gracefully (logs, never throws) and — in Development — logs the message + link so the
// activation flow is testable with no SMTP sink running.
// IEmailSender is registered in AddPartnerConnectServices (shared with the workers host). Bind
// EmailOptions from THIS host's config so the API's Email settings apply (workers use defaults).
builder.Services.Configure<Vendorea.PartnerConnect.Infrastructure.Services.EmailOptions>(
    builder.Configuration.GetSection(Vendorea.PartnerConnect.Infrastructure.Services.EmailOptions.SectionName));

var jwtSettings = builder.Configuration
    .GetSection(Vendorea.PartnerConnect.Api.Authentication.JwtSettings.SectionName)
    .Get<Vendorea.PartnerConnect.Api.Authentication.JwtSettings>()
    ?? new Vendorea.PartnerConnect.Api.Authentication.JwtSettings();

// Authentication. Two schemes coexist: the org/dealer/admin API-key scheme (machine/integration
// callers via X-API-Key) and a JWT bearer scheme (customer-portal user tokens via Authorization:
// Bearer). A smart selector routes each request to the right one based on its headers, so the
// default principal is populated correctly for either credential.
const string SmartAuthScheme = "Smart";
builder.Services.AddAuthentication(SmartAuthScheme)
    .AddPolicyScheme(SmartAuthScheme, "API key or bearer token", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            // Prefer the API key. Integrations (e.g. Merchant360) authenticate with X-API-Key, and
            // some HttpClient pipelines also attach an Authorization: Bearer header that is NOT a PC
            // JWT — routing those to the JWT scheme would reject a valid API-key caller (a regression).
            // So: if X-API-Key is present, use the API-key scheme; only route to JWT for a Bearer token
            // with no API key (the customer-portal user tokens).
            if (context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.ApiKeyHeaderName))
            {
                return ApiKeyAuthenticationHandler.AuthenticationScheme;
            }
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader)
                && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            }
            return ApiKeyAuthenticationHandler.AuthenticationScheme;
        };
    })
    .AddApiKeyAuthentication()
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep the raw claim types (sub, role, scope, token_type)
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// Authorization with permissions
builder.Services.AddPermissionAuthorization();

// API-key scope authorization: a dynamic "scope:<value>" policy provider + handler, plus a global
// fallback policy so EVERY endpoint requires an authenticated key unless explicitly [AllowAnonymous].
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    Vendorea.PartnerConnect.Api.Authorization.ScopeAuthorizationHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider,
    Vendorea.PartnerConnect.Api.Authorization.ScopePolicyProvider>();
// Default-deny: any endpoint that does NOT declare its own [Authorize]/[RequireScope] (i.e. all
// internal/admin controllers) requires an authenticated key carrying the admin scope ("*" satisfies
// it). The M360-facing controllers opt into their own least-privilege scopes, so org keys reach only
// those. Truly public endpoints (e.g. /health) must opt out with [AllowAnonymous].
builder.Services.Configure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            ApiKeyAuthenticationHandler.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .AddRequirements(new Vendorea.PartnerConnect.Api.Authorization.ScopeRequirement(
            Vendorea.PartnerConnect.Domain.Entities.ApiScopes.Admin))
        .Build();
});

// Resilience: a single hosted-service (e.g. WebhookDeliveryWorker) throwing must not stop the whole
// API host. The default StopHost would take the API down with it.
builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(o =>
    o.BackgroundServiceExceptionBehavior = Microsoft.Extensions.Hosting.BackgroundServiceExceptionBehavior.Ignore);

// Rate limiting for the public/anonymous auth surface (register, access-request, forgot-password,
// login). Opt-in via [EnableRateLimiting(...)] on those endpoints only — the authenticated app/API
// surface is never throttled.
builder.Services.AddPublicRateLimiting();

// Health checks
builder.Services.AddHealthChecks();

// CORS. In production, restrict to configured origins (Cors:AllowedOrigins). When none are
// configured (e.g. local dev), allow any origin but WITHOUT credentials — API-key auth travels in
// a header, not a cookie, so credentialed cross-origin access is unnecessary.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
        else
        {
            policy.SetIsOriginAllowed(_ => true)
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// Cleanup any orphaned ingestion runs from previous crashes
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<Vendorea.PartnerConnect.Persistence.PartnerConnectDbContext>();
        var orphanedRuns = dbContext.FtpIngestionRuns
            .Where(r => r.CompletedAt == null)
            .ToList();

        if (orphanedRuns.Any())
        {
            Log.Information("Found {Count} orphaned ingestion runs, marking as failed", orphanedRuns.Count);
            foreach (var run in orphanedRuns)
            {
                run.Success = false;
                run.CompletedAt = DateTime.UtcNow;
                run.Errors.Add("Process terminated unexpectedly (API restart)");
            }
            dbContext.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not cleanup orphaned ingestion runs");
    }
}

// Seed the default Admin Portal user (pcadmin) when no active Admin exists.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<Vendorea.PartnerConnect.Persistence.PartnerConnectDbContext>();
        var hasAdmin = dbContext.AdminPortalUsers.Any(u =>
            u.Role == Vendorea.PartnerConnect.Domain.Entities.AdminPortalRole.Admin && u.IsActive);
        if (!hasAdmin)
        {
            dbContext.AdminPortalUsers.Add(new Vendorea.PartnerConnect.Domain.Entities.AdminPortalUser
            {
                Username = "pcadmin",
                DisplayName = "Portal Administrator",
                Role = Vendorea.PartnerConnect.Domain.Entities.AdminPortalRole.Admin,
                IsActive = true,
                PasswordHash = Vendorea.PartnerConnect.Infrastructure.Security.PortalPasswordHasher.Hash("12345678")
            });
            dbContext.SaveChanges();
            Log.Information("Seeded default Admin Portal user 'pcadmin'");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not seed default Admin Portal user");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PartnerConnect API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("Default");
app.UsePartnerConnectMiddleware();
app.UseAuthentication();
app.UseAuthorization();
// Must follow routing/auth so per-endpoint [EnableRateLimiting] policies resolve; only decorated
// (public/anonymous) endpoints are throttled.
app.UseRateLimiter();
app.UseSerilogRequestLogging();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

try
{
    Log.Information("Starting Vendorea PartnerConnect API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible for testing
public partial class Program { }
