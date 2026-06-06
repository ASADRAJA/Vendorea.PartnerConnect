namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// Line item on a supplier purchase order.
/// </summary>
public class SupplierPurchaseOrderLine
{
    public int Id { get; set; }

    /// <summary>
    /// Parent order ID.
    /// </summary>
    public int SupplierPurchaseOrderId { get; set; }

    /// <summary>
    /// Line number (1-based).
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Supplier's SKU/item ID.
    /// </summary>
    public string SupplierSku { get; set; } = string.Empty;

    /// <summary>
    /// Customer's SKU reference.
    /// </summary>
    public string? CustomerSku { get; set; }

    /// <summary>
    /// UPC/EAN code.
    /// </summary>
    public string? Upc { get; set; }

    /// <summary>
    /// Manufacturer part number.
    /// </summary>
    public string? ManufacturerPartNumber { get; set; }

    /// <summary>
    /// Item description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Quantity ordered.
    /// </summary>
    public int QuantityOrdered { get; set; }

    /// <summary>
    /// Quantity acknowledged by supplier.
    /// </summary>
    public int? QuantityAcknowledged { get; set; }

    /// <summary>
    /// Quantity shipped so far.
    /// </summary>
    public int? QuantityShipped { get; set; }

    /// <summary>
    /// Quantity backordered.
    /// </summary>
    public int? QuantityBackordered { get; set; }

    /// <summary>
    /// Quantity cancelled.
    /// </summary>
    public int? QuantityCancelled { get; set; }

    /// <summary>
    /// Unit of measure (EA, CS, etc.).
    /// </summary>
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Unit price.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Extended price (quantity * unit price).
    /// </summary>
    public decimal ExtendedPrice { get; set; }

    /// <summary>
    /// Line-level discount.
    /// </summary>
    public decimal? DiscountAmount { get; set; }

    /// <summary>
    /// Requested delivery date for this line.
    /// </summary>
    public DateTime? RequestedDeliveryDate { get; set; }

    /// <summary>
    /// Expected ship date from supplier.
    /// </summary>
    public DateTime? ExpectedShipDate { get; set; }

    /// <summary>
    /// Line status.
    /// </summary>
    public SupplierOrderLineStatus Status { get; set; } = SupplierOrderLineStatus.Pending;

    /// <summary>
    /// Status reason/notes from supplier.
    /// </summary>
    public string? StatusReason { get; set; }

    // Navigation
    public SupplierPurchaseOrder? Order { get; set; }
}

/// <summary>
/// Status of a supplier order line.
/// </summary>
public enum SupplierOrderLineStatus
{
    Pending = 0,
    Acknowledged = 10,
    Backordered = 20,
    Substituted = 30,
    PartiallyShipped = 40,
    Shipped = 50,
    Delivered = 60,
    Cancelled = 70,
    Rejected = 80
}
