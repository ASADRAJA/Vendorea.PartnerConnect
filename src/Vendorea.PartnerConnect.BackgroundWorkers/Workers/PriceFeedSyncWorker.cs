using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

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
        var maxConcurrentConnections = _configuration.GetValue<int>("Workers:PriceFeedSync:MaxConcurrentConnections", 5);
        var initialDelaySeconds = _configuration.GetValue<int>("Workers:PriceFeedSync:InitialDelaySeconds", 30);

        _logger.LogInformation(
            "Price Feed Sync Worker starting with interval: {Interval} minutes, max concurrent: {MaxConcurrent}",
            intervalMinutes, maxConcurrentConnections);

        // Initial delay to allow services to fully start
        await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPriceFeedsAsync(maxConcurrentConnections, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing price feeds");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Price Feed Sync Worker stopping");
    }

    private async Task ProcessPriceFeedsAsync(int maxConcurrent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting price feed sync at {Time}", DateTimeOffset.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var connectionRepo = scope.ServiceProvider.GetRequiredService<IDealerPartnerConnectionRepository>();
        var feedService = scope.ServiceProvider.GetRequiredService<IFeedProcessingService>();
        var batchRepo = scope.ServiceProvider.GetRequiredService<IPriceFeedBatchRepository>();

        var connections = await connectionRepo.GetActiveConnectionsAsync(cancellationToken);

        // Filter to connections that support price feeds
        var priceConnections = connections
            .Where(c => SupportsPriceFeeds(c))
            .ToList();

        if (priceConnections.Count == 0)
        {
            _logger.LogInformation("No active connections with price feed capability found");
            return;
        }

        _logger.LogInformation("Found {Count} connections to process for price feeds", priceConnections.Count);

        // Process connections with controlled concurrency
        var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = priceConnections.Select(async connection =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessConnectionPriceFeedAsync(connection, feedService, batchRepo, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("Price feed sync completed at {Time}", DateTimeOffset.UtcNow);
    }

    private async Task ProcessConnectionPriceFeedAsync(
        DealerPartnerConnection connection,
        IFeedProcessingService feedService,
        IPriceFeedBatchRepository batchRepo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing price feed for connection {ConnectionId} (Dealer: {DealerId}, Partner: {PartnerCode})",
            connection.Id, connection.DealerId, connection.TradingPartner?.Code);

        try
        {
            // Check if we should skip based on last sync time
            if (ShouldSkipSync(connection))
            {
                _logger.LogDebug(
                    "Skipping price feed sync for connection {ConnectionId} - synced recently",
                    connection.Id);
                return;
            }

            var batch = await feedService.ProcessPriceFeedAsync(connection.Id, cancellationToken);

            // Save the batch result
            await batchRepo.AddAsync(batch, cancellationToken);

            if (batch.Status == FeedBatchStatus.Completed)
            {
                _logger.LogInformation(
                    "Price feed completed for connection {ConnectionId}: {Processed} items processed, {Updated} updated",
                    connection.Id, batch.ProcessedItems, batch.UpdatedItems);
            }
            else if (batch.Status == FeedBatchStatus.Failed)
            {
                _logger.LogWarning(
                    "Price feed failed for connection {ConnectionId}: {Error}",
                    connection.Id, batch.ErrorSummary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing price feed for connection {ConnectionId}",
                connection.Id);
        }
    }

    private bool SupportsPriceFeeds(DealerPartnerConnection connection)
    {
        // Check if the partner supports price feeds
        // For now, assume all active connections support price feeds
        // In a real implementation, this would check capability configurations
        return connection.Status == ConnectionStatus.Active;
    }

    private bool ShouldSkipSync(DealerPartnerConnection connection)
    {
        // Skip if synced within the last 30 minutes (configurable)
        var minimumInterval = _configuration.GetValue<int>("Workers:PriceFeedSync:MinimumIntervalMinutes", 30);

        if (connection.LastSyncAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - connection.LastSyncAt.Value;
            return elapsed.TotalMinutes < minimumInterval;
        }

        return false;
    }
}
