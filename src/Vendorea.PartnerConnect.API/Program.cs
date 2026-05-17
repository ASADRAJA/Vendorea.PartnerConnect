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

var builder = WebApplication.CreateBuilder(args);

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
