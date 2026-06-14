using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Background worker that processes scheduled content synchronization jobs.
/// Handles product content sync (descriptions, images, specifications).
/// </summary>
public class ContentSyncWorker : BackgroundService
{
    private readonly ILogger<ContentSyncWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public ContentSyncWorker(
        ILogger<ContentSyncWorker> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue<int>("Workers:ContentSync:IntervalMinutes", 60);
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        var maxConcurrentJobs = _configuration.GetValue<int>("Workers:ContentSync:MaxConcurrentJobs", 3);
        var staleJobTimeout = _configuration.GetValue<int>("Workers:ContentSync:StaleJobTimeoutMinutes", 120);
        var initialDelaySeconds = _configuration.GetValue<int>("Workers:ContentSync:InitialDelaySeconds", 60);

        _logger.LogInformation(
            "Content Sync Worker starting with interval: {Interval} minutes, max concurrent: {MaxConcurrent}",
            intervalMinutes, maxConcurrentJobs);

        // Initial delay
        await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Handle stale jobs first
                await HandleStaleJobsAsync(TimeSpan.FromMinutes(staleJobTimeout), stoppingToken);

                // Process scheduled jobs
                await ProcessScheduledJobsAsync(maxConcurrentJobs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in content sync worker");
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

        _logger.LogInformation("Content Sync Worker stopping");
    }

    private async Task HandleStaleJobsAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IContentSyncJobRepository>();

        var staleJobs = await jobRepo.GetStaleRunningJobsAsync(timeout, cancellationToken);

        foreach (var job in staleJobs)
        {
            _logger.LogWarning(
                "Marking stale content sync job {JobId} as failed (started: {StartedAt})",
                job.Id, job.StartedAt);

            job.Status = ContentSyncStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorDetails = $"Job timed out after running for more than {timeout.TotalMinutes} minutes";

            await jobRepo.UpdateAsync(job, cancellationToken);
        }
    }

    private async Task ProcessScheduledJobsAsync(int maxConcurrent, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IContentSyncJobRepository>();

        var scheduledJobs = await jobRepo.GetScheduledJobsAsync(DateTime.UtcNow, cancellationToken);

        if (scheduledJobs.Count == 0)
        {
            return;
        }

        // eContent is shared master data per partner — sync ONCE per (partner, sync type) and
        // apply the outcome to every dealer job in that group, instead of re-pulling identical
        // content once per dealer.
        var groups = scheduledJobs
            .GroupBy(j => (j.TradingPartnerId, j.SyncType))
            .ToList();

        _logger.LogInformation(
            "Processing content sync for {GroupCount} partner/type group(s) covering {JobCount} dealer job(s)",
            groups.Count, scheduledJobs.Count);

        var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = groups.Select(async group =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessPartnerContentGroupAsync(group.ToList(), jobRepo, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Syncs a partner's shared content once (via a representative job's connection — transport
    /// comes from the partner) and applies the outcome to every dealer job in the (partner, sync
    /// type) group, since they're all satisfied by the single shared sync.
    /// </summary>
    private async Task ProcessPartnerContentGroupAsync(
        List<ContentSyncJob> jobs,
        IContentSyncJobRepository jobRepo,
        CancellationToken cancellationToken)
    {
        var partnerId = jobs[0].TradingPartnerId;
        var syncType = jobs[0].SyncType;

        foreach (var job in jobs)
        {
            job.Status = ContentSyncStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            await jobRepo.UpdateAsync(job, cancellationToken);
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var feedService = scope.ServiceProvider.GetRequiredService<IFeedProcessingService>();

            // Content is shared per partner; transport comes from the partner directly.
            _logger.LogInformation(
                "Syncing shared content for partner {PartnerId} ({SyncType}) covering {Count} dealer job(s)",
                partnerId, syncType, jobs.Count);

            var result = await feedService.ProcessContentSyncAsync(partnerId, syncType, cancellationToken);

            foreach (var job in jobs)
            {
                job.TotalProducts = result.TotalProducts;
                job.ProcessedProducts = result.ProcessedProducts;
                job.UpdatedProducts = result.UpdatedProducts;
                job.NewImagesDownloaded = result.NewImagesDownloaded;
                job.SkippedProducts = result.SkippedProducts;
                job.ErrorProducts = result.ErrorProducts;
                job.Status = result.Status;
                job.CompletedAt = result.CompletedAt;
                job.ErrorDetails = result.ErrorDetails;
                await jobRepo.UpdateAsync(job, cancellationToken);
            }

            _logger.LogInformation(
                "Shared content sync for partner {PartnerId} ended {Status}: {Processed} processed, {Updated} updated",
                partnerId, result.Status, result.ProcessedProducts, result.UpdatedProducts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing shared content sync for partner {PartnerId}", partnerId);
            foreach (var job in jobs)
            {
                job.Status = ContentSyncStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.ErrorDetails = ex.Message;
                await jobRepo.UpdateAsync(job, cancellationToken);
            }
        }
    }
}
