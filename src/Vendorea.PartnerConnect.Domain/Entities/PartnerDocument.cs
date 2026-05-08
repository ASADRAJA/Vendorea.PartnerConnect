namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a trading document exchanged with a partner (price list, inventory feed, PO, invoice, etc.).
/// Tracks document lifecycle from receipt through processing.
/// </summary>
public class PartnerDocument
{
    public int Id { get; set; }
    public int DealerPartnerConnectionId { get; set; }
    public DocumentType DocumentType { get; set; }
    public DocumentDirection Direction { get; set; }
    public DocumentStatus Status { get; set; }
    public string? ExternalReference { get; set; }
    public string? FileName { get; set; }
    public string? StoragePath { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? ContentHash { get; set; }
    public int? RecordCount { get; set; }
    public int? ProcessedCount { get; set; }
    public int? ErrorCount { get; set; }
    public string? ErrorDetails { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }

    public DealerPartnerConnection? DealerPartnerConnection { get; set; }
}

public enum DocumentType
{
    PriceList,
    InventoryFeed,
    ProductCatalog,
    PurchaseOrder,
    PurchaseOrderAcknowledgment,
    AdvanceShipNotice,
    Invoice,
    CreditMemo,
    ReturnAuthorization
}

public enum DocumentDirection
{
    Inbound,
    Outbound
}

public enum DocumentStatus
{
    Received,
    Queued,
    Processing,
    Completed,
    PartiallyCompleted,
    Failed,
    Cancelled
}
