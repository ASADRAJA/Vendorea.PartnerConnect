namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// Line item in a shipment order.
/// </summary>
public class SupplierShipmentLine
{
    public int Id { get; set; }

    /// <summary>
    /// Parent shipment order ID.
    /// </summary>
    public int SupplierShipmentOrderId { get; set; }

    /// <summary>
    /// Link to the original PO line if matched.
    /// </summary>
    public int? SupplierPurchaseOrderLineId { get; set; }

    /// <summary>
    /// Line number from the original order.
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
    /// Quantity shipped.
    /// </summary>
    public int QuantityShipped { get; set; }

    /// <summary>
    /// Quantity originally ordered.
    /// </summary>
    public int? QuantityOrdered { get; set; }

    /// <summary>
    /// Quantity remaining on backorder.
    /// </summary>
    public int? QuantityBackordered { get; set; }

    /// <summary>
    /// Unit of measure.
    /// </summary>
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Unit price.
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// Lot number if applicable.
    /// </summary>
    public string? LotNumber { get; set; }

    /// <summary>
    /// Serial numbers (comma-separated if multiple).
    /// </summary>
    public string? SerialNumbers { get; set; }

    /// <summary>
    /// Expiration date if applicable.
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    // Navigation
    public SupplierShipmentOrder? ShipmentOrder { get; set; }
    public SupplierPurchaseOrderLine? PurchaseOrderLine { get; set; }
    public ICollection<SupplierCartonItem> CartonItems { get; set; } = new List<SupplierCartonItem>();
}
