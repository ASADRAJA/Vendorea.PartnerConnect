using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Serilog;
using Vendorea.PartnerConnect.BackgroundWorkers;
using Vendorea.PartnerConnect.BackgroundWorkers.Workers;
using Vendorea.PartnerConnect.Infrastructure.DependencyInjection;
using Vendorea.PartnerConnect.Merchant360Connector;
using Vendorea.PartnerConnect.PartnerAdapters;
using Vendorea.PartnerConnect.Persistence;
using Vendorea.PartnerConnect.Storage;
using Vendorea.PartnerConnect.Transport;
using Vendorea.PartnerConnect.WorkerProcesses;

// Minimal web host: the background workers run as hosted services, and a lightweight HTTP
// listener exposes /health so Azure App Service (which expects an HTTP endpoint) keeps the
// always-on worker app loaded.
var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();

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

// Merchant360 connector with OAuth2 authentication
builder.Services.AddMerchant360Connector(options =>
{
    var section = builder.Configuration.GetSection("Merchant360");
    options.BaseUrl = section.GetValue<string>("BaseUrl") ?? "http://localhost:5003";
    options.TokenEndpoint = section.GetValue<string>("TokenEndpoint") ?? "http://localhost:5003/oauth2/token";
    options.ClientId = section.GetValue<string>("ClientId") ?? "";
    options.ClientSecret = section.GetValue<string>("ClientSecret") ?? "";
});

// Configure outbox worker options
builder.Services.Configure<OutboxWorkerOptions>(
    builder.Configuration.GetSection("OutboxWorker"));

// Background workers
builder.Services.AddHostedService<PriceFeedSyncWorker>();
// InventoryFeedSyncWorker (placeholder SFTP/CSV interval worker) retired — SPR inventory is now
// imported by the "spr-inventory" cron job (sprfull.ezoh over FTP) via ScheduledJobsCoordinator.
builder.Services.AddHostedService<DocumentProcessingWorker>();
builder.Services.AddHostedService<ContentSyncWorker>();
builder.Services.AddHostedService<OutboxProcessorWorker>();
builder.Services.AddHostedService<EdiDocumentSyncWorker>();

// Generic cron-jobs framework coordinator (runs DB-configured ScheduledJobs on their cron schedules)
builder.Services.AddHostedService<ScheduledJobsCoordinator>();

// SPR Content Ingestion Worker
builder.Services.AddWorkerProcesses(builder.Configuration);
builder.Services.AddSprContentIngestionWorker();

try
{
    Log.Information("Starting Vendorea PartnerConnect Background Workers");
    var app = builder.Build();
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
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
