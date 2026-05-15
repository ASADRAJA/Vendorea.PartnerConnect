using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Interface for parsing SPR price feed files.
/// </summary>
public interface ISprPriceFeedParser
{
    /// <summary>
    /// Parses an SPR price feed file and returns the result.
    /// </summary>
    /// <param name="stream">The file content stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parse result containing records and any errors.</returns>
    Task<SprPriceParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of parsing an SPR price feed file.
/// </summary>
public class SprPriceParseResult
{
    public bool Success { get; set; }
    public IReadOnlyList<SprParsedRecord> Records { get; set; } = Array.Empty<SprParsedRecord>();
    public IReadOnlyList<SprParseError> Errors { get; set; } = Array.Empty<SprParseError>();
    public int TotalLinesProcessed { get; set; }
    public int SkippedLines { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ParseDuration { get; set; }
}

/// <summary>
/// A parsed SPR price record (DTO from parser to service).
/// Maps 1:1 to SprPriceRecord entity fields.
/// </summary>
public class SprParsedRecord
{
    // Record Type I - Master Item
    public string StockNumber { get; set; } = string.Empty;
    public string StockNumberStripped { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public string ProductStatus { get; set; } = string.Empty;
    public string? NewItemNumber { get; set; }
    public string SellingUnitOfMeasure { get; set; } = string.Empty;
    public string? GeneralLineCatalogPage { get; set; }
    public string? SpecialFlyerCatalogPage { get; set; }
    public string? FurnitureCatalogPage { get; set; }
    public int PackingQuantity1 { get; set; }
    public string? PackingUom1 { get; set; }
    public string? PackedPerUom1 { get; set; }
    public int PackingQuantity2 { get; set; }
    public string? PackingUom2 { get; set; }
    public string? PackedPerUom2 { get; set; }
    public int PackingQuantity3 { get; set; }
    public string? PackingUom3 { get; set; }
    public string? PackedPerUom3 { get; set; }
    public decimal WeightLbs { get; set; }
    public decimal HeightInches { get; set; }
    public decimal LengthInches { get; set; }
    public decimal WidthInches { get; set; }
    public string? CategoryCode { get; set; }
    public string? CountryOfOrigin { get; set; }
    public bool IsReadyToAssemble { get; set; }
    public bool IsRecycled { get; set; }
    public string? CanShipUps { get; set; }
    public bool BrokenQuantitiesAllowed { get; set; }
    public decimal RetailListPrice { get; set; }
    public string? RetailUnitOfMeasure { get; set; }
    public int RetailUnitsPerSuom { get; set; }
    public string? MsdsRequired { get; set; }
    public string? RecommendedSubstitutions { get; set; }
    public string? OldItemNumber { get; set; }
    public decimal CatalogListPrice { get; set; }
    public string? CatalogUom { get; set; }
    public string? MinorityVendorFlag { get; set; }
    public bool IsCustom { get; set; }
    public bool IsDatedGoods { get; set; }
    public int QuantityPerSuom { get; set; }
    public bool IsNonReturnable { get; set; }
    public bool IsAlwaysNet { get; set; }
    public bool IsSpecialOrder { get; set; }
    public string? HarmonizedCode { get; set; }
    public string? FreightRestricted { get; set; }
    public string? SingleUsePlastic { get; set; }

    // Record Type X - Cross Reference
    public string? Upc { get; set; }
    public string? UnitedPrefixStockNumber { get; set; }
    public string? MpcNumber { get; set; }
    public string? MoorePrefixStockNumber { get; set; }
    public int UpcRetailPackFactor { get; set; }
    public string? UpcRetailPack { get; set; }
    public int UpcIntermediatePackFactor { get; set; }
    public string? UpcIntermediatePack { get; set; }
    public int UpcCasePackFactor { get; set; }
    public string? UpcCasePack { get; set; }
    public string? BranchStockingStatus { get; set; }
    public string? OldModel { get; set; }
    public string? NewModel { get; set; }

    // Record Type P - Pricing
    public string? PricingProgramName { get; set; }
    public string? PricingProgramCode { get; set; }
    public DateTime? PricingStartDate { get; set; }
    public DateTime? PricingEndDate { get; set; }
    public string? PricingFlyerPage { get; set; }
    public int MinimumSellingQuantity { get; set; }
    public decimal NetCostNonCcp { get; set; }
    public decimal NetCostCcp3 { get; set; }
    public decimal NetCostCcp4 { get; set; }
    public string? VendorDropShipFlag { get; set; }
    public int ShippingLeadTimeDays { get; set; }
    public bool AutoProcureFromVendor { get; set; }
    public bool ProjectNumberRequired { get; set; }
    public int PromoLevel1Quantity { get; set; }
    public decimal PromoLevel1Cost { get; set; }
    public int PromoLevel2Quantity { get; set; }
    public decimal PromoLevel2Cost { get; set; }
    public int PromoLevel3Quantity { get; set; }
    public decimal PromoLevel3Cost { get; set; }
    public int ConsumerPrice1Quantity { get; set; }
    public decimal ConsumerPrice1 { get; set; }
    public int ConsumerPrice2Quantity { get; set; }
    public decimal ConsumerPrice2 { get; set; }
    public int ConsumerPrice3Quantity { get; set; }
    public decimal ConsumerPrice3 { get; set; }
    public string? ShippingLeadTimeDescription { get; set; }
    public decimal ConsumerPriceInCatalog { get; set; }
    public string? CatalogPriceUom { get; set; }
    public string? PriceCodeIdentifier { get; set; }
    public bool IsFirmCost { get; set; }
    public bool IsNetCost { get; set; }

    // Metadata
    public int SourceLineNumber { get; set; }
}

/// <summary>
/// Error encountered during SPR price feed parsing.
/// </summary>
public class SprParseError
{
    public int LineNumber { get; set; }
    public string? RawContent { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
