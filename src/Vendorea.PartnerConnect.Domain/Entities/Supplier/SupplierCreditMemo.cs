namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// Credit memo received from a supplier.
/// Represents a credit/refund for returned goods or billing adjustments.
/// </summary>
public class SupplierCreditMemo
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the PartnerDocument containing this credit memo.
    /// </summary>
    public int? PartnerDocumentId { get; set; }

    /// <summary>
    /// Trading partner that sent this credit memo.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// The tenant this credit memo is for.
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Link to the original invoice being credited.
    /// </summary>
    public int? SupplierInvoiceId { get; set; }

    /// <summary>
    /// Link to the original purchase order.
    /// </summary>
    public int? SupplierPurchaseOrderId { get; set; }

    /// <summary>
    /// Credit memo number.
    /// </summary>
    public string CreditMemoNumber { get; set; } = string.Empty;

    /// <summary>
    /// Original invoice number being credited.
    /// </summary>
    public string? OriginalInvoiceNumber { get; set; }

    /// <summary>
    /// Customer's PO number.
    /// </summary>
    public string? PoNumber { get; set; }

    /// <summary>
    /// Credit memo date.
    /// </summary>
    public DateTime CreditMemoDate { get; set; }

    /// <summary>
    /// Reason for credit.
    /// </summary>
    public CreditMemoReason Reason { get; set; }

    /// <summary>
    /// Detailed reason description.
    /// </summary>
    public string? ReasonDescription { get; set; }

    /// <summary>
    /// Credit memo status.
    /// </summary>
    public CreditMemoStatus Status { get; set; } = CreditMemoStatus.Received;

    /// <summary>
    /// Currency code.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Subtotal credit amount.
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Tax credit amount.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Shipping credit amount.
    /// </summary>
    public decimal ShippingAmount { get; set; }

    /// <summary>
    /// Total credit amount.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// RMA/return authorization number.
    /// </summary>
    public string? RmaNumber { get; set; }

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
    /// When this credit memo was received.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PartnerDocument? PartnerDocument { get; set; }
    public TradingPartner? TradingPartner { get; set; }
    public Tenant? Tenant { get; set; }
    public SupplierInvoice? OriginalInvoice { get; set; }
    public SupplierPurchaseOrder? PurchaseOrder { get; set; }
    public ICollection<SupplierCreditMemoLine> Lines { get; set; } = new List<SupplierCreditMemoLine>();
}

/// <summary>
/// Reason for credit memo.
/// </summary>
public enum CreditMemoReason
{
    /// <summary>Returned merchandise.</summary>
    Return = 0,

    /// <summary>Pricing adjustment.</summary>
    PriceAdjustment = 10,

    /// <summary>Damaged goods.</summary>
    Damaged = 20,

    /// <summary>Defective goods.</summary>
    Defective = 30,

    /// <summary>Shipping error.</summary>
    ShippingError = 40,

    /// <summary>Duplicate billing.</summary>
    DuplicateBilling = 50,

    /// <summary>Cancelled order.</summary>
    Cancellation = 60,

    /// <summary>Promotional credit.</summary>
    Promotional = 70,

    /// <summary>Other reason.</summary>
    Other = 99
}

/// <summary>
/// Credit memo status.
/// </summary>
public enum CreditMemoStatus
{
    /// <summary>Credit memo received.</summary>
    Received = 0,

    /// <summary>Credit memo validated.</summary>
    Validated = 10,

    /// <summary>Credit memo approved.</summary>
    Approved = 20,

    /// <summary>Credit applied to account.</summary>
    Applied = 30,

    /// <summary>Credit refunded.</summary>
    Refunded = 40,

    /// <summary>Credit memo disputed.</summary>
    Disputed = 50,

    /// <summary>Credit memo voided.</summary>
    Voided = 60
}
