namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a line item in an order.
/// </summary>
public class OrderLine
{
    public int Id { get; set; }

    /// <summary>
    /// Parent order.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Line number (1-based sequence).
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// SKU/Item number.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Vendor/Partner SKU (if different from tenant's SKU).
    /// </summary>
    public string? VendorSku { get; set; }

    /// <summary>
    /// UPC barcode.
    /// </summary>
    public string? Upc { get; set; }

    /// <summary>
    /// Item description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Ordered quantity.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit of measure (EA, CS, PK, etc.).
    /// </summary>
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Unit price.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Line total (quantity * unit price).
    /// </summary>
    public decimal LineTotal { get; set; }

    /// <summary>
    /// Current line status.
    /// </summary>
    public OrderLineStatus Status { get; set; } = OrderLineStatus.Pending;

    /// <summary>
    /// Quantity acknowledged by partner.
    /// </summary>
    public decimal? AcknowledgedQuantity { get; set; }

    /// <summary>
    /// Quantity shipped so far.
    /// </summary>
    public decimal? ShippedQuantity { get; set; }

    /// <summary>
    /// Quantity on backorder.
    /// </summary>
    public decimal? BackorderedQuantity { get; set; }

    /// <summary>
    /// Partner's acknowledgment code for this line.
    /// </summary>
    public string? AcknowledgmentCode { get; set; }

    /// <summary>
    /// Partner's acknowledgment message for this line.
    /// </summary>
    public string? AcknowledgmentMessage { get; set; }

    /// <summary>
    /// Estimated ship date for this line.
    /// </summary>
    public DateTime? EstimatedShipDate { get; set; }

    /// <summary>
    /// Notes/special instructions for this line.
    /// </summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Order? Order { get; set; }
}

/// <summary>
/// Order line status values.
/// </summary>
public enum OrderLineStatus
{
    /// <summary>
    /// Pending processing.
    /// </summary>
    Pending,

    /// <summary>
    /// Acknowledged by partner.
    /// </summary>
    Acknowledged,

    /// <summary>
    /// On backorder.
    /// </summary>
    Backordered,

    /// <summary>
    /// Shipped.
    /// </summary>
    Shipped,

    /// <summary>
    /// Cancelled.
    /// </summary>
    Cancelled
}
