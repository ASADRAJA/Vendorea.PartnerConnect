namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// A point-in-time inventory snapshot from a supplier.
/// Represents a full-refresh of inventory data.
/// </summary>
public class SupplierInventorySnapshot
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the PartnerDocument containing this snapshot.
    /// </summary>
    public int? PartnerDocumentId { get; set; }

    /// <summary>
    /// Trading partner providing this inventory.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// Snapshot identifier/filename.
    /// </summary>
    public string SnapshotId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the inventory data from supplier.
    /// </summary>
    public DateTime InventoryDate { get; set; }

    /// <summary>
    /// When this snapshot was received.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When processing started.
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }

    /// <summary>
    /// When processing completed.
    /// </summary>
    public DateTime? ProcessingCompletedAt { get; set; }

    /// <summary>
    /// Current status of this snapshot.
    /// </summary>
    public InventorySnapshotStatus Status { get; set; } = InventorySnapshotStatus.Received;

    /// <summary>
    /// Total number of items in snapshot.
    /// </summary>
    public int TotalItemCount { get; set; }

    /// <summary>
    /// Number of items processed.
    /// </summary>
    public int ProcessedItemCount { get; set; }

    /// <summary>
    /// Number of items with errors.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Number of new items added.
    /// </summary>
    public int NewItemCount { get; set; }

    /// <summary>
    /// Number of items updated.
    /// </summary>
    public int UpdatedItemCount { get; set; }

    /// <summary>
    /// Number of items removed/zeroed out.
    /// </summary>
    public int RemovedItemCount { get; set; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether this is a full refresh (true) or incremental update (false).
    /// </summary>
    public bool IsFullRefresh { get; set; } = true;

    /// <summary>
    /// Reference to previous snapshot for incremental tracking.
    /// </summary>
    public int? PreviousSnapshotId { get; set; }

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    // Navigation
    public PartnerDocument? PartnerDocument { get; set; }
    public TradingPartner? TradingPartner { get; set; }
    public SupplierInventorySnapshot? PreviousSnapshot { get; set; }
    public ICollection<SupplierInventoryItem> Items { get; set; } = new List<SupplierInventoryItem>();
}

/// <summary>
/// Status of an inventory snapshot.
/// </summary>
public enum InventorySnapshotStatus
{
    /// <summary>Snapshot received but not processed.</summary>
    Received = 0,

    /// <summary>Snapshot is being validated.</summary>
    Validating = 10,

    /// <summary>Validation failed.</summary>
    ValidationFailed = 15,

    /// <summary>Snapshot is in staging.</summary>
    Staging = 20,

    /// <summary>Snapshot is being applied.</summary>
    Applying = 30,

    /// <summary>Snapshot applied successfully.</summary>
    Applied = 40,

    /// <summary>Snapshot application failed.</summary>
    Failed = 50,

    /// <summary>Snapshot was superseded by a newer one.</summary>
    Superseded = 60
}
