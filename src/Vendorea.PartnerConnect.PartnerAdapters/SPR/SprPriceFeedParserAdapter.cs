using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;
using AppInterfaces = Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR;

/// <summary>
/// Adapter that bridges the SPR parser to the Application layer interface.
/// </summary>
public class SprPriceFeedParserAdapter : ISprPriceFeedParser
{
    private readonly SprPriceFeedParser _parser;

    public SprPriceFeedParserAdapter(SprPriceFeedParser parser)
    {
        _parser = parser;
    }

    public async Task<AppInterfaces.SprPriceParseResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = await _parser.ParseAsync(stream, cancellationToken);

        // Map to Application layer types
        return new AppInterfaces.SprPriceParseResult
        {
            Success = result.Success,
            Records = result.Records.Select(MapToSprParsedRecord).ToList(),
            Errors = result.Errors.Select(e => new AppInterfaces.SprParseError
            {
                LineNumber = e.LineNumber,
                RawContent = e.LineContent,
                ErrorMessage = e.ErrorMessage
            }).ToList(),
            TotalLinesProcessed = result.TotalLinesProcessed,
            SkippedLines = result.SkippedLines,
            ErrorMessage = result.ErrorMessage,
            ParseDuration = result.ParseDuration
        };
    }

    private static AppInterfaces.SprParsedRecord MapToSprParsedRecord(Models.SprPriceRecord r)
    {
        return new AppInterfaces.SprParsedRecord
        {
            StockNumber = r.StockNumber,
            StockNumberStripped = r.StockNumberStripped,
            ProductDescription = r.ProductDescription,
            ProductStatus = r.ProductStatus,
            NewItemNumber = r.NewItemNumber,
            SellingUnitOfMeasure = r.SellingUnitOfMeasure,
            GeneralLineCatalogPage = r.GeneralLineCatalogPage,
            SpecialFlyerCatalogPage = r.SpecialFlyerCatalogPage,
            FurnitureCatalogPage = r.FurnitureCatalogPage,
            PackingQuantity1 = r.PackingQuantity1,
            PackingUom1 = r.PackingUom1,
            PackedPerUom1 = r.PackedPerUom1,
            PackingQuantity2 = r.PackingQuantity2,
            PackingUom2 = r.PackingUom2,
            PackedPerUom2 = r.PackedPerUom2,
            PackingQuantity3 = r.PackingQuantity3,
            PackingUom3 = r.PackingUom3,
            PackedPerUom3 = r.PackedPerUom3,
            WeightLbs = r.WeightLbs,
            HeightInches = r.HeightInches,
            LengthInches = r.LengthInches,
            WidthInches = r.WidthInches,
            CategoryCode = r.CategoryCode,
            CountryOfOrigin = r.CountryOfOrigin,
            IsReadyToAssemble = r.IsReadyToAssemble,
            IsRecycled = r.IsRecycled,
            CanShipUps = r.CanShipUps,
            BrokenQuantitiesAllowed = r.BrokenQuantitiesAllowed,
            RetailListPrice = r.RetailListPrice,
            RetailUnitOfMeasure = r.RetailUnitOfMeasure,
            RetailUnitsPerSuom = r.RetailUnitsPerSuom,
            MsdsRequired = r.MsdsRequired,
            RecommendedSubstitutions = r.RecommendedSubstitutions,
            OldItemNumber = r.OldItemNumber,
            CatalogListPrice = r.CatalogListPrice,
            CatalogUom = r.CatalogUom,
            MinorityVendorFlag = r.MinorityVendorFlag,
            IsCustom = r.IsCustom,
            IsDatedGoods = r.IsDatedGoods,
            QuantityPerSuom = r.QuantityPerSuom,
            IsNonReturnable = r.IsNonReturnable,
            IsAlwaysNet = r.IsAlwaysNet,
            IsSpecialOrder = r.IsSpecialOrder,
            HarmonizedCode = r.HarmonizedCode,
            FreightRestricted = r.FreightRestricted,
            SingleUsePlastic = r.SingleUsePlastic,
            Upc = r.Upc,
            UnitedPrefixStockNumber = r.UnitedPrefixStockNumber,
            MpcNumber = r.MpcNumber,
            MoorePrefixStockNumber = r.MoorePrefixStockNumber,
            UpcRetailPackFactor = r.UpcRetailPackFactor,
            UpcRetailPack = r.UpcRetailPack,
            UpcIntermediatePackFactor = r.UpcIntermediatePackFactor,
            UpcIntermediatePack = r.UpcIntermediatePack,
            UpcCasePackFactor = r.UpcCasePackFactor,
            UpcCasePack = r.UpcCasePack,
            BranchStockingStatus = r.BranchStockingStatus,
            OldModel = r.OldModel,
            NewModel = r.NewModel,
            PricingProgramName = r.PricingProgramName,
            PricingProgramCode = r.PricingProgramCode,
            PricingStartDate = r.PricingStartDate,
            PricingEndDate = r.PricingEndDate,
            PricingFlyerPage = r.PricingFlyerPage,
            MinimumSellingQuantity = r.MinimumSellingQuantity,
            NetCostNonCcp = r.NetCostNonCcp,
            NetCostCcp3 = r.NetCostCcp3,
            NetCostCcp4 = r.NetCostCcp4,
            VendorDropShipFlag = r.VendorDropShipFlag,
            ShippingLeadTimeDays = r.ShippingLeadTimeDays,
            AutoProcureFromVendor = r.AutoProcureFromVendor,
            ProjectNumberRequired = r.ProjectNumberRequired,
            PromoLevel1Quantity = r.PromoLevel1Quantity,
            PromoLevel1Cost = r.PromoLevel1Cost,
            PromoLevel2Quantity = r.PromoLevel2Quantity,
            PromoLevel2Cost = r.PromoLevel2Cost,
            PromoLevel3Quantity = r.PromoLevel3Quantity,
            PromoLevel3Cost = r.PromoLevel3Cost,
            ConsumerPrice1Quantity = r.ConsumerPrice1Quantity,
            ConsumerPrice1 = r.ConsumerPrice1,
            ConsumerPrice2Quantity = r.ConsumerPrice2Quantity,
            ConsumerPrice2 = r.ConsumerPrice2,
            ConsumerPrice3Quantity = r.ConsumerPrice3Quantity,
            ConsumerPrice3 = r.ConsumerPrice3,
            ShippingLeadTimeDescription = r.ShippingLeadTimeDescription,
            ConsumerPriceInCatalog = r.ConsumerPriceInCatalog,
            CatalogPriceUom = r.CatalogPriceUom,
            PriceCodeIdentifier = r.PriceCodeIdentifier,
            IsFirmCost = r.IsFirmCost,
            IsNetCost = r.IsNetCost,
            SourceLineNumber = r.SourceLineNumber
        };
    }
}
