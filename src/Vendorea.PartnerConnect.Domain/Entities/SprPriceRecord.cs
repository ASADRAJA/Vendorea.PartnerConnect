namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// SPR price record entity containing all 104 fields from the BESTPRICE CSV file.
/// Each row contains three concatenated record types: Item (I), Cross-Reference (X), and Pricing (P).
/// </summary>
public class SprPriceRecord
{
    public long Id { get; set; }

    /// <summary>
    /// The price feed upload this record belongs to.
    /// </summary>
    public int PriceFeedUploadId { get; set; }

    /// <summary>
    /// Navigation property to the upload.
    /// </summary>
    public PriceFeedUpload? PriceFeedUpload { get; set; }

    /// <summary>
    /// Denormalized dealer ID for efficient queries.
    /// </summary>
    public int DealerId { get; set; }

    // ==========================================
    // Record Type I - Master Item (Columns 1-50)
    // ==========================================

    /// <summary>Column 2: Stock Number - Primary SKU identifier</summary>
    public string StockNumber { get; set; } = string.Empty;

    /// <summary>Column 3: Stock Number without special characters</summary>
    public string StockNumberStripped { get; set; } = string.Empty;

    /// <summary>Column 4: Product Description</summary>
    public string ProductDescription { get; set; } = string.Empty;

    /// <summary>Column 5: Product Status - A=New, C=Change, D=Discontinue, U=Unchanged</summary>
    public string ProductStatus { get; set; } = string.Empty;

    /// <summary>Column 6: New item number (replacement SKU if applicable)</summary>
    public string? NewItemNumber { get; set; }

    /// <summary>Column 7: SPR selling unit of measure (SUOM) - EA, PK, CT, etc.</summary>
    public string SellingUnitOfMeasure { get; set; } = string.Empty;

    /// <summary>Column 8: General Line catalog page number</summary>
    public string? GeneralLineCatalogPage { get; set; }

    /// <summary>Column 9: Special flyer/catalog page number</summary>
    public string? SpecialFlyerCatalogPage { get; set; }

    /// <summary>Column 10: Furniture catalog page number</summary>
    public string? FurnitureCatalogPage { get; set; }

    /// <summary>Column 13: Item packing quantity #1</summary>
    public int PackingQuantity1 { get; set; }

    /// <summary>Column 14: Packing unit of measure #1</summary>
    public string? PackingUom1 { get; set; }

    /// <summary>Column 15: Packed per unit of measure #1</summary>
    public string? PackedPerUom1 { get; set; }

    /// <summary>Column 16: Item packing quantity #2</summary>
    public int PackingQuantity2 { get; set; }

    /// <summary>Column 17: Packing unit of measure #2</summary>
    public string? PackingUom2 { get; set; }

    /// <summary>Column 18: Packed per unit of measure #2</summary>
    public string? PackedPerUom2 { get; set; }

    /// <summary>Column 19: Item packing quantity #3</summary>
    public int PackingQuantity3 { get; set; }

    /// <summary>Column 20: Packing unit of measure #3</summary>
    public string? PackingUom3 { get; set; }

    /// <summary>Column 21: Packed per unit of measure #3</summary>
    public string? PackedPerUom3 { get; set; }

    /// <summary>Column 22: Item weight in lbs per SUOM</summary>
    public decimal WeightLbs { get; set; }

    /// <summary>Column 23: Item height in inches</summary>
    public decimal HeightInches { get; set; }

    /// <summary>Column 24: Item length in inches</summary>
    public decimal LengthInches { get; set; }

    /// <summary>Column 25: Item width in inches</summary>
    public decimal WidthInches { get; set; }

    /// <summary>Column 26: Product classification/category code</summary>
    public string? CategoryCode { get; set; }

    /// <summary>Column 27: Country of origin (2-letter code)</summary>
    public string? CountryOfOrigin { get; set; }

    /// <summary>Column 28: Is this a ready-to-assemble item?</summary>
    public bool IsReadyToAssemble { get; set; }

    /// <summary>Column 29: Is this a recycled item?</summary>
    public bool IsRecycled { get; set; }

    /// <summary>Column 30: Can item be shipped via UPS? (Y/N/G)</summary>
    public string? CanShipUps { get; set; }

    /// <summary>Column 31: Broken quantities are allowed?</summary>
    public bool BrokenQuantitiesAllowed { get; set; }

    /// <summary>Column 32: Current retail list price (MSRP)</summary>
    public decimal RetailListPrice { get; set; }

    /// <summary>Column 33: Retail unit of measure</summary>
    public string? RetailUnitOfMeasure { get; set; }

    /// <summary>Column 34: Retail units per SUOM</summary>
    public int RetailUnitsPerSuom { get; set; }

    /// <summary>Column 35: Material Safety Data Sheet required? (Y/N/H)</summary>
    public string? MsdsRequired { get; set; }

    /// <summary>Column 36: Recommended item substitutions</summary>
    public string? RecommendedSubstitutions { get; set; }

    /// <summary>Column 37: Old item number</summary>
    public string? OldItemNumber { get; set; }

    /// <summary>Column 38: List price printed in general line catalog</summary>
    public decimal CatalogListPrice { get; set; }

    /// <summary>Column 39: Unit of measure printed in general line catalog</summary>
    public string? CatalogUom { get; set; }

    /// <summary>Column 40: Minority/Women-Owned/Challenged Vendor</summary>
    public string? MinorityVendorFlag { get; set; }

    /// <summary>Column 41: Custom?</summary>
    public bool IsCustom { get; set; }

    /// <summary>Column 42: Dated Goods?</summary>
    public bool IsDatedGoods { get; set; }

    /// <summary>Column 43: Quantity per SUOM</summary>
    public int QuantityPerSuom { get; set; }

    /// <summary>Column 44: Non Returnable Item?</summary>
    public bool IsNonReturnable { get; set; }

    /// <summary>Column 45: Always Net Item</summary>
    public bool IsAlwaysNet { get; set; }

    /// <summary>Column 46: Special Order Item</summary>
    public bool IsSpecialOrder { get; set; }

    /// <summary>Column 47: Harmonized Code (for customs)</summary>
    public string? HarmonizedCode { get; set; }

    /// <summary>Column 48: SPR Freight Restricted</summary>
    public string? FreightRestricted { get; set; }

    /// <summary>Column 49: Single Use Plastic</summary>
    public string? SingleUsePlastic { get; set; }

    // ==========================================
    // Record Type X - Cross Reference (Columns 51-67)
    // ==========================================

    /// <summary>Column 54: Universal Product Code (UPC)</summary>
    public string? Upc { get; set; }

    /// <summary>Column 55: United prefix and stock number</summary>
    public string? UnitedPrefixStockNumber { get; set; }

    /// <summary>Column 56: MPC Number (Moore Product Code)</summary>
    public string? MpcNumber { get; set; }

    /// <summary>Column 57: Moore prefix and stock number</summary>
    public string? MoorePrefixStockNumber { get; set; }

    /// <summary>Column 58: UPC retail packing factor</summary>
    public int UpcRetailPackFactor { get; set; }

    /// <summary>Column 59: UPC number for retail pack</summary>
    public string? UpcRetailPack { get; set; }

    /// <summary>Column 60: UPC intermediate packing factor</summary>
    public int UpcIntermediatePackFactor { get; set; }

    /// <summary>Column 61: UPC number for intermediate pack</summary>
    public string? UpcIntermediatePack { get; set; }

    /// <summary>Column 62: UPC case packing factor</summary>
    public int UpcCasePackFactor { get; set; }

    /// <summary>Column 63: UPC number for case pack</summary>
    public string? UpcCasePack { get; set; }

    /// <summary>Column 64: Stocking status at each SPR branch location</summary>
    public string? BranchStockingStatus { get; set; }

    /// <summary>Column 65: Old Model</summary>
    public string? OldModel { get; set; }

    /// <summary>Column 66: New Model</summary>
    public string? NewModel { get; set; }

    // ==========================================
    // Record Type P - Pricing (Columns 68-104)
    // ==========================================

    /// <summary>Column 71: Pricing Program Name (e.g., "STANDARD CONSUMER")</summary>
    public string? PricingProgramName { get; set; }

    /// <summary>Column 72: Pricing Program Code (e.g., "LB")</summary>
    public string? PricingProgramCode { get; set; }

    /// <summary>Column 74: Pricing Start Date</summary>
    public DateTime? PricingStartDate { get; set; }

    /// <summary>Column 75: Pricing End Date</summary>
    public DateTime? PricingEndDate { get; set; }

    /// <summary>Column 76: Special Flyer/Catalog Page Number</summary>
    public string? PricingFlyerPage { get; set; }

    /// <summary>Column 77: Minimum selling quantity</summary>
    public int MinimumSellingQuantity { get; set; }

    /// <summary>Column 78: Net Cost for non-CCP Dealers (Primary dealer cost)</summary>
    public decimal NetCostNonCcp { get; set; }

    /// <summary>Column 79: Net Cost for Dealers on SPR CCP-3 program</summary>
    public decimal NetCostCcp3 { get; set; }

    /// <summary>Column 80: Net Cost for Dealers on SPR CCP-4 program</summary>
    public decimal NetCostCcp4 { get; set; }

    /// <summary>Column 81: Vendor Drop Ship Flag (V=Vendor, C=?)</summary>
    public string? VendorDropShipFlag { get; set; }

    /// <summary>Column 82: Shipping Lead Time in Days</summary>
    public int ShippingLeadTimeDays { get; set; }

    /// <summary>Column 83: Automatically Procure from Vendor</summary>
    public bool AutoProcureFromVendor { get; set; }

    /// <summary>Column 84: Project number for personalized product is required</summary>
    public bool ProjectNumberRequired { get; set; }

    /// <summary>Column 86: Promo Level 1 Quantity</summary>
    public int PromoLevel1Quantity { get; set; }

    /// <summary>Column 87: Promo Level 1 Dealer Cost</summary>
    public decimal PromoLevel1Cost { get; set; }

    /// <summary>Column 88: Promo Level 2 Quantity</summary>
    public int PromoLevel2Quantity { get; set; }

    /// <summary>Column 89: Promo Level 2 Dealer Cost</summary>
    public decimal PromoLevel2Cost { get; set; }

    /// <summary>Column 90: Promo Level 3 Quantity</summary>
    public int PromoLevel3Quantity { get; set; }

    /// <summary>Column 91: Promo Level 3 Dealer Cost</summary>
    public decimal PromoLevel3Cost { get; set; }

    /// <summary>Column 93: Consumer price #1 quantity</summary>
    public int ConsumerPrice1Quantity { get; set; }

    /// <summary>Column 94: Consumer price #1 price (Retail/Consumer price)</summary>
    public decimal ConsumerPrice1 { get; set; }

    /// <summary>Column 95: Consumer price #2 quantity</summary>
    public int ConsumerPrice2Quantity { get; set; }

    /// <summary>Column 96: Consumer price #2 price</summary>
    public decimal ConsumerPrice2 { get; set; }

    /// <summary>Column 97: Consumer price #3 quantity</summary>
    public int ConsumerPrice3Quantity { get; set; }

    /// <summary>Column 98: Consumer price #3 price</summary>
    public decimal ConsumerPrice3 { get; set; }

    /// <summary>Column 99: Shipping Lead Time Description</summary>
    public string? ShippingLeadTimeDescription { get; set; }

    /// <summary>Column 100: Consumer price printed in flyer/catalog</summary>
    public decimal ConsumerPriceInCatalog { get; set; }

    /// <summary>Column 101: Unit of measure printed in flyer/catalog</summary>
    public string? CatalogPriceUom { get; set; }

    /// <summary>Column 102: Price Code Identifier</summary>
    public string? PriceCodeIdentifier { get; set; }

    /// <summary>Column 103: Firm Cost?</summary>
    public bool IsFirmCost { get; set; }

    /// <summary>Column 104: Net Cost?</summary>
    public bool IsNetCost { get; set; }

    // ==========================================
    // Metadata
    // ==========================================

    /// <summary>Line number in the source file (for error tracking)</summary>
    public int SourceLineNumber { get; set; }

    /// <summary>When this record was created</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
