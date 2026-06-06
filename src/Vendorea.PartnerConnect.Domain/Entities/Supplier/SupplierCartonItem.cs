namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// An item packed within a carton.
/// Links cartons to shipment lines.
/// </summary>
public class SupplierCartonItem
{
    public int Id { get; set; }

    /// <summary>
    /// Parent carton ID.
    /// </summary>
    public int SupplierCartonId { get; set; }

    /// <summary>
    /// Link to the shipment line.
    /// </summary>
    public int SupplierShipmentLineId { get; set; }

    /// <summary>
    /// Quantity of this item in this carton.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Supplier SKU.
    /// </summary>
    public string SupplierSku { get; set; } = string.Empty;

    /// <summary>
    /// UPC/EAN code.
    /// </summary>
    public string? Upc { get; set; }

    /// <summary>
    /// Lot number if applicable.
    /// </summary>
    public string? LotNumber { get; set; }

    /// <summary>
    /// Serial number if applicable.
    /// </summary>
    public string? SerialNumber { get; set; }

    // Navigation
    public SupplierCarton? Carton { get; set; }
    public SupplierShipmentLine? ShipmentLine { get; set; }
}
