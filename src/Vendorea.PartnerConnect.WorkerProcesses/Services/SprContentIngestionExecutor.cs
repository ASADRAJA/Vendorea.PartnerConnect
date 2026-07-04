using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Persistence;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;
using Vendorea.PartnerConnect.WorkerProcesses.Storage;

namespace Vendorea.PartnerConnect.WorkerProcesses.Services;

/// <summary>
/// Runs the SPR content ingestion pipeline for a single claimed <c>FtpIngestionRun</c>.
/// Extracted verbatim from the old fire-and-forget <c>Task.Run</c> in AdminFtpIngestionController
/// so the heavy Download → Import → Transform work now runs in the BackgroundWorkers host, drained
/// from a queue with stale-reclaim (mirroring the price-feed upload pattern).
/// </summary>
public class SprContentIngestionExecutor : ISprContentIngestionExecutor
{
    private readonly ILogger<SprContentIngestionExecutor> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly PartnerConnectDbContext _dbContext;
    private readonly IFtpIngestionRunRepository _runRepository;
    private readonly ISprRawToCanonicalTransformService _transformService;

    public SprContentIngestionExecutor(
        ILogger<SprContentIngestionExecutor> logger,
        ILoggerFactory loggerFactory,
        PartnerConnectDbContext dbContext,
        IFtpIngestionRunRepository runRepository,
        ISprRawToCanonicalTransformService transformService)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _dbContext = dbContext;
        _runRepository = runRepository;
        _transformService = transformService;
    }

    public async Task ExecuteAsync(int runId, SprContentIngestionOptions runOptions, CancellationToken ct)
    {
        try
        {
            var options = Options.Create(runOptions);

            // Storage selection mirrors the controller's original construction: Azure Blob when the
            // partner config asks for it, otherwise the local file system.
            var storage = runOptions.UseAzureBlobStorage
                ? (IIngestionFileStorage)new BlobIngestionFileStorage(
                    _loggerFactory.CreateLogger<BlobIngestionFileStorage>(), options)
                : new LocalIngestionFileStorage(
                    _loggerFactory.CreateLogger<LocalIngestionFileStorage>(), options);

            var ftpService = new SprFtpDownloadService(
                _loggerFactory.CreateLogger<SprFtpDownloadService>(),
                options,
                storage);

            var importService = new SprCsvBulkImportService(
                _loggerFactory.CreateLogger<SprCsvBulkImportService>(),
                _dbContext,
                options,
                storage);

            // The transform service is resolved from DI (as the controller did) and uses the
            // globally-configured options; the per-run options above drive download + import.
            var transformService = _transformService;

            var runRecord = await _runRepository.GetByIdAsync(runId, ct);
            if (runRecord == null)
            {
                _logger.LogWarning("FtpIngestionRun {RunId} not found; nothing to execute", runId);
                return;
            }

            // The run was already claimed (Queued -> Running) atomically; keep it Running through
            // intermediate saves so a mid-run save can't revert a stale tracked Status.
            runRecord.Status = "Running";

            // Step 1: Download
            runRecord.Phase = "Downloading";
            var downloadResult = await ftpService.DownloadAllFilesAsync(ct);

            runRecord.FilesDownloaded = downloadResult.FilesDownloaded;
            runRecord.BytesDownloaded = downloadResult.TotalBytesDownloaded;

            if (!downloadResult.Success)
            {
                runRecord.Status = "Failed";
                runRecord.Success = false;
                runRecord.Errors.AddRange(downloadResult.Errors);
                runRecord.CompletedAt = DateTime.UtcNow;
                await _runRepository.SaveRunAsync(runRecord, ct);
                return;
            }

            // Step 2: Import to raw schema
            runRecord.Phase = "Importing";
            var importResult = await importService.ImportAllAsync(downloadResult.Files, ct);
            runRecord.TablesImported = importResult.TablesSucceeded;
            runRecord.RowsImported = importResult.TotalRowsInserted;

            if (!importResult.Success)
            {
                runRecord.Status = "Failed";
                runRecord.Success = false;
                runRecord.Errors.AddRange(importResult.Errors);
                runRecord.CompletedAt = DateTime.UtcNow;
                await _runRepository.SaveRunAsync(runRecord, ct);
                return;
            }

            // Save intermediate progress
            await _runRepository.SaveRunAsync(runRecord, ct);

            // Step 3: Transform to canonical
            runRecord.Phase = "Transforming";
            var transformResult = await transformService.TransformAllAsync(ct);

            runRecord.ProductsTransformed = transformResult.ProductsTransformed;
            runRecord.CategoriesTransformed = transformResult.CategoriesTransformed;
            runRecord.FeaturesTransformed = transformResult.FeaturesTransformed;
            runRecord.RelationshipsTransformed = transformResult.RelationshipsTransformed;
            runRecord.SpecificationsTransformed = transformResult.SpecificationsTransformed;
            runRecord.Success = transformResult.Success;
            runRecord.Status = transformResult.Success ? "Succeeded" : "Failed";
            runRecord.Errors.AddRange(transformResult.Errors);
            runRecord.CompletedAt = DateTime.UtcNow;
            await _runRepository.SaveRunAsync(runRecord, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background ingestion failed for run {RunId}", runId);
            var runRecord = await _runRepository.GetByIdAsync(runId, CancellationToken.None);
            if (runRecord != null)
            {
                runRecord.Status = "Failed";
                runRecord.Success = false;
                runRecord.Errors.Add($"Ingestion failed: {ex.Message}");
                runRecord.CompletedAt = DateTime.UtcNow;
                await _runRepository.SaveRunAsync(runRecord, CancellationToken.None);
            }
        }
    }
}
