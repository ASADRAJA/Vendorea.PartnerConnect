namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Records that a shipment manifest (EZASNS) has been applied to an order, so re-ingesting the
/// same manifest does not double-count shipped quantities. Keyed by (OrderId, ManifestId).
/// </summary>
public class OrderAppliedShipment
{
    public int Id { get; set; }

    /// <summary>
    /// Order the shipment manifest was applied to.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Partner manifest/shipment id (ShipmentNotice.ShipmentId).
    /// </summary>
    public string ManifestId { get; set; } = string.Empty;

    /// <summary>
    /// When the manifest was applied.
    /// </summary>
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Order? Order { get; set; }
}
