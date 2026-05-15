namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Tracks EDI-specific metadata for X12 documents.
/// Links to PartnerDocument for general document lifecycle tracking.
/// </summary>
public class EdiDocument
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the parent document tracking record.
    /// </summary>
    public int PartnerDocumentId { get; set; }

    /// <summary>
    /// EDI transaction set code (850, 855, 856, 810, 997).
    /// </summary>
    public string TransactionSetCode { get; set; } = string.Empty;

    /// <summary>
    /// ISA interchange control number.
    /// </summary>
    public string InterchangeControlNumber { get; set; } = string.Empty;

    /// <summary>
    /// GS functional group control number.
    /// </summary>
    public string GroupControlNumber { get; set; } = string.Empty;

    /// <summary>
    /// ST transaction set control number.
    /// </summary>
    public string TransactionControlNumber { get; set; } = string.Empty;

    /// <summary>
    /// ISA sender ID (trading partner identifier).
    /// </summary>
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// ISA receiver ID (our identifier).
    /// </summary>
    public string ReceiverId { get; set; } = string.Empty;

    /// <summary>
    /// Sender qualifier (ISA05).
    /// </summary>
    public string? SenderQualifier { get; set; }

    /// <summary>
    /// Receiver qualifier (ISA07).
    /// </summary>
    public string? ReceiverQualifier { get; set; }

    /// <summary>
    /// Document direction: Inbound (received) or Outbound (sent).
    /// </summary>
    public EdiDirection Direction { get; set; }

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
    /// Reference to the response document (855 for 850, 997 for any).
    /// </summary>
    public int? ResponseDocumentId { get; set; }

    /// <summary>
    /// Reference to the original document this is a response to.
    /// </summary>
    public int? OriginalDocumentId { get; set; }

    /// <summary>
    /// Whether the acknowledgment has been generated.
    /// </summary>
    public bool AcknowledgmentGenerated { get; set; }

    /// <summary>
    /// Whether the acknowledgment has been sent to the trading partner.
    /// </summary>
    public bool AcknowledgmentSent { get; set; }

    /// <summary>
    /// Timestamp when acknowledgment was sent.
    /// </summary>
    public DateTime? AcknowledgmentSentAt { get; set; }

    /// <summary>
    /// Raw EDI content for outbound documents.
    /// </summary>
    public string? RawEdiContent { get; set; }

    /// <summary>
    /// Business document reference (e.g., PO number, invoice number).
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
    /// When this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public PartnerDocument? PartnerDocument { get; set; }
    public EdiDocument? ResponseDocument { get; set; }
    public EdiDocument? OriginalDocument { get; set; }
    public ICollection<EdiDocument> Responses { get; set; } = new List<EdiDocument>();
}

/// <summary>
/// Direction of EDI document flow.
/// </summary>
public enum EdiDirection
{
    /// <summary>
    /// Document received from trading partner.
    /// </summary>
    Inbound,

    /// <summary>
    /// Document sent to trading partner.
    /// </summary>
    Outbound
}

/// <summary>
/// Type of acknowledgment to generate.
/// </summary>
public enum AcknowledgmentType
{
    /// <summary>
    /// EDI 997 Functional Acknowledgment - acknowledges receipt of any EDI document.
    /// </summary>
    Edi997,

    /// <summary>
    /// EDI 855 Purchase Order Acknowledgment - business-level response to 850.
    /// </summary>
    Edi855
}
