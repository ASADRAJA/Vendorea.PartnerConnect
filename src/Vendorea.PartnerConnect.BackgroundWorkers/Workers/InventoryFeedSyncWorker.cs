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
        var partnerRepo = scope.ServiceProvider.GetRequiredService<ITradingPartnerRepository>();
        var feedService = scope.ServiceProvider.GetRequiredService<IFeedProcessingService>();
        var batchRepo = scope.ServiceProvider.GetRequiredService<IInventoryFeedBatchRepository>();

        var partners = await partnerRepo.GetByStatusAsync(TradingPartnerStatus.Active, cancellationToken);

        // Inventory is shared master data per partner — pull it ONCE per partner over the
        // partner's shared transport.
        var inventoryPartners = partners
            .Where(SupportsInventoryFeeds)
            .ToList();

        if (inventoryPartners.Count == 0)
        {
            _logger.LogInformation("No active trading partners with inventory feed capability found");
            return;
        }

        _logger.LogInformation("Processing inventory feeds for {Count} partner(s) (shared per partner)", inventoryPartners.Count);

        // Process partners with controlled concurrency
        var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = inventoryPartners.Select(async partner =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessPartnerInventoryFeedAsync(partner, feedService, batchRepo, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("Inventory feed sync completed at {Time}", DateTimeOffset.UtcNow);
    }

    private async Task ProcessPartnerInventoryFeedAsync(
        TradingPartner partner,
        IFeedProcessingService feedService,
        IInventoryFeedBatchRepository batchRepo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing inventory feed for partner {TradingPartnerId} ({PartnerCode})",
            partner.Id, partner.Code);

        try
        {
            var batch = await feedService.ProcessInventoryFeedAsync(partner.Id, cancellationToken);

            // Save the batch result
            await batchRepo.AddAsync(batch, cancellationToken);

            if (batch.Status == FeedBatchStatus.Completed)
            {
                _logger.LogInformation(
                    "Inventory feed completed for partner {TradingPartnerId}: {Processed} items processed, {Updated} updated",
                    partner.Id, batch.ProcessedItems, batch.UpdatedItems);
            }
            else if (batch.Status == FeedBatchStatus.Failed)
            {
                _logger.LogWarning(
                    "Inventory feed failed for partner {TradingPartnerId}: {Error}",
                    partner.Id, batch.ErrorSummary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing inventory feed for partner {TradingPartnerId}",
                partner.Id);
        }
    }

    private static bool SupportsInventoryFeeds(TradingPartner partner)
    {
        return partner.Status == TradingPartnerStatus.Active;
    }
}
