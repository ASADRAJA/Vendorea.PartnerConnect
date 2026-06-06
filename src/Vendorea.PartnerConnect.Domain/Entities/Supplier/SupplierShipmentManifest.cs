namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// Shipping manifest (ASN) from a supplier.
/// May contain shipments for multiple orders.
/// </summary>
public class SupplierShipmentManifest
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the PartnerDocument containing this manifest.
    /// </summary>
    public int? PartnerDocumentId { get; set; }

    /// <summary>
    /// Trading partner that sent this manifest.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// Supplier's shipment/manifest number.
    /// </summary>
    public string ManifestNumber { get; set; } = string.Empty;

    /// <summary>
    /// Bill of lading number.
    /// </summary>
    public string? BillOfLading { get; set; }

    /// <summary>
    /// Date the shipment was shipped.
    /// </summary>
    public DateTime ShipDate { get; set; }

    /// <summary>
    /// Expected delivery date.
    /// </summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>
    /// Actual delivery date (updated when delivered).
    /// </summary>
    public DateTime? ActualDeliveryDate { get; set; }

    /// <summary>
    /// Carrier SCAC code.
    /// </summary>
    public string? CarrierCode { get; set; }

    /// <summary>
    /// Carrier name.
    /// </summary>
    public string? CarrierName { get; set; }

    /// <summary>
    /// Shipping method/service level.
    /// </summary>
    public string? ShippingMethod { get; set; }

    /// <summary>
    /// Master tracking number.
    /// </summary>
    public string? TrackingNumber { get; set; }

    /// <summary>
    /// Ship-from warehouse/location code.
    /// </summary>
    public string? ShipFromLocationCode { get; set; }

    /// <summary>
    /// Ship-from name.
    /// </summary>
    public string? ShipFromName { get; set; }
    public string? ShipFromAddress1 { get; set; }
    public string? ShipFromAddress2 { get; set; }
    public string? ShipFromCity { get; set; }
    public string? ShipFromState { get; set; }
    public string? ShipFromPostalCode { get; set; }
    public string? ShipFromCountry { get; set; }

    /// <summary>
    /// Total number of cartons/packages.
    /// </summary>
    public int TotalCartons { get; set; }

    /// <summary>
    /// Total weight.
    /// </summary>
    public decimal? TotalWeight { get; set; }

    /// <summary>
    /// Weight unit of measure (LB, KG).
    /// </summary>
    public string? WeightUom { get; set; }

    /// <summary>
    /// Total number of orders in this manifest.
    /// </summary>
    public int OrderCount { get; set; }

    /// <summary>
    /// Total number of line items across all orders.
    /// </summary>
    public int TotalLineCount { get; set; }

    /// <summary>
    /// Shipment status.
    /// </summary>
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Shipped;

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// When this manifest was received.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PartnerDocument? PartnerDocument { get; set; }
    public TradingPartner? TradingPartner { get; set; }
    public ICollection<SupplierShipmentOrder> Orders { get; set; } = new List<SupplierShipmentOrder>();
    public ICollection<SupplierCarton> Cartons { get; set; } = new List<SupplierCarton>();
}

/// <summary>
/// Status of a shipment.
/// </summary>
public enum ShipmentStatus
{
    /// <summary>Shipment is pending pickup.</summary>
    Pending = 0,

    /// <summary>Shipment has been picked up/shipped.</summary>
    Shipped = 10,

    /// <summary>Shipment is in transit.</summary>
    InTransit = 20,

    /// <summary>Shipment is out for delivery.</summary>
    OutForDelivery = 30,

    /// <summary>Shipment has been delivered.</summary>
    Delivered = 40,

    /// <summary>Delivery exception occurred.</summary>
    Exception = 50,

    /// <summary>Shipment was returned.</summary>
    Returned = 60
}
