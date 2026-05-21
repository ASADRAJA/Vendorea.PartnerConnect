using Serilog;
using Vendorea.PartnerConnect.Api.Authentication;
using Vendorea.PartnerConnect.Api.Authorization;
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

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

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

// Authentication
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.AuthenticationScheme)
    .AddApiKeyAuthentication();

// Authorization with permissions
builder.Services.AddPermissionAuthorization();

// Health checks
builder.Services.AddHealthChecks();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
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
app.UseCors("AllowAll");
app.UsePartnerConnectMiddleware();
app.UseAuthentication();
app.UseAuthorization();
app.UseSerilogRequestLogging();

app.MapControllers();
app.MapHealthChecks("/health");

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
