using Serilog;
using Vendorea.PartnerConnect.BackgroundWorkers;
using Vendorea.PartnerConnect.BackgroundWorkers.Workers;
using Vendorea.PartnerConnect.Infrastructure.DependencyInjection;
using Vendorea.PartnerConnect.Merchant360Connector;
using Vendorea.PartnerConnect.PartnerAdapters;
using Vendorea.PartnerConnect.Persistence;

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

builder.Services.AddPartnerAdapters();

// Merchant360 connector
var merchant360BaseUrl = builder.Configuration.GetValue<string>("Merchant360:BaseUrl") ?? "http://localhost:5003";
builder.Services.AddMerchant360Connector(merchant360BaseUrl);

// Background workers
builder.Services.AddHostedService<PriceFeedSyncWorker>();
builder.Services.AddHostedService<InventoryFeedSyncWorker>();

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
