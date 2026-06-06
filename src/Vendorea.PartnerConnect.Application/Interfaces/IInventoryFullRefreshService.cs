using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for processing full-refresh inventory snapshots.
/// Implements staging-to-production workflow with atomic swap.
///
/// Full-refresh workflow:
/// 1. Receive inventory file → Received
/// 2. Validate structure and items → Validating → [ValidationFailed or Staging]
/// 3. Load into staging tables → Staging
/// 4. Verify staging data → Applying
/// 5. Atomic swap from staging to production → Applied
/// 6. Mark previous snapshot as Superseded
/// </summary>
public interface IInventoryFullRefreshService
{
    /// <summary>
    /// Creates a new inventory snapshot record from an uploaded file.
    /// </summary>
    /// <param name="tradingPartnerId">Trading partner ID.</param>
    /// <param name="fileName">Source file name.</param>
    /// <param name="inventoryDate">Date of the inventory data.</param>
    /// <param name="partnerDocumentId">Optional linked partner document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created snapshot.</returns>
    Task<SupplierInventorySnapshot> CreateSnapshotAsync(
        int tradingPartnerId,
        string fileName,
        DateTime inventoryDate,
        int? partnerDocumentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates inventory snapshot items.
    /// Transitions from Received → Validating → Staging or ValidationFailed.
    /// </summary>
    /// <param name="snapshotId">Snapshot ID.</param>
    /// <param name="items">Items to validate and stage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result.</returns>
    Task<InventoryValidationResult> ValidateAndStageAsync(
        int snapshotId,
        IEnumerable<SupplierInventoryItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a validated staging snapshot to production.
    /// Performs atomic swap: marks previous as Superseded, marks new as Applied.
    /// </summary>
    /// <param name="snapshotId">Snapshot ID to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Apply result with statistics.</returns>
    Task<InventoryApplyResult> ApplySnapshotAsync(
        int snapshotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current (most recently applied) snapshot for a trading partner.
    /// </summary>
    /// <param name="tradingPartnerId">Trading partner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current snapshot or null.</returns>
    Task<SupplierInventorySnapshot?> GetCurrentSnapshotAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshot by ID with items.
    /// </summary>
    /// <param name="snapshotId">Snapshot ID.</param>
    /// <param name="includeItems">Whether to include item details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Snapshot or null.</returns>
    Task<SupplierInventorySnapshot?> GetSnapshotAsync(
        int snapshotId,
        bool includeItems = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a snapshot as failed with an error message.
    /// </summary>
    /// <param name="snapshotId">Snapshot ID.</param>
    /// <param name="errorMessage">Error message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkFailedAsync(
        int snapshotId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supersedes previous snapshots when a newer one is applied.
    /// </summary>
    /// <param name="tradingPartnerId">Trading partner ID.</param>
    /// <param name="excludeSnapshotId">The new snapshot that should NOT be superseded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SupersedePreviousSnapshotsAsync(
        int tradingPartnerId,
        int excludeSnapshotId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of inventory validation.
/// </summary>
public class InventoryValidationResult
{
    /// <summary>
    /// Whether validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Total items validated.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Items that passed validation.
    /// </summary>
    public int ValidItems { get; set; }

    /// <summary>
    /// Items with validation errors.
    /// </summary>
    public int InvalidItems { get; set; }

    /// <summary>
    /// Validation errors (limited to first N).
    /// </summary>
    public List<InventoryItemError> Errors { get; set; } = new();

    /// <summary>
    /// Snapshot status after validation.
    /// </summary>
    public InventorySnapshotStatus ResultStatus { get; set; }
}

/// <summary>
/// Single inventory item validation error.
/// </summary>
public class InventoryItemError
{
    /// <summary>
    /// SKU with the error.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Line number in source file.
    /// </summary>
    public int? LineNumber { get; set; }
}

/// <summary>
/// Result of applying inventory snapshot.
/// </summary>
public class InventoryApplyResult
{
    /// <summary>
    /// Whether apply succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Snapshot ID that was applied.
    /// </summary>
    public int SnapshotId { get; set; }

    /// <summary>
    /// New items added to production.
    /// </summary>
    public int NewItems { get; set; }

    /// <summary>
    /// Existing items updated.
    /// </summary>
    public int UpdatedItems { get; set; }

    /// <summary>
    /// Items removed/zeroed (in previous but not in new).
    /// </summary>
    public int RemovedItems { get; set; }

    /// <summary>
    /// Items unchanged.
    /// </summary>
    public int UnchangedItems { get; set; }

    /// <summary>
    /// Previous snapshot ID that was superseded.
    /// </summary>
    public int? SupersededSnapshotId { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken to apply (ms).
    /// </summary>
    public long ApplyTimeMs { get; set; }
}
