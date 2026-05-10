using Vendorea.PartnerConnect.Canonical.Enums;

namespace Vendorea.PartnerConnect.Canonical.Models;

/// <summary>
/// Canonical price update record representing a normalized price from any trading partner.
/// </summary>
public record PriceUpdate
{
    /// <summary>
    /// Unique correlation ID for tracking this update through the system.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The dealer ID this price update belongs to.
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
    /// Dealer's cost (wholesale price).
    /// </summary>
    public decimal Cost { get; init; }

    /// <summary>
    /// Suggested list price (MSRP).
    /// </summary>
    public decimal? ListPrice { get; init; }

    /// <summary>
    /// Minimum Advertised Price.
    /// </summary>
    public decimal? MapPrice { get; init; }

    /// <summary>
    /// Currency code for all prices.
    /// </summary>
    public CurrencyCode Currency { get; init; } = CurrencyCode.USD;

    /// <summary>
    /// Date when this price becomes effective.
    /// </summary>
    public DateTime EffectiveDate { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Date when this price expires (null = no expiration).
    /// </summary>
    public DateTime? ExpirationDate { get; init; }

    /// <summary>
    /// Quantity-based price breaks.
    /// </summary>
    public IReadOnlyList<PriceBreak>? PriceBreaks { get; init; }

    /// <summary>
    /// When this record was received from the partner.
    /// </summary>
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;

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

/// <summary>
/// Quantity-based price break.
/// </summary>
public record PriceBreak
{
    /// <summary>
    /// Minimum quantity for this price break.
    /// </summary>
    public int MinQuantity { get; init; }

    /// <summary>
    /// Maximum quantity for this price break (null = unlimited).
    /// </summary>
    public int? MaxQuantity { get; init; }

    /// <summary>
    /// Unit price at this quantity level.
    /// </summary>
    public decimal UnitPrice { get; init; }
}
