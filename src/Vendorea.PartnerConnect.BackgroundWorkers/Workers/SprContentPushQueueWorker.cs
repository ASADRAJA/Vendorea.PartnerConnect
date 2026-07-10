using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Drains queued SPR content -> Merchant360 pushes. The admin "push-start" endpoint only flips the
/// upload's <c>M360PushStatus</c> to Queued; the heavy categories + paged product push runs here, off
/// the API request thread, with progress persisted to the upload row so the push-status endpoint
/// survives an API/worker recycle. Mirrors <see cref="FtpIngestionQueueWorker"/>: atomic claim +
/// stale-reclaim, one DI scope per upload.
/// </summary>
public class SprContentPushQueueWorker : BackgroundService
{
    private readonly ILogger<SprContentPushQueueWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public SprContentPushQueueWorker(
        ILogger<SprContentPushQueueWorker> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollSeconds = _configuration.GetValue<int>("Workers:ContentPushQueue:PollSeconds", 30);
        var batchSize = _configuration.GetValue<int>("Workers:ContentPushQueue:BatchSize", 1);
        var initialDelaySeconds = _configuration.GetValue<int>("Workers:ContentPushQueue:InitialDelaySeconds", 15);
        // A full-catalog push is long (categories + paged product batches), so allow a generous window
        // before considering a Pushing row stranded.
        var staleMinutes = _configuration.GetValue<int>("Workers:ContentPushQueue:StaleProcessingMinutes", 60);
        var poll = TimeSpan.FromSeconds(pollSeconds);

        _logger.LogInformation(
            "SPR Content Push Queue Worker starting (poll every {PollSeconds}s, batch {BatchSize})",
            pollSeconds, batchSize);

        try { await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainQueuedPushesAsync(batchSize, staleMinutes, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error draining queued SPR content pushes");
            }

            try
            {
                await Task.Delay(poll, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("SPR Content Push Queue Worker stopping");
    }

    private async Task DrainQueuedPushesAsync(int batchSize, int staleMinutes, CancellationToken cancellationToken)
    {
        // A dedicated scope for reclaim + queue lookup; each claimed push then gets its own scope.
        List<int> queuedIds;
        using (var scope = _scopeFactory.CreateScope())
        {
            var uploadRepository = scope.ServiceProvider.GetRequiredService<ISprContentUploadRepository>();

            // Safety net: fail pushes stranded in Pushing by a crashed/restarted worker so an operator
            // can re-trigger (no auto-requeue — a re-click is an idempotent upsert).
            if (staleMinutes > 0)
            {
                var reclaimed = await uploadRepository.ReclaimStaleM360PushAsync(
                    DateTime.UtcNow.AddMinutes(-staleMinutes), cancellationToken);
                if (reclaimed > 0)
                    _logger.LogWarning("Reclaimed {Count} stale Pushing SPR content push(es) as Failed", reclaimed);
            }

            var queued = await uploadRepository.GetByM360PushStatusAsync("Queued", batchSize, cancellationToken);
            queuedIds = queued.Select(u => u.Id).ToList();
        }

        if (queuedIds.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} queued SPR content push(es) to process", queuedIds.Count);

        // Process sequentially: each push is a full-catalog categories + product upsert.
        foreach (var uploadId in queuedIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await ProcessPushAsync(uploadId, cancellationToken);
        }
    }

    private async Task ProcessPushAsync(int uploadId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var uploadRepository = scope.ServiceProvider.GetRequiredService<ISprContentUploadRepository>();
        var importService = scope.ServiceProvider.GetRequiredService<ISprContentImportService>();

        // Atomic Queued -> Pushing so only one worker instance runs this push.
        var claimed = await uploadRepository.TryClaimM360PushAsync(uploadId, cancellationToken);
        if (!claimed)
        {
            return;
        }

        _logger.LogInformation("Processing SPR content push for upload {UploadId}", uploadId);
        await importService.ExecuteQueuedM360PushAsync(uploadId, cancellationToken);
    }
}
