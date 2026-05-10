using Vendorea.PartnerConnect.Canonical.Enums;

namespace Vendorea.PartnerConnect.Canonical.Models;

/// <summary>
/// Canonical shipment notice representing an advance ship notice from a trading partner.
/// Maps to EDI 856.
/// </summary>
public record ShipmentNotice
{
    /// <summary>
    /// Unique correlation ID for tracking.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The dealer ID receiving this shipment.
    /// </summary>
    public int DealerId { get; init; }

    /// <summary>
    /// The trading partner code shipping the order.
    /// </summary>
    public string TradingPartnerCode { get; init; } = string.Empty;

    /// <summary>
    /// Partner's shipment ID.
    /// </summary>
    public string ShipmentId { get; init; } = string.Empty;

    /// <summary>
    /// Reference to the original purchase order number.
    /// </summary>
    public string? PoNumber { get; init; }

    /// <summary>
    /// Partner's order reference.
    /// </summary>
    public string? PartnerOrderReference { get; init; }

    /// <summary>
    /// Date shipped.
    /// </summary>
    public DateTime ShipDate { get; init; }

    /// <summary>
    /// Expected delivery date.
    /// </summary>
    public DateTime? ExpectedDeliveryDate { get; init; }

    /// <summary>
    /// Actual delivery date (if delivered).
    /// </summary>
    public DateTime? ActualDeliveryDate { get; init; }

    /// <summary>
    /// Carrier name.
    /// </summary>
    public string? CarrierName { get; init; }

    /// <summary>
    /// Carrier SCAC code.
    /// </summary>
    public string? CarrierScac { get; init; }

    /// <summary>
    /// Tracking number.
    /// </summary>
    public string? TrackingNumber { get; init; }

    /// <summary>
    /// Additional tracking numbers if multiple packages.
    /// </summary>
    public IReadOnlyList<string>? AdditionalTrackingNumbers { get; init; }

    /// <summary>
    /// Service level (e.g., Ground, Express, Overnight).
    /// </summary>
    public string? ServiceLevel { get; init; }

    /// <summary>
    /// Ship-from address.
    /// </summary>
    public Address? ShipFrom { get; init; }

    /// <summary>
    /// Ship-to address.
    /// </summary>
    public Address? ShipTo { get; init; }

    /// <summary>
    /// Shipment line items.
    /// </summary>
    public IReadOnlyList<ShipmentLine> Lines { get; init; } = Array.Empty<ShipmentLine>();

    /// <summary>
    /// Total number of packages.
    /// </summary>
    public int? PackageCount { get; init; }

    /// <summary>
    /// Total weight.
    /// </summary>
    public decimal? TotalWeight { get; init; }

    /// <summary>
    /// Weight unit (LB, KG).
    /// </summary>
    public string? WeightUnit { get; init; } = "LB";

    /// <summary>
    /// Current shipment status.
    /// </summary>
    public ShipmentStatus Status { get; init; } = ShipmentStatus.Pending;

    /// <summary>
    /// Reference to the source document ID.
    /// </summary>
    public string? SourceDocumentId { get; init; }

    /// <summary>
    /// When this record was received.
    /// </summary>
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Shipment line item.
/// </summary>
public record ShipmentLine
{
    /// <summary>
    /// Line number (1-based).
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Reference to the PO line number.
    /// </summary>
    public int? PoLineNumber { get; init; }

    /// <summary>
    /// Partner's SKU.
    /// </summary>
    public string PartnerSku { get; init; } = string.Empty;

    /// <summary>
    /// UPC/EAN code.
    /// </summary>
    public string? Upc { get; init; }

    /// <summary>
    /// Product description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Quantity shipped.
    /// </summary>
    public int QuantityShipped { get; init; }

    /// <summary>
    /// Quantity originally ordered.
    /// </summary>
    public int? QuantityOrdered { get; init; }

    /// <summary>
    /// Unit of measure.
    /// </summary>
    public UnitOfMeasure UnitOfMeasure { get; init; } = UnitOfMeasure.Each;

    /// <summary>
    /// Lot number if applicable.
    /// </summary>
    public string? LotNumber { get; init; }

    /// <summary>
    /// Serial numbers if applicable.
    /// </summary>
    public IReadOnlyList<string>? SerialNumbers { get; init; }

    /// <summary>
    /// Expiration date if applicable.
    /// </summary>
    public DateTime? ExpirationDate { get; init; }
}
