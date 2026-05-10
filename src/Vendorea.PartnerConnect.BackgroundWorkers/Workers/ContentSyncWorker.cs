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
        var feedService = scope.ServiceProvider.GetRequiredService<IFeedProcessingService>();

        var scheduledJobs = await jobRepo.GetScheduledJobsAsync(DateTime.UtcNow, cancellationToken);

        if (scheduledJobs.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} scheduled content sync jobs to process", scheduledJobs.Count);

        // Process jobs with controlled concurrency
        var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = scheduledJobs.Select(async job =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessContentSyncJobAsync(job, feedService, jobRepo, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task ProcessContentSyncJobAsync(
        ContentSyncJob job,
        IFeedProcessingService feedService,
        IContentSyncJobRepository jobRepo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting content sync job {JobId} for dealer {DealerId}, partner {PartnerId}, type {SyncType}",
            job.Id, job.DealerId, job.TradingPartnerId, job.SyncType);

        try
        {
            // Mark as running
            job.Status = ContentSyncStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            await jobRepo.UpdateAsync(job, cancellationToken);

            // Get connection ID for this dealer-partner combination
            using var scope = _serviceProvider.CreateScope();
            var connectionRepo = scope.ServiceProvider.GetRequiredService<IDealerPartnerConnectionRepository>();
            var connection = await connectionRepo.GetByDealerAndPartnerAsync(
                job.DealerId, job.TradingPartnerId, cancellationToken);

            if (connection == null)
            {
                throw new InvalidOperationException(
                    $"No connection found for dealer {job.DealerId} and partner {job.TradingPartnerId}");
            }

            // Process the content sync
            var result = await feedService.ProcessContentSyncAsync(
                connection.Id, job.SyncType, cancellationToken);

            // Update job with results
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

            if (job.Status == ContentSyncStatus.Completed)
            {
                _logger.LogInformation(
                    "Content sync job {JobId} completed: {Processed} products processed, {Updated} updated, {Images} images",
                    job.Id, job.ProcessedProducts, job.UpdatedProducts, job.NewImagesDownloaded);
            }
            else
            {
                _logger.LogWarning(
                    "Content sync job {JobId} ended with status {Status}: {Error}",
                    job.Id, job.Status, job.ErrorDetails);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing content sync job {JobId}", job.Id);

            job.Status = ContentSyncStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorDetails = ex.Message;
            await jobRepo.UpdateAsync(job, cancellationToken);
        }
    }
}
