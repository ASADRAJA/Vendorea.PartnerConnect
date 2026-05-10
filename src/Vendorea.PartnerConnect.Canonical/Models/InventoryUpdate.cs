using Vendorea.PartnerConnect.Canonical.Enums;

namespace Vendorea.PartnerConnect.Canonical.Models;

/// <summary>
/// Canonical inventory update record representing normalized inventory from any trading partner.
/// </summary>
public record InventoryUpdate
{
    /// <summary>
    /// Unique correlation ID for tracking this update through the system.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The dealer ID this inventory update belongs to.
    /// </summary>
    public int DealerId { get; init; }

    /// <summary>
    /// The trading partner code (e.g., "SPR").
    /// </summary>
    public string TradingPartnerCode { get; init; } = string.Empty;

    /// <summary>
    /// Partner's SKU identifier.
    /// </summary>
    public string PartnerSku { get; init; } = string.Empty;

    /// <summary>
    /// Universal Product Code (UPC/EAN).
    /// </summary>
    public string? Upc { get; init; }

    /// <summary>
    /// Manufacturer part number.
    /// </summary>
    public string? ManufacturerPartNumber { get; init; }

    /// <summary>
    /// Quantity currently available for sale.
    /// </summary>
    public int QuantityAvailable { get; init; }

    /// <summary>
    /// Quantity on order from supplier.
    /// </summary>
    public int? QuantityOnOrder { get; init; }

    /// <summary>
    /// Quantity reserved/allocated.
    /// </summary>
    public int? QuantityReserved { get; init; }

    /// <summary>
    /// Warehouse or location code.
    /// </summary>
    public string? WarehouseCode { get; init; }

    /// <summary>
    /// Current availability status.
    /// </summary>
    public AvailabilityStatus AvailabilityStatus { get; init; } = AvailabilityStatus.InStock;

    /// <summary>
    /// Expected restock date if out of stock.
    /// </summary>
    public DateTime? ExpectedRestockDate { get; init; }

    /// <summary>
    /// Lead time in days for replenishment.
    /// </summary>
    public int? LeadTimeDays { get; init; }

    /// <summary>
    /// When this record was received from the partner.
    /// </summary>
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the partner last updated this inventory.
    /// </summary>
    public DateTime? PartnerUpdatedAt { get; init; }

    /// <summary>
    /// Reference to the source document ID.
    /// </summary>
    public string? SourceDocumentId { get; init; }

    /// <summary>
    /// Processing status of this update.
    /// </summary>
    public CanonicalStatus Status { get; init; } = CanonicalStatus.Pending;

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
