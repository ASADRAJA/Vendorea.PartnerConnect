using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;
using Vendorea.PartnerConnect.WorkerProcesses.Services;

namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Drains queued SPR content-ingestion runs. The admin "run" endpoint only creates a Queued
/// <c>FtpIngestionRun</c> row; the heavy Download → Import → Transform pipeline runs here, off the
/// API request thread, so long content runs never hit the Azure App Service 230s request limit.
/// Mirrors <see cref="PriceFeedUploadProcessingWorker"/>: atomic claim + stale-reclaim.
/// </summary>
public class FtpIngestionQueueWorker : BackgroundService
{
    private readonly ILogger<FtpIngestionQueueWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public FtpIngestionQueueWorker(
        ILogger<FtpIngestionQueueWorker> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollSeconds = _configuration.GetValue<int>("Workers:FtpIngestionQueue:PollSeconds", 30);
        var batchSize = _configuration.GetValue<int>("Workers:FtpIngestionQueue:BatchSize", 1);
        var initialDelaySeconds = _configuration.GetValue<int>("Workers:FtpIngestionQueue:InitialDelaySeconds", 15);
        // Content runs are long (full download + bulk import + transform), so allow a generous window
        // before considering a Running row stranded.
        var staleMinutes = _configuration.GetValue<int>("Workers:FtpIngestionQueue:StaleProcessingMinutes", 180);
        var poll = TimeSpan.FromSeconds(pollSeconds);

        _logger.LogInformation(
            "FTP Ingestion Queue Worker starting (poll every {PollSeconds}s, batch {BatchSize})",
            pollSeconds, batchSize);

        try { await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainQueuedRunsAsync(batchSize, staleMinutes, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error draining queued FTP ingestion runs");
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

        _logger.LogInformation("FTP Ingestion Queue Worker stopping");
    }

    private async Task DrainQueuedRunsAsync(int batchSize, int staleMinutes, CancellationToken cancellationToken)
    {
        // A dedicated scope for reclaim + claim bookkeeping; each claimed run then gets its own scope.
        List<int> queuedIds;
        using (var scope = _scopeFactory.CreateScope())
        {
            var runRepository = scope.ServiceProvider.GetRequiredService<IFtpIngestionRunRepository>();

            // Safety net: un-stick runs stranded in Running by a crashed/restarted worker.
            if (staleMinutes > 0)
            {
                var reclaimed = await runRepository.ReclaimStaleAsync(
                    DateTime.UtcNow.AddMinutes(-staleMinutes), cancellationToken);
                if (reclaimed > 0)
                    _logger.LogWarning("Reclaimed {Count} stale Running ingestion run(s) as Failed", reclaimed);
            }

            var queued = await runRepository.GetByStatusAsync("Queued", batchSize, cancellationToken);
            queuedIds = queued.Select(r => r.Id).ToList();
        }

        if (queuedIds.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} queued FTP ingestion run(s) to process", queuedIds.Count);

        // Process sequentially: each run is a heavy download + bulk insert + transform.
        foreach (var runId in queuedIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await ProcessRunAsync(runId, cancellationToken);
        }
    }

    private async Task ProcessRunAsync(int runId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var runRepository = scope.ServiceProvider.GetRequiredService<IFtpIngestionRunRepository>();
        var configRepository = scope.ServiceProvider.GetRequiredService<IPartnerIngestionConfigRepository>();
        var executor = scope.ServiceProvider.GetRequiredService<ISprContentIngestionExecutor>();

        // Atomic Queued -> Running so only one worker instance picks up this run.
        var claimed = await runRepository.TryClaimAsync(runId, cancellationToken);
        if (!claimed)
        {
            return;
        }

        // Load the SPR partner config and build per-run options the same way the controller did.
        var dbConfig = await configRepository.GetByPartnerCodeAsync("SPR", cancellationToken);
        if (dbConfig == null || string.IsNullOrEmpty(dbConfig.FtpUsername))
        {
            _logger.LogError("SPR ingestion config missing/incomplete; failing run {RunId}", runId);
            var runRecord = await runRepository.GetByIdAsync(runId, cancellationToken);
            if (runRecord != null)
            {
                runRecord.Status = "Failed";
                runRecord.Success = false;
                runRecord.Errors.Add("FTP configuration not found or incomplete.");
                runRecord.CompletedAt = DateTime.UtcNow;
                await runRepository.SaveRunAsync(runRecord, cancellationToken);
            }
            return;
        }

        var options = new SprContentIngestionOptions
        {
            FtpHost = dbConfig.FtpHost,
            FtpUsername = dbConfig.FtpUsername,
            FtpPassword = dbConfig.FtpPassword,
            LocalDownloadPath = !string.IsNullOrEmpty(dbConfig.LocalDownloadPath)
                ? dbConfig.LocalDownloadPath
                : Path.Combine(Path.GetTempPath(), "spr-inquire"),
            Locale = dbConfig.Locale,
            DatabaseType = dbConfig.DatabaseType,
            BulkInsertBatchSize = dbConfig.BulkInsertBatchSize > 0 ? dbConfig.BulkInsertBatchSize : 10000,
            UseAzureBlobStorage = dbConfig.UseAzureBlobStorage,
            AzureBlobConnectionString = dbConfig.AzureBlobConnectionString,
            AzureBlobContainerName = dbConfig.AzureBlobContainerName ?? "spr-content-ingestion"
        };

        _logger.LogInformation("Processing FTP ingestion run {RunId}", runId);
        await executor.ExecuteAsync(runId, options, cancellationToken);

        var completed = await runRepository.GetByIdAsync(runId, cancellationToken);
        _logger.LogInformation(
            "Completed FTP ingestion run {RunId}: status={Status}", runId, completed?.Status);
    }
}
