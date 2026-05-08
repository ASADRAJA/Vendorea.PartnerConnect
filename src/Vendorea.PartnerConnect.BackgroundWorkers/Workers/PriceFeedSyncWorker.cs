namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Background worker that periodically syncs price feeds from trading partners.
/// </summary>
public class PriceFeedSyncWorker : BackgroundService
{
    private readonly ILogger<PriceFeedSyncWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public PriceFeedSyncWorker(
        ILogger<PriceFeedSyncWorker> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue<int>("Workers:PriceFeedSync:IntervalMinutes", 60);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        _logger.LogInformation("Price Feed Sync Worker starting with interval: {Interval} minutes", intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPriceFeedsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing price feeds");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Price Feed Sync Worker stopping");
    }

    private async Task ProcessPriceFeedsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting price feed sync at {Time}", DateTimeOffset.UtcNow);

        using var scope = _serviceProvider.CreateScope();

        // TODO: Inject IFeedProcessingService and process price feeds for all active connections
        // var feedService = scope.ServiceProvider.GetRequiredService<IFeedProcessingService>();
        // var connectionRepo = scope.ServiceProvider.GetRequiredService<IDealerPartnerConnectionRepository>();
        // var connections = await connectionRepo.GetActiveConnectionsAsync(cancellationToken);
        // foreach (var connection in connections) { ... }

        _logger.LogInformation("Price feed sync completed at {Time}", DateTimeOffset.UtcNow);
        await Task.CompletedTask;
    }
}
