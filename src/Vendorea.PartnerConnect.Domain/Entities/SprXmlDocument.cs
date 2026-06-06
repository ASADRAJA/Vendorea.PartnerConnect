namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Tracks SPR XML EDI documents (EZPO4, EZPOACK, EZASNS, EZINV4).
/// Links to PartnerDocument for general document lifecycle tracking.
/// </summary>
public class SprXmlDocument
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the parent document tracking record.
    /// </summary>
    public int PartnerDocumentId { get; set; }

    /// <summary>
    /// SPR XML document type code (EZPO4, EZPOACK, EZASNS, EZINV4).
    /// </summary>
    public SprXmlDocumentType DocumentType { get; set; }

    /// <summary>
    /// Document direction: Inbound (received) or Outbound (sent).
    /// </summary>
    public EdiDirection Direction { get; set; }

    /// <summary>
    /// SPR enterprise code (buyer organization identifier).
    /// </summary>
    public string? EnterpriseCode { get; set; }

    /// <summary>
    /// SPR buyer organization code.
    /// </summary>
    public string? BuyerOrganizationCode { get; set; }

    /// <summary>
    /// SPR seller organization code.
    /// </summary>
    public string? SellerOrganizationCode { get; set; }

    /// <summary>
    /// Order number (PO number for orders, SO number for shipments).
    /// </summary>
    public string? OrderNumber { get; set; }

    /// <summary>
    /// External order reference (e.g., customer PO number).
    /// </summary>
    public string? ExternalOrderReference { get; set; }

    /// <summary>
    /// Manifest number for EZASNS documents.
    /// </summary>
    public string? ManifestNumber { get; set; }

    /// <summary>
    /// Invoice number for EZINV4 documents.
    /// </summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>
    /// Type of canonical model this document was parsed into.
    /// E.g., "PurchaseOrder", "ShipmentNotice", "SupplierInvoice"
    /// </summary>
    public string? CanonicalType { get; set; }

    /// <summary>
    /// Serialized canonical model as JSON.
    /// </summary>
    public string? CanonicalJson { get; set; }

    /// <summary>
    /// Reference to the response document (EZPOACK for EZPO4).
    /// </summary>
    public int? ResponseDocumentId { get; set; }

    /// <summary>
    /// Reference to the original document this is a response to.
    /// </summary>
    public int? OriginalDocumentId { get; set; }

    /// <summary>
    /// Whether the acknowledgment has been received.
    /// </summary>
    public bool AcknowledgmentReceived { get; set; }

    /// <summary>
    /// Timestamp when acknowledgment was received.
    /// </summary>
    public DateTime? AcknowledgmentReceivedAt { get; set; }

    /// <summary>
    /// Raw XML content.
    /// </summary>
    public string? RawXmlContent { get; set; }

    /// <summary>
    /// Business document reference (PO number, invoice number, etc.).
    /// </summary>
    public string? BusinessReference { get; set; }

    /// <summary>
    /// Number of line items in the document.
    /// </summary>
    public int? LineItemCount { get; set; }

    /// <summary>
    /// Total document value (for orders/invoices).
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// Processing errors if any.
    /// </summary>
    public string? ProcessingErrors { get; set; }

    /// <summary>
    /// Processing status for the document.
    /// </summary>
    public SprXmlProcessingStatus ProcessingStatus { get; set; } = SprXmlProcessingStatus.Pending;

    /// <summary>
    /// When this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When this document was sent (for outbound).
    /// </summary>
    public DateTime? SentAt { get; set; }

    // Navigation properties
    public PartnerDocument? PartnerDocument { get; set; }
    public SprXmlDocument? ResponseDocument { get; set; }
    public SprXmlDocument? OriginalDocument { get; set; }
    public ICollection<SprXmlDocument> Responses { get; set; } = new List<SprXmlDocument>();
}

/// <summary>
/// SPR XML document types.
/// </summary>
public enum SprXmlDocumentType
{
    /// <summary>
    /// Purchase Order - outbound to SPR.
    /// </summary>
    EZPO4,

    /// <summary>
    /// Purchase Order Acknowledgment - inbound from SPR.
    /// </summary>
    EZPOACK,

    /// <summary>
    /// Advance Ship Notice/Manifest - inbound from SPR.
    /// </summary>
    EZASNS,

    /// <summary>
    /// Invoice with embedded Credit Memos - inbound from SPR.
    /// </summary>
    EZINV4,

    /// <summary>
    /// Inventory feed - inbound from SPR.
    /// </summary>
    Inventory
}

/// <summary>
/// Processing status for SPR XML documents.
/// </summary>
public enum SprXmlProcessingStatus
{
    /// <summary>
    /// Document is pending processing.
    /// </summary>
    Pending,

    /// <summary>
    /// Document is currently being processed.
    /// </summary>
    Processing,

    /// <summary>
    /// Document was parsed successfully.
    /// </summary>
    Parsed,

    /// <summary>
    /// Document was validated successfully.
    /// </summary>
    Validated,

    /// <summary>
    /// Document processing completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Document processing failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Document is ready for transport via SFTP/file transfer (outbound only).
    /// </summary>
    ReadyForTransport,

    /// <summary>
    /// Document was sent to SPR (outbound only).
    /// </summary>
    Sent,

    /// <summary>
    /// Document acknowledgment received (outbound only).
    /// </summary>
    Acknowledged
}
