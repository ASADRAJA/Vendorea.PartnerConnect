namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents an order submitted through PartnerConnect.
/// Orders are placed by tenants and processed via EDI with trading partners.
/// </summary>
public class Order
{
    public int Id { get; set; }

    /// <summary>
    /// Organization for billing purposes.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tenant that placed the order.
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Trading partner the order is sent to.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// Tenant's account with the partner.
    /// </summary>
    public int TenantPartnerAccountId { get; set; }

    /// <summary>
    /// Purchase order number (provided by tenant).
    /// </summary>
    public string PoNumber { get; set; } = string.Empty;

    /// <summary>
    /// Current order status.
    /// </summary>
    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    /// <summary>
    /// When the order was placed.
    /// </summary>
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Requested ship date.
    /// </summary>
    public DateTime? RequestedShipDate { get; set; }

    /// <summary>
    /// Requested delivery date.
    /// </summary>
    public DateTime? RequestedDeliveryDate { get; set; }

    /// <summary>
    /// Ship-to address as JSON.
    /// </summary>
    public string? ShipToJson { get; set; }

    /// <summary>
    /// Bill-to address as JSON.
    /// </summary>
    public string? BillToJson { get; set; }

    /// <summary>
    /// Shipping method code.
    /// </summary>
    public string? ShippingMethod { get; set; }

    /// <summary>
    /// Order notes/special instructions.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Subtotal (sum of line totals).
    /// </summary>
    public decimal SubTotal { get; set; }

    /// <summary>
    /// Tax amount.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Shipping/handling amount.
    /// </summary>
    public decimal ShippingAmount { get; set; }

    /// <summary>
    /// Total amount (subtotal + tax + shipping).
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Currency code (ISO 4217).
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Reference to generated EDI 850 document.
    /// </summary>
    public int? EdiDocumentId { get; set; }

    /// <summary>
    /// Reference to received EDI 855 acknowledgment.
    /// </summary>
    public int? AcknowledgmentDocumentId { get; set; }

    /// <summary>
    /// Partner's order/acknowledgment number.
    /// </summary>
    public string? PartnerOrderNumber { get; set; }

    /// <summary>
    /// When the order was submitted to the partner.
    /// </summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>
    /// When acknowledgment was received.
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// When the order was shipped.
    /// </summary>
    public DateTime? ShippedAt { get; set; }

    /// <summary>
    /// When the order was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// When the order was cancelled.
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Reason for cancellation.
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Error message if status is Failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Organization? Organization { get; set; }
    public Tenant? Tenant { get; set; }
    public TradingPartner? TradingPartner { get; set; }
    public TenantPartnerAccount? TenantPartnerAccount { get; set; }
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}

/// <summary>
/// Order status values.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order is being prepared, not yet submitted.
    /// </summary>
    Draft,

    /// <summary>
    /// Order submitted and pending processing.
    /// </summary>
    Submitted,

    /// <summary>
    /// Order acknowledged by trading partner.
    /// </summary>
    Acknowledged,

    /// <summary>
    /// Order is being processed by the partner.
    /// </summary>
    Processing,

    /// <summary>
    /// Order partially shipped.
    /// </summary>
    PartiallyShipped,

    /// <summary>
    /// Order fully shipped.
    /// </summary>
    Shipped,

    /// <summary>
    /// Order delivered.
    /// </summary>
    Delivered,

    /// <summary>
    /// Order completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Order cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Order failed to process.
    /// </summary>
    Failed
}
