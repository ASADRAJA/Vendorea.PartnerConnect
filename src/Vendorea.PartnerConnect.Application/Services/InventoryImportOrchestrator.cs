using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Orchestrates batch inventory import processing.
/// Coordinates snapshot lifecycle from receipt through application.
/// </summary>
public class InventoryImportOrchestrator : IInventoryImportOrchestrator
{
    private readonly IInventoryFullRefreshService _refreshService;
    private readonly ISupplierInventorySnapshotRepository _snapshotRepository;
    private readonly ILogger<InventoryImportOrchestrator> _logger;

    public InventoryImportOrchestrator(
        IInventoryFullRefreshService refreshService,
        ISupplierInventorySnapshotRepository snapshotRepository,
        ILogger<InventoryImportOrchestrator> logger)
    {
        _refreshService = refreshService;
        _snapshotRepository = snapshotRepository;
        _logger = logger;
    }

    public async Task<InventoryImportBatchResult> ProcessPendingSnapshotsAsync(
        int? tradingPartnerId = null,
        int batchSize = 10,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new InventoryImportBatchResult();

        try
        {
            // Get snapshots in Received or Staging status ready for processing
            var snapshots = await _snapshotRepository.GetByStatusAsync(
                new[] { InventorySnapshotStatus.Received, InventorySnapshotStatus.Staging },
                tradingPartnerId,
                batchSize,
                cancellationToken);

            _logger.LogInformation(
                "Processing {Count} pending inventory snapshots (Partner={PartnerId})",
                snapshots.Count, tradingPartnerId?.ToString() ?? "all");

            foreach (var snapshot in snapshots)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var snapshotResult = await ProcessSnapshotAsync(snapshot, cancellationToken);
                result.Results.Add(snapshotResult);

                if (snapshotResult.Success)
                    result.Succeeded++;
                else
                    result.Failed++;

                result.TotalProcessed++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during inventory import batch processing");
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        }

        _logger.LogInformation(
            "Inventory import batch completed: {Total} processed, {Succeeded} succeeded, {Failed} failed, {TimeMs}ms",
            result.TotalProcessed, result.Succeeded, result.Failed, result.ProcessingTimeMs);

        return result;
    }

    public async Task<InventoryApplyBatchResult> ApplyStagedSnapshotsAsync(
        int? tradingPartnerId = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new InventoryApplyBatchResult();

        try
        {
            // Get snapshots ready for apply (Staging status)
            var stagedSnapshots = await _snapshotRepository.GetByStatusAsync(
                new[] { InventorySnapshotStatus.Staging },
                tradingPartnerId,
                100, // Process all staged
                cancellationToken);

            _logger.LogInformation(
                "Applying {Count} staged inventory snapshots (Partner={PartnerId})",
                stagedSnapshots.Count, tradingPartnerId?.ToString() ?? "all");

            foreach (var snapshot in stagedSnapshots)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var applyResult = await ApplySnapshotWithResultAsync(snapshot, cancellationToken);
                result.Results.Add(applyResult);

                if (applyResult.Success)
                    result.Succeeded++;
                else
                    result.Failed++;

                result.TotalApplied++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during inventory apply batch processing");
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        }

        _logger.LogInformation(
            "Inventory apply batch completed: {Total} applied, {Succeeded} succeeded, {Failed} failed",
            result.TotalApplied, result.Succeeded, result.Failed);

        return result;
    }

    public async Task<InventoryFileImportResult> ImportFromFileAsync(
        int tradingPartnerId,
        string fileName,
        Stream fileContent,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new InventoryFileImportResult();

        try
        {
            // Create new snapshot
            var snapshot = await _refreshService.CreateSnapshotAsync(
                tradingPartnerId,
                fileName,
                DateTime.UtcNow,
                null,
                cancellationToken);

            result.SnapshotId = snapshot.Id;

            // Parse file content based on content type
            var items = await ParseInventoryFileAsync(fileContent, contentType, cancellationToken);
            result.TotalItems = items.Count;

            if (items.Count == 0)
            {
                result.ErrorMessage = "No items parsed from file";
                result.Status = InventorySnapshotStatus.Failed;
                await _refreshService.MarkFailedAsync(snapshot.Id, result.ErrorMessage, cancellationToken);
                return result;
            }

            // Validate and stage
            var validationResult = await _refreshService.ValidateAndStageAsync(
                snapshot.Id,
                items,
                cancellationToken);

            result.ValidItems = validationResult.ValidItems;
            result.InvalidItems = validationResult.InvalidItems;
            result.Status = validationResult.ResultStatus;
            result.Success = validationResult.IsValid;

            if (!validationResult.IsValid)
            {
                result.ErrorMessage = $"Validation failed: {validationResult.Errors.FirstOrDefault()?.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing inventory file {FileName} for partner {PartnerId}",
                fileName, tradingPartnerId);
            result.ErrorMessage = ex.Message;
            result.Status = InventorySnapshotStatus.Failed;
        }
        finally
        {
            stopwatch.Stop();
            result.ImportTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    private async Task<InventorySnapshotProcessResult> ProcessSnapshotAsync(
        SupplierInventorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var result = new InventorySnapshotProcessResult
        {
            SnapshotId = snapshot.Id,
            TradingPartnerId = snapshot.TradingPartnerId
        };

        try
        {
            if (snapshot.Status == InventorySnapshotStatus.Received)
            {
                // Snapshot needs items loaded and validated
                // In a real scenario, items would be loaded from the source document
                // For now, we just transition to validating if items are present
                var snapshotWithItems = await _snapshotRepository.GetByIdWithItemsAsync(
                    snapshot.Id, cancellationToken);

                if (snapshotWithItems?.Items.Count > 0)
                {
                    var validationResult = await _refreshService.ValidateAndStageAsync(
                        snapshot.Id,
                        snapshotWithItems.Items,
                        cancellationToken);

                    result.Success = validationResult.IsValid;
                    result.Status = validationResult.ResultStatus;
                    result.ItemCount = validationResult.TotalItems;
                    result.ErrorCount = validationResult.InvalidItems;

                    if (!validationResult.IsValid)
                    {
                        result.ErrorMessage = validationResult.Errors.FirstOrDefault()?.Message;
                    }
                }
                else
                {
                    // No items yet - leave as received
                    result.Status = InventorySnapshotStatus.Received;
                    result.ErrorMessage = "Snapshot has no items to process";
                }
            }
            else if (snapshot.Status == InventorySnapshotStatus.Staging)
            {
                // Already staged, apply it
                var applyResult = await _refreshService.ApplySnapshotAsync(
                    snapshot.Id, cancellationToken);

                result.Success = applyResult.Success;
                result.Status = applyResult.Success
                    ? InventorySnapshotStatus.Applied
                    : InventorySnapshotStatus.Failed;
                result.ItemCount = applyResult.NewItems + applyResult.UpdatedItems + applyResult.UnchangedItems;
                result.ErrorMessage = applyResult.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing snapshot {SnapshotId}", snapshot.Id);
            result.ErrorMessage = ex.Message;
            result.Status = InventorySnapshotStatus.Failed;
            await _refreshService.MarkFailedAsync(snapshot.Id, ex.Message, cancellationToken);
        }

        return result;
    }

    private async Task<InventorySnapshotApplyResult> ApplySnapshotWithResultAsync(
        SupplierInventorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var applyResult = await _refreshService.ApplySnapshotAsync(
            snapshot.Id, cancellationToken);

        return new InventorySnapshotApplyResult
        {
            SnapshotId = snapshot.Id,
            Success = applyResult.Success,
            NewItems = applyResult.NewItems,
            UpdatedItems = applyResult.UpdatedItems,
            RemovedItems = applyResult.RemovedItems,
            SupersededSnapshotId = applyResult.SupersededSnapshotId,
            ErrorMessage = applyResult.ErrorMessage
        };
    }

    private async Task<List<SupplierInventoryItem>> ParseInventoryFileAsync(
        Stream fileContent,
        string contentType,
        CancellationToken cancellationToken)
    {
        // This is a placeholder - actual implementation would delegate to
        // partner-specific parsers based on content type
        // For now, return empty list (real parsing happens in PartnerAdapters layer)
        await Task.CompletedTask;

        _logger.LogDebug(
            "ParseInventoryFileAsync called with content type {ContentType}. " +
            "Actual parsing should be done by partner adapters.",
            contentType);

        return new List<SupplierInventoryItem>();
    }
}
