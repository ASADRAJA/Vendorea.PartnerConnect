namespace Vendorea.PartnerConnect.Canonical.Enums;

/// <summary>
/// Status of a canonical document during processing.
/// </summary>
public enum CanonicalStatus
{
    Pending,
    Validated,
    Transformed,
    Delivered,
    Failed,
    Skipped
}

/// <summary>
/// Source of the update (which system originated the data).
/// </summary>
public enum UpdateSource
{
    Partner,
    Manual,
    Import,
    Api
}

/// <summary>
/// Currency codes supported by the platform.
/// </summary>
public enum CurrencyCode
{
    USD,
    CAD,
    EUR,
    GBP,
    MXN
}

/// <summary>
/// Units of measure for inventory and pricing.
/// </summary>
public enum UnitOfMeasure
{
    Each,
    Case,
    Pack,
    Box,
    Dozen,
    Piece,
    Pound,
    Kilogram,
    Gallon,
    Liter,
    Pallet,
    Roll
}

/// <summary>
/// Availability status for inventory items.
/// </summary>
public enum AvailabilityStatus
{
    InStock,
    LowStock,
    OutOfStock,
    Backordered,
    Discontinued,
    PreOrder
}

/// <summary>
/// Order status for purchase orders.
/// </summary>
public enum OrderStatus
{
    Draft,
    Submitted,
    Acknowledged,
    PartiallyShipped,
    Shipped,
    Delivered,
    Cancelled,
    OnHold
}

/// <summary>
/// Shipment status for advance ship notices.
/// </summary>
public enum ShipmentStatus
{
    Pending,
    InTransit,
    OutForDelivery,
    Delivered,
    Exception,
    Returned
}

/// <summary>
/// Invoice status for supplier invoices.
/// </summary>
public enum InvoiceStatus
{
    Received,
    Validated,
    Matched,
    Disputed,
    Approved,
    Paid,
    Voided
}
