namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// Canonical representation of a purchase order sent to a supplier.
/// Partner-agnostic - SPR, Ingram, etc. all map to this.
/// </summary>
public class SupplierPurchaseOrder
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the PartnerDocument tracking this order.
    /// </summary>
    public int? PartnerDocumentId { get; set; }

    /// <summary>
    /// The trading partner this order was sent to.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// The tenant placing this order.
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Customer's purchase order number.
    /// </summary>
    public string PoNumber { get; set; } = string.Empty;

    /// <summary>
    /// Supplier's order number (assigned after acknowledgment).
    /// </summary>
    public string? SupplierOrderNumber { get; set; }

    /// <summary>
    /// Customer account number at the supplier.
    /// </summary>
    public string? CustomerAccountNumber { get; set; }

    /// <summary>
    /// Date the order was placed.
    /// </summary>
    public DateTime OrderDate { get; set; }

    /// <summary>
    /// Requested delivery date.
    /// </summary>
    public DateTime? RequestedDeliveryDate { get; set; }

    /// <summary>
    /// Requested ship date.
    /// </summary>
    public DateTime? RequestedShipDate { get; set; }

    /// <summary>
    /// Order status.
    /// </summary>
    public SupplierOrderStatus Status { get; set; } = SupplierOrderStatus.Draft;

    /// <summary>
    /// Ship-to name/company.
    /// </summary>
    public string? ShipToName { get; set; }
    public string? ShipToAddress1 { get; set; }
    public string? ShipToAddress2 { get; set; }
    public string? ShipToCity { get; set; }
    public string? ShipToState { get; set; }
    public string? ShipToPostalCode { get; set; }
    public string? ShipToCountry { get; set; }
    public string? ShipToPhone { get; set; }
    public string? ShipToEmail { get; set; }

    /// <summary>
    /// Bill-to name/company.
    /// </summary>
    public string? BillToName { get; set; }
    public string? BillToAddress1 { get; set; }
    public string? BillToAddress2 { get; set; }
    public string? BillToCity { get; set; }
    public string? BillToState { get; set; }
    public string? BillToPostalCode { get; set; }
    public string? BillToCountry { get; set; }

    /// <summary>
    /// Shipping method code.
    /// </summary>
    public string? ShippingMethod { get; set; }

    /// <summary>
    /// Carrier SCAC code.
    /// </summary>
    public string? CarrierCode { get; set; }

    /// <summary>
    /// Currency code (USD, CAD, etc.).
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Order subtotal before tax/shipping.
    /// </summary>
    public decimal? Subtotal { get; set; }

    /// <summary>
    /// Tax amount.
    /// </summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>
    /// Shipping/freight amount.
    /// </summary>
    public decimal? ShippingAmount { get; set; }

    /// <summary>
    /// Total order amount.
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// Number of line items.
    /// </summary>
    public int LineCount { get; set; }

    /// <summary>
    /// Special instructions or notes.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Correlation ID for tracking through the system.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// When this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When the order was submitted to the supplier.
    /// </summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>
    /// When the acknowledgment was received.
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    // Navigation properties
    public PartnerDocument? PartnerDocument { get; set; }
    public TradingPartner? TradingPartner { get; set; }
    public Tenant? Tenant { get; set; }
    public ICollection<SupplierPurchaseOrderLine> Lines { get; set; } = new List<SupplierPurchaseOrderLine>();
    public ICollection<SupplierOrderAcknowledgement> Acknowledgements { get; set; } = new List<SupplierOrderAcknowledgement>();
}

/// <summary>
/// Status of a supplier purchase order.
/// </summary>
public enum SupplierOrderStatus
{
    /// <summary>Order is being prepared.</summary>
    Draft = 0,

    /// <summary>Order is ready to send.</summary>
    Pending = 10,

    /// <summary>Order has been submitted to supplier.</summary>
    Submitted = 20,

    /// <summary>Order has been acknowledged by supplier.</summary>
    Acknowledged = 30,

    /// <summary>Order is partially shipped.</summary>
    PartiallyShipped = 40,

    /// <summary>Order has been fully shipped.</summary>
    Shipped = 50,

    /// <summary>Order has been delivered.</summary>
    Delivered = 60,

    /// <summary>Order is complete.</summary>
    Completed = 70,

    /// <summary>Order was cancelled.</summary>
    Cancelled = 80,

    /// <summary>Order failed to process.</summary>
    Failed = 90
}
