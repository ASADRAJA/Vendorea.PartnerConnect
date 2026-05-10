using Serilog;
using Vendorea.PartnerConnect.BackgroundWorkers;
using Vendorea.PartnerConnect.BackgroundWorkers.Workers;
using Vendorea.PartnerConnect.Infrastructure.DependencyInjection;
using Vendorea.PartnerConnect.Merchant360Connector;
using Vendorea.PartnerConnect.PartnerAdapters;
using Vendorea.PartnerConnect.Persistence;
using Vendorea.PartnerConnect.Storage;
using Vendorea.PartnerConnect.Transport;

var builder = Host.CreateApplicationBuilder(args);

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

// Merchant360 connector
var merchant360BaseUrl = builder.Configuration.GetValue<string>("Merchant360:BaseUrl") ?? "http://localhost:5003";
builder.Services.AddMerchant360Connector(merchant360BaseUrl);

// Configure outbox worker options
builder.Services.Configure<OutboxWorkerOptions>(
    builder.Configuration.GetSection("OutboxWorker"));

// Background workers
builder.Services.AddHostedService<PriceFeedSyncWorker>();
builder.Services.AddHostedService<InventoryFeedSyncWorker>();
builder.Services.AddHostedService<DocumentProcessingWorker>();
builder.Services.AddHostedService<ContentSyncWorker>();
builder.Services.AddHostedService<OutboxProcessorWorker>();

try
{
    Log.Information("Starting Vendorea PartnerConnect Background Workers");
    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
