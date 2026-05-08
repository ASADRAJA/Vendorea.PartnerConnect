using Serilog;
using Vendorea.PartnerConnect.Infrastructure.DependencyInjection;
using Vendorea.PartnerConnect.Merchant360Connector;
using Vendorea.PartnerConnect.PartnerAdapters;
using Vendorea.PartnerConnect.Persistence;

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
builder.Services.AddPartnerConnectPersistence();
builder.Services.AddPartnerAdapters();

// Merchant360 connector
var merchant360BaseUrl = builder.Configuration.GetValue<string>("Merchant360:BaseUrl") ?? "http://localhost:5003";
builder.Services.AddMerchant360Connector(merchant360BaseUrl);

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
