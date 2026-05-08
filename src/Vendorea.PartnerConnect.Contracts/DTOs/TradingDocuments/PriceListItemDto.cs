namespace Vendorea.PartnerConnect.Contracts.DTOs.TradingDocuments;

/// <summary>
/// Represents a single item in a price list feed from a trading partner.
/// </summary>
public record PriceListItemDto(
    string PartnerSku,
    string? Upc,
    string? ManufacturerPartNumber,
    decimal Cost,
    decimal? ListPrice,
    decimal? MapPrice,
    string? CurrencyCode,
    DateTime? EffectiveDate,
    DateTime? ExpirationDate,
    string? PriceBreakJson);
