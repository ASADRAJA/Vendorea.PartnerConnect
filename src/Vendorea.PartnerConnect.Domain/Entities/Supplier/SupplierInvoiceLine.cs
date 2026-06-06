namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// Line item on a supplier invoice.
/// </summary>
public class SupplierInvoiceLine
{
    public int Id { get; set; }

    /// <summary>
    /// Parent invoice ID.
    /// </summary>
    public int SupplierInvoiceId { get; set; }

    /// <summary>
    /// Link to the original PO line if matched.
    /// </summary>
    public int? SupplierPurchaseOrderLineId { get; set; }

    /// <summary>
    /// Link to the shipment line if matched.
    /// </summary>
    public int? SupplierShipmentLineId { get; set; }

    /// <summary>
    /// Line number.
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
    /// Quantity invoiced.
    /// </summary>
    public int QuantityInvoiced { get; set; }

    /// <summary>
    /// Quantity shipped (for reference).
    /// </summary>
    public int? QuantityShipped { get; set; }

    /// <summary>
    /// Quantity ordered (for reference).
    /// </summary>
    public int? QuantityOrdered { get; set; }

    /// <summary>
    /// Unit of measure.
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
    /// Line-level tax.
    /// </summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>
    /// Net line amount.
    /// </summary>
    public decimal LineTotal { get; set; }

    /// <summary>
    /// PO line number reference.
    /// </summary>
    public int? PoLineNumber { get; set; }

    /// <summary>
    /// Notes/comments for this line.
    /// </summary>
    public string? Notes { get; set; }

    // Navigation
    public SupplierInvoice? Invoice { get; set; }
    public SupplierPurchaseOrderLine? PurchaseOrderLine { get; set; }
    public SupplierShipmentLine? ShipmentLine { get; set; }
}
