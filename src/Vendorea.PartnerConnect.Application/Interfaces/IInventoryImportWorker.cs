using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Interface for inventory import orchestration.
/// Drives the full-refresh inventory workflow.
/// </summary>
public interface IInventoryImportOrchestrator
{
    /// <summary>
    /// Processes pending inventory snapshots (Received → Validated → Staged).
    /// </summary>
    /// <param name="tradingPartnerId">Trading partner to process (null = all partners).</param>
    /// <param name="batchSize">Max snapshots to process in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result.</returns>
    Task<InventoryImportBatchResult> ProcessPendingSnapshotsAsync(
        int? tradingPartnerId = null,
        int batchSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies staged snapshots to production.
    /// </summary>
    /// <param name="tradingPartnerId">Trading partner to apply (null = all partners).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Apply result.</returns>
    Task<InventoryApplyBatchResult> ApplyStagedSnapshotsAsync(
        int? tradingPartnerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports inventory from a file stream.
    /// </summary>
    /// <param name="tradingPartnerId">Trading partner.</param>
    /// <param name="fileName">Source file name.</param>
    /// <param name="fileContent">File content stream.</param>
    /// <param name="contentType">Content type (e.g., text/csv, application/xml).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result.</returns>
    Task<InventoryFileImportResult> ImportFromFileAsync(
        int tradingPartnerId,
        string fileName,
        Stream fileContent,
        string contentType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of processing pending inventory snapshots.
/// </summary>
public class InventoryImportBatchResult
{
    /// <summary>
    /// Total snapshots processed.
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Snapshots validated and staged.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Snapshots that failed validation.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Individual snapshot results.
    /// </summary>
    public List<InventorySnapshotProcessResult> Results { get; set; } = new();

    /// <summary>
    /// Processing duration in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// Result of processing a single snapshot.
/// </summary>
public class InventorySnapshotProcessResult
{
    /// <summary>
    /// Snapshot ID.
    /// </summary>
    public int SnapshotId { get; set; }

    /// <summary>
    /// Trading partner ID.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// Whether processing succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Snapshot status after processing.
    /// </summary>
    public InventorySnapshotStatus Status { get; set; }

    /// <summary>
    /// Number of items processed.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Number of errors.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of applying staged snapshots.
/// </summary>
public class InventoryApplyBatchResult
{
    /// <summary>
    /// Total snapshots applied.
    /// </summary>
    public int TotalApplied { get; set; }

    /// <summary>
    /// Snapshots that applied successfully.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Snapshots that failed to apply.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Individual apply results.
    /// </summary>
    public List<InventorySnapshotApplyResult> Results { get; set; } = new();

    /// <summary>
    /// Processing duration in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// Result of applying a single snapshot.
/// </summary>
public class InventorySnapshotApplyResult
{
    /// <summary>
    /// Snapshot ID.
    /// </summary>
    public int SnapshotId { get; set; }

    /// <summary>
    /// Whether apply succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// New items added.
    /// </summary>
    public int NewItems { get; set; }

    /// <summary>
    /// Items updated.
    /// </summary>
    public int UpdatedItems { get; set; }

    /// <summary>
    /// Items removed.
    /// </summary>
    public int RemovedItems { get; set; }

    /// <summary>
    /// Previous snapshot that was superseded.
    /// </summary>
    public int? SupersededSnapshotId { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of importing inventory from a file.
/// </summary>
public class InventoryFileImportResult
{
    /// <summary>
    /// Whether import succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Created snapshot ID.
    /// </summary>
    public int? SnapshotId { get; set; }

    /// <summary>
    /// Snapshot status after import.
    /// </summary>
    public InventorySnapshotStatus Status { get; set; }

    /// <summary>
    /// Total items in file.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Valid items staged.
    /// </summary>
    public int ValidItems { get; set; }

    /// <summary>
    /// Invalid items skipped.
    /// </summary>
    public int InvalidItems { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Import duration in milliseconds.
    /// </summary>
    public long ImportTimeMs { get; set; }
}
