namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// An order within a shipment manifest.
/// A manifest may contain multiple orders being shipped together.
/// </summary>
public class SupplierShipmentOrder
{
    public int Id { get; set; }

    /// <summary>
    /// Parent manifest ID.
    /// </summary>
    public int SupplierShipmentManifestId { get; set; }

    /// <summary>
    /// Link to the original purchase order if matched.
    /// </summary>
    public int? SupplierPurchaseOrderId { get; set; }

    /// <summary>
    /// Customer's PO number.
    /// </summary>
    public string PoNumber { get; set; } = string.Empty;

    /// <summary>
    /// Supplier's order number.
    /// </summary>
    public string? SupplierOrderNumber { get; set; }

    /// <summary>
    /// Ship-to name.
    /// </summary>
    public string? ShipToName { get; set; }
    public string? ShipToAddress1 { get; set; }
    public string? ShipToAddress2 { get; set; }
    public string? ShipToCity { get; set; }
    public string? ShipToState { get; set; }
    public string? ShipToPostalCode { get; set; }
    public string? ShipToCountry { get; set; }

    /// <summary>
    /// Number of line items for this order.
    /// </summary>
    public int LineCount { get; set; }

    /// <summary>
    /// Total quantity shipped for this order.
    /// </summary>
    public int TotalQuantityShipped { get; set; }

    /// <summary>
    /// Whether this order is complete (all lines shipped).
    /// </summary>
    public bool IsComplete { get; set; }

    // Navigation
    public SupplierShipmentManifest? Manifest { get; set; }
    public SupplierPurchaseOrder? PurchaseOrder { get; set; }
    public ICollection<SupplierShipmentLine> Lines { get; set; } = new List<SupplierShipmentLine>();
}
