using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;
using Vendorea.PartnerConnect.WorkerProcesses.Services;

namespace Vendorea.PartnerConnect.WorkerProcesses.Workers;

/// <summary>
/// Background worker that orchestrates SPR Enhanced Content ingestion.
/// Downloads from FTP, imports to raw schema, transforms to canonical schema.
/// </summary>
public class SprContentIngestionWorker : BackgroundService
{
    private readonly ILogger<SprContentIngestionWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SprContentIngestionOptions _options;

    public SprContentIngestionWorker(
        ILogger<SprContentIngestionWorker> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<SprContentIngestionOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SPR Content Ingestion Worker starting...");

        // Wait for initial delay if configured
        if (_options.InitialDelaySeconds > 0)
        {
            _logger.LogInformation("Waiting {Delay} seconds before first run...", _options.InitialDelaySeconds);
            try { await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check if we should run based on schedule
                if (ShouldRunNow())
                {
                    await RunIngestionAsync(stoppingToken);
                }
                else
                {
                    _logger.LogDebug("Not scheduled to run at this time. Next check in {Interval} minutes.",
                        _options.CheckIntervalMinutes);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SPR content ingestion");
            }

            // Wait before next check
            await Task.Delay(TimeSpan.FromMinutes(_options.CheckIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("SPR Content Ingestion Worker stopped.");
    }

    /// <summary>
    /// Runs the full ingestion pipeline: Download → Import → Transform
    /// </summary>
    public async Task<IngestionResult> RunIngestionAsync(CancellationToken cancellationToken = default)
    {
        var result = new IngestionResult
        {
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Starting SPR content ingestion pipeline...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ftpService = scope.ServiceProvider.GetRequiredService<ISprFtpDownloadService>();
            var importService = scope.ServiceProvider.GetRequiredService<ISprCsvBulkImportService>();
            var transformService = scope.ServiceProvider.GetRequiredService<ISprRawToCanonicalTransformService>();

            // Step 1: Download from FTP
            _logger.LogInformation("Step 1/3: Downloading files from FTP...");
            var downloadResult = await ftpService.DownloadAllFilesAsync(cancellationToken);
            result.DownloadResult = downloadResult;

            if (!downloadResult.Success)
            {
                _logger.LogError("FTP download failed. Aborting ingestion.");
                result.Success = false;
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }

            _logger.LogInformation("Downloaded {Count} files in {Duration}",
                downloadResult.FilesDownloaded, downloadResult.Duration);

            // Step 2: Import to raw schema
            _logger.LogInformation("Step 2/3: Importing CSV files to raw schema...");
            var importResult = await importService.ImportAllAsync(downloadResult.Files, cancellationToken);
            result.ImportResult = importResult;

            if (!importResult.Success)
            {
                _logger.LogError("CSV import failed. Aborting ingestion.");
                result.Success = false;
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }

            _logger.LogInformation("Imported {Rows:N0} rows into {Tables} tables in {Duration}",
                importResult.TotalRowsInserted, importResult.TablesSucceeded, importResult.Duration);

            // Step 3: Transform to canonical schema
            _logger.LogInformation("Step 3/3: Transforming to canonical schema...");
            var transformResult = await transformService.TransformAllAsync(cancellationToken);
            result.TransformResult = transformResult;

            if (!transformResult.Success)
            {
                _logger.LogError("Transform failed.");
                result.Success = false;
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }

            _logger.LogInformation(
                "Transformed {Products} products, {Categories} categories, " +
                "{Features} features, {Relationships} relationships, {Specs} specifications in {Duration}",
                transformResult.ProductsTransformed,
                transformResult.CategoriesTransformed,
                transformResult.FeaturesTransformed,
                transformResult.RelationshipsTransformed,
                transformResult.SpecificationsTransformed,
                transformResult.Duration);

            // Cleanup downloaded files if configured
            if (_options.CleanupAfterImport)
            {
                await CleanupDownloadedFilesAsync(downloadResult.Files);
            }

            result.Success = true;
            _logger.LogInformation("SPR content ingestion completed successfully in {Duration}",
                DateTime.UtcNow - result.StartedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SPR content ingestion pipeline failed");
            result.Success = false;
            result.Errors.Add(ex.Message);
        }

        result.CompletedAt = DateTime.UtcNow;
        return result;
    }

    private bool ShouldRunNow()
    {
        if (!_options.EnableScheduledRun)
            return false;

        var now = DateTime.UtcNow;
        var scheduledHour = _options.ScheduledRunHourUtc;

        // Check if we're within the scheduled hour window
        // Allow a 5-minute window to account for timing variations
        return now.Hour == scheduledHour && now.Minute < 5;
    }

    private async Task CleanupDownloadedFilesAsync(IReadOnlyList<DownloadedFileInfo> files)
    {
        foreach (var file in files.Where(f => f.Success && File.Exists(f.LocalPath)))
        {
            try
            {
                File.Delete(file.LocalPath);
                _logger.LogDebug("Deleted temporary file: {Path}", file.LocalPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary file: {Path}", file.LocalPath);
            }
        }

        // Also try to clean up the download directory if empty
        try
        {
            var downloadDir = _options.LocalDownloadPath;
            if (Directory.Exists(downloadDir) && !Directory.EnumerateFileSystemEntries(downloadDir).Any())
            {
                Directory.Delete(downloadDir);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
/// Result of a complete ingestion pipeline run.
/// </summary>
public class IngestionResult
{
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;

    public DownloadResult? DownloadResult { get; set; }
    public BulkImportResult? ImportResult { get; set; }
    public TransformResult? TransformResult { get; set; }

    public List<string> Errors { get; set; } = new();
}
