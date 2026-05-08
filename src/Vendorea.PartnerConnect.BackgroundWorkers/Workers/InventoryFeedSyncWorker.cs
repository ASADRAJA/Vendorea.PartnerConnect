namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Background worker that periodically syncs inventory feeds from trading partners.
/// </summary>
public class InventoryFeedSyncWorker : BackgroundService
{
    private readonly ILogger<InventoryFeedSyncWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public InventoryFeedSyncWorker(
        ILogger<InventoryFeedSyncWorker> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue<int>("Workers:InventoryFeedSync:IntervalMinutes", 30);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        _logger.LogInformation("Inventory Feed Sync Worker starting with interval: {Interval} minutes", intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessInventoryFeedsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inventory feeds");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Inventory Feed Sync Worker stopping");
    }

    private async Task ProcessInventoryFeedsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting inventory feed sync at {Time}", DateTimeOffset.UtcNow);

        using var scope = _serviceProvider.CreateScope();

        // TODO: Inject IFeedProcessingService and process inventory feeds for all active connections
        // var feedService = scope.ServiceProvider.GetRequiredService<IFeedProcessingService>();
        // var connectionRepo = scope.ServiceProvider.GetRequiredService<IDealerPartnerConnectionRepository>();
        // var connections = await connectionRepo.GetActiveConnectionsAsync(cancellationToken);
        // foreach (var connection in connections) { ... }

        _logger.LogInformation("Inventory feed sync completed at {Time}", DateTimeOffset.UtcNow);
        await Task.CompletedTask;
    }
}
