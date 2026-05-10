using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

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
        var maxConcurrentConnections = _configuration.GetValue<int>("Workers:InventoryFeedSync:MaxConcurrentConnections", 5);
        var initialDelaySeconds = _configuration.GetValue<int>("Workers:InventoryFeedSync:InitialDelaySeconds", 45);

        _logger.LogInformation(
            "Inventory Feed Sync Worker starting with interval: {Interval} minutes, max concurrent: {MaxConcurrent}",
            intervalMinutes, maxConcurrentConnections);

        // Initial delay to allow services to fully start (stagger with price feed worker)
        await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessInventoryFeedsAsync(maxConcurrentConnections, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inventory feeds");
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

        _logger.LogInformation("Inventory Feed Sync Worker stopping");
    }

    private async Task ProcessInventoryFeedsAsync(int maxConcurrent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting inventory feed sync at {Time}", DateTimeOffset.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var connectionRepo = scope.ServiceProvider.GetRequiredService<IDealerPartnerConnectionRepository>();
        var feedService = scope.ServiceProvider.GetRequiredService<IFeedProcessingService>();
        var batchRepo = scope.ServiceProvider.GetRequiredService<IInventoryFeedBatchRepository>();

        var connections = await connectionRepo.GetActiveConnectionsAsync(cancellationToken);

        // Filter to connections that support inventory feeds
        var inventoryConnections = connections
            .Where(c => SupportsInventoryFeeds(c))
            .ToList();

        if (inventoryConnections.Count == 0)
        {
            _logger.LogInformation("No active connections with inventory feed capability found");
            return;
        }

        _logger.LogInformation("Found {Count} connections to process for inventory feeds", inventoryConnections.Count);

        // Process connections with controlled concurrency
        var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = inventoryConnections.Select(async connection =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessConnectionInventoryFeedAsync(connection, feedService, batchRepo, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("Inventory feed sync completed at {Time}", DateTimeOffset.UtcNow);
    }

    private async Task ProcessConnectionInventoryFeedAsync(
        DealerPartnerConnection connection,
        IFeedProcessingService feedService,
        IInventoryFeedBatchRepository batchRepo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing inventory feed for connection {ConnectionId} (Dealer: {DealerId}, Partner: {PartnerCode})",
            connection.Id, connection.DealerId, connection.TradingPartner?.Code);

        try
        {
            // Check if we should skip based on last sync time
            if (ShouldSkipSync(connection))
            {
                _logger.LogDebug(
                    "Skipping inventory feed sync for connection {ConnectionId} - synced recently",
                    connection.Id);
                return;
            }

            var batch = await feedService.ProcessInventoryFeedAsync(connection.Id, cancellationToken);

            // Save the batch result
            await batchRepo.AddAsync(batch, cancellationToken);

            if (batch.Status == FeedBatchStatus.Completed)
            {
                _logger.LogInformation(
                    "Inventory feed completed for connection {ConnectionId}: {Processed} items processed, {Updated} updated",
                    connection.Id, batch.ProcessedItems, batch.UpdatedItems);
            }
            else if (batch.Status == FeedBatchStatus.Failed)
            {
                _logger.LogWarning(
                    "Inventory feed failed for connection {ConnectionId}: {Error}",
                    connection.Id, batch.ErrorSummary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing inventory feed for connection {ConnectionId}",
                connection.Id);
        }
    }

    private bool SupportsInventoryFeeds(DealerPartnerConnection connection)
    {
        // Check if the partner supports inventory feeds
        return connection.Status == ConnectionStatus.Active;
    }

    private bool ShouldSkipSync(DealerPartnerConnection connection)
    {
        // Inventory is more time-sensitive, use shorter minimum interval
        var minimumInterval = _configuration.GetValue<int>("Workers:InventoryFeedSync:MinimumIntervalMinutes", 15);

        if (connection.LastSyncAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - connection.LastSyncAt.Value;
            return elapsed.TotalMinutes < minimumInterval;
        }

        return false;
    }
}
