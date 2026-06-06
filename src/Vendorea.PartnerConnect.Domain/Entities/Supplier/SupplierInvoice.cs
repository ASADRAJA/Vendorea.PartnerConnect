namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// Invoice received from a supplier.
/// </summary>
public class SupplierInvoice
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the PartnerDocument containing this invoice.
    /// </summary>
    public int? PartnerDocumentId { get; set; }

    /// <summary>
    /// Trading partner that sent this invoice.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// The tenant this invoice is for.
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Link to the original purchase order if matched.
    /// </summary>
    public int? SupplierPurchaseOrderId { get; set; }

    /// <summary>
    /// Link to the shipment if matched.
    /// </summary>
    public int? SupplierShipmentManifestId { get; set; }

    /// <summary>
    /// Supplier's invoice number.
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Customer's PO number.
    /// </summary>
    public string? PoNumber { get; set; }

    /// <summary>
    /// Supplier's order number.
    /// </summary>
    public string? SupplierOrderNumber { get; set; }

    /// <summary>
    /// Invoice date.
    /// </summary>
    public DateTime InvoiceDate { get; set; }

    /// <summary>
    /// Payment due date.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Ship date referenced on invoice.
    /// </summary>
    public DateTime? ShipDate { get; set; }

    /// <summary>
    /// Invoice status.
    /// </summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Received;

    /// <summary>
    /// Invoice type.
    /// </summary>
    public InvoiceType Type { get; set; } = InvoiceType.Standard;

    /// <summary>
    /// Currency code.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Subtotal before tax and charges.
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Tax amount.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Shipping/freight amount.
    /// </summary>
    public decimal ShippingAmount { get; set; }

    /// <summary>
    /// Handling/fees amount.
    /// </summary>
    public decimal HandlingAmount { get; set; }

    /// <summary>
    /// Discount amount.
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// Total invoice amount.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Amount already paid.
    /// </summary>
    public decimal? AmountPaid { get; set; }

    /// <summary>
    /// Balance due.
    /// </summary>
    public decimal? BalanceDue { get; set; }

    /// <summary>
    /// Payment terms code.
    /// </summary>
    public string? PaymentTerms { get; set; }

    /// <summary>
    /// Payment terms description.
    /// </summary>
    public string? PaymentTermsDescription { get; set; }

    /// <summary>
    /// Early payment discount percent.
    /// </summary>
    public decimal? EarlyPaymentDiscountPercent { get; set; }

    /// <summary>
    /// Early payment discount due date.
    /// </summary>
    public DateTime? EarlyPaymentDiscountDate { get; set; }

    /// <summary>
    /// Remit-to name.
    /// </summary>
    public string? RemitToName { get; set; }
    public string? RemitToAddress1 { get; set; }
    public string? RemitToAddress2 { get; set; }
    public string? RemitToCity { get; set; }
    public string? RemitToState { get; set; }
    public string? RemitToPostalCode { get; set; }
    public string? RemitToCountry { get; set; }

    /// <summary>
    /// Number of line items.
    /// </summary>
    public int LineCount { get; set; }

    /// <summary>
    /// Notes/comments.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// When this invoice was received.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PartnerDocument? PartnerDocument { get; set; }
    public TradingPartner? TradingPartner { get; set; }
    public Tenant? Tenant { get; set; }
    public SupplierPurchaseOrder? PurchaseOrder { get; set; }
    public SupplierShipmentManifest? ShipmentManifest { get; set; }
    public ICollection<SupplierInvoiceLine> Lines { get; set; } = new List<SupplierInvoiceLine>();
}

/// <summary>
/// Invoice status.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Invoice received but not processed.</summary>
    Received = 0,

    /// <summary>Invoice is being validated.</summary>
    Validating = 10,

    /// <summary>Invoice validated successfully.</summary>
    Validated = 20,

    /// <summary>Invoice has discrepancies.</summary>
    Discrepancy = 30,

    /// <summary>Invoice approved for payment.</summary>
    Approved = 40,

    /// <summary>Invoice paid partially.</summary>
    PartiallyPaid = 50,

    /// <summary>Invoice paid in full.</summary>
    Paid = 60,

    /// <summary>Invoice disputed.</summary>
    Disputed = 70,

    /// <summary>Invoice voided.</summary>
    Voided = 80
}

/// <summary>
/// Type of invoice.
/// </summary>
public enum InvoiceType
{
    /// <summary>Standard invoice.</summary>
    Standard = 0,

    /// <summary>Credit memo/credit invoice.</summary>
    Credit = 10,

    /// <summary>Debit memo.</summary>
    Debit = 20,

    /// <summary>Pro forma invoice.</summary>
    ProForma = 30,

    /// <summary>Corrected invoice.</summary>
    Correction = 40
}
