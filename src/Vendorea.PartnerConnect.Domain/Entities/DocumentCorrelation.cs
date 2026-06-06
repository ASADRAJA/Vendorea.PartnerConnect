namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Tracks relationships between documents (PO → ACK → ASN → INV).
/// Enables document chain tracking and business process visibility.
/// </summary>
public class DocumentCorrelation
{
    public int Id { get; set; }

    /// <summary>
    /// The source/parent document in the relationship.
    /// </summary>
    public int SourceDocumentId { get; set; }

    /// <summary>
    /// The target/child document in the relationship.
    /// </summary>
    public int TargetDocumentId { get; set; }

    /// <summary>
    /// Type of correlation.
    /// </summary>
    public CorrelationType CorrelationType { get; set; }

    /// <summary>
    /// Business reference used for correlation (PO number, invoice number, etc.).
    /// </summary>
    public string? BusinessReference { get; set; }

    /// <summary>
    /// Confidence of the correlation (0.0 - 1.0).
    /// </summary>
    public decimal Confidence { get; set; } = 1.0m;

    /// <summary>
    /// How the correlation was established.
    /// </summary>
    public CorrelationMethod Method { get; set; }

    /// <summary>
    /// Whether this correlation has been verified/confirmed.
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// When this correlation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Notes about this correlation.
    /// </summary>
    public string? Notes { get; set; }

    // Navigation
    public PartnerDocument? SourceDocument { get; set; }
    public PartnerDocument? TargetDocument { get; set; }
}

/// <summary>
/// Type of document correlation.
/// </summary>
public enum CorrelationType
{
    /// <summary>Purchase order to acknowledgment.</summary>
    OrderToAcknowledgment = 0,

    /// <summary>Purchase order to shipment.</summary>
    OrderToShipment = 10,

    /// <summary>Purchase order to invoice.</summary>
    OrderToInvoice = 20,

    /// <summary>Shipment to invoice.</summary>
    ShipmentToInvoice = 30,

    /// <summary>Invoice to credit memo.</summary>
    InvoiceToCreditMemo = 40,

    /// <summary>Document to functional acknowledgment (997).</summary>
    DocumentToAck997 = 50,

    /// <summary>Document to response (855, etc.).</summary>
    DocumentToResponse = 60,

    /// <summary>Update/replacement relationship.</summary>
    UpdateOf = 70,

    /// <summary>Cancellation relationship.</summary>
    CancellationOf = 80
}

/// <summary>
/// How a correlation was established.
/// </summary>
public enum CorrelationMethod
{
    /// <summary>Correlated automatically by PO number match.</summary>
    AutomaticPoNumber = 0,

    /// <summary>Correlated automatically by invoice number match.</summary>
    AutomaticInvoiceNumber = 10,

    /// <summary>Correlated automatically by control number match.</summary>
    AutomaticControlNumber = 20,

    /// <summary>Correlated automatically by SKU/line matching.</summary>
    AutomaticLineMatch = 30,

    /// <summary>Correlated manually by user.</summary>
    Manual = 50,

    /// <summary>Correlation inferred from document content.</summary>
    Inferred = 60
}
