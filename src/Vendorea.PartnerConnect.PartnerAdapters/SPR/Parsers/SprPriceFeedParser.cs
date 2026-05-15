using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Models;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;

/// <summary>
/// Parser for SPR BESTPRICE CSV files.
/// Handles the multi-record format where each row contains Item (I), Cross-Reference (X), and Pricing (P) sections.
/// Total: 104 columns per row.
/// </summary>
public class SprPriceFeedParser
{
    private const int ExpectedColumnCount = 104;
    private const char DefaultDelimiter = ',';

    private readonly ILogger<SprPriceFeedParser> _logger;

    public SprPriceFeedParser(ILogger<SprPriceFeedParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses an SPR price feed file from a stream.
    /// </summary>
    public async Task<SprPriceParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var records = new List<SprPriceRecord>();
        var errors = new List<SprParseError>();
        var lineNumber = 0;
        var skippedLines = 0;

        using var reader = new StreamReader(stream);

        // Skip header row
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        lineNumber++;

        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return new SprPriceParseResult
            {
                Success = false,
                Records = records,
                Errors = errors,
                ErrorMessage = "Empty file or missing header",
                ParseDuration = stopwatch.Elapsed
            };
        }

        // Validate header has expected column count
        var headerColumns = ParseCsvLine(headerLine);
        if (headerColumns.Length != ExpectedColumnCount)
        {
            _logger.LogWarning(
                "Header column count mismatch. Expected {Expected}, got {Actual}",
                ExpectedColumnCount, headerColumns.Length);
        }

        // Parse data rows
        while (!reader.EndOfStream)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var line = await reader.ReadLineAsync(cancellationToken);
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                skippedLines++;
                continue;
            }

            try
            {
                var columns = ParseCsvLine(line);

                // Validate this is an Item record (starts with 'I')
                if (columns.Length == 0 || columns[0] != "I")
                {
                    skippedLines++;
                    continue;
                }

                if (columns.Length < ExpectedColumnCount)
                {
                    errors.Add(new SprParseError(
                        lineNumber,
                        line.Length > 100 ? line.Substring(0, 100) + "..." : line,
                        $"Insufficient columns. Expected {ExpectedColumnCount}, got {columns.Length}"));
                    continue;
                }

                var record = MapToRecord(columns, lineNumber);
                records.Add(record);
            }
            catch (Exception ex)
            {
                errors.Add(new SprParseError(
                    lineNumber,
                    line.Length > 100 ? line.Substring(0, 100) + "..." : line,
                    ex.Message));

                _logger.LogWarning(
                    "Error parsing line {LineNumber}: {Error}",
                    lineNumber, ex.Message);
            }
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Parsed SPR price feed: {RecordCount} records, {ErrorCount} errors, {SkippedCount} skipped in {Duration}ms",
            records.Count, errors.Count, skippedLines, stopwatch.ElapsedMilliseconds);

        return new SprPriceParseResult
        {
            Success = errors.Count == 0,
            Records = records,
            Errors = errors,
            TotalLinesProcessed = lineNumber,
            SkippedLines = skippedLines,
            ErrorMessage = errors.Count > 0 ? $"{errors.Count} lines failed to parse" : null,
            ParseDuration = stopwatch.Elapsed
        };
    }

    /// <summary>
    /// Parses the file and converts to canonical PriceUpdate records for Merchant360.
    /// </summary>
    public async Task<SprParseFeedResult<PriceUpdate>> ParseToCanonicalAsync(
        Stream stream,
        int dealerId,
        string sourceDocumentId,
        SprConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var parseResult = await ParseAsync(stream, cancellationToken);

        var priceUpdates = parseResult.Records
            .Select(r => MapToCanonicalPriceUpdate(r, dealerId, sourceDocumentId, config))
            .ToList();

        return new SprParseFeedResult<PriceUpdate>
        {
            Success = parseResult.Success,
            Items = priceUpdates,
            Errors = parseResult.Errors,
            TotalLinesProcessed = parseResult.TotalLinesProcessed,
            ErrorMessage = parseResult.ErrorMessage
        };
    }

    /// <summary>
    /// Maps an SPR record to the canonical PriceUpdate model.
    /// </summary>
    private PriceUpdate MapToCanonicalPriceUpdate(
        SprPriceRecord record,
        int dealerId,
        string sourceDocumentId,
        SprConfiguration config)
    {
        // Determine which cost to use based on dealer's pricing tier
        var dealerCost = config.PricingTier switch
        {
            SprPricingTier.Ccp3 => record.NetCostCcp3,
            SprPricingTier.Ccp4 => record.NetCostCcp4,
            _ => record.NetCostNonCcp
        };

        return new PriceUpdate
        {
            DealerId = dealerId,
            TradingPartnerCode = SprAdapter.AdapterCode,
            PartnerSku = record.StockNumber,
            Upc = string.IsNullOrWhiteSpace(record.Upc) ? null : record.Upc,
            ManufacturerPartNumber = null, // SPR doesn't provide MPN in this file
            Cost = dealerCost,
            ListPrice = record.RetailListPrice > 0 ? record.RetailListPrice : record.ConsumerPrice1,
            MapPrice = null, // SPR doesn't provide MAP in this file
            Currency = CurrencyCode.USD,
            EffectiveDate = record.PricingStartDate ?? DateTime.UtcNow,
            ExpirationDate = record.PricingEndDate,
            ReceivedAt = DateTime.UtcNow,
            SourceDocumentId = sourceDocumentId,
            Status = CanonicalStatus.Pending,
            PriceBreaks = BuildPriceBreaks(record)
        };
    }

    /// <summary>
    /// Builds price breaks from promo levels if available.
    /// </summary>
    private IReadOnlyList<PriceBreak>? BuildPriceBreaks(SprPriceRecord record)
    {
        var breaks = new List<PriceBreak>();

        if (record.PromoLevel1Quantity > 0 && record.PromoLevel1Cost > 0)
        {
            breaks.Add(new PriceBreak
            {
                MinQuantity = record.PromoLevel1Quantity,
                MaxQuantity = record.PromoLevel2Quantity > record.PromoLevel1Quantity
                    ? record.PromoLevel2Quantity - 1
                    : null,
                UnitPrice = record.PromoLevel1Cost
            });
        }

        if (record.PromoLevel2Quantity > 0 && record.PromoLevel2Cost > 0 &&
            record.PromoLevel2Quantity > record.PromoLevel1Quantity)
        {
            breaks.Add(new PriceBreak
            {
                MinQuantity = record.PromoLevel2Quantity,
                MaxQuantity = record.PromoLevel3Quantity > record.PromoLevel2Quantity
                    ? record.PromoLevel3Quantity - 1
                    : null,
                UnitPrice = record.PromoLevel2Cost
            });
        }

        if (record.PromoLevel3Quantity > 0 && record.PromoLevel3Cost > 0 &&
            record.PromoLevel3Quantity > record.PromoLevel2Quantity)
        {
            breaks.Add(new PriceBreak
            {
                MinQuantity = record.PromoLevel3Quantity,
                MaxQuantity = null,
                UnitPrice = record.PromoLevel3Cost
            });
        }

        return breaks.Count > 0 ? breaks : null;
    }

    /// <summary>
    /// Maps CSV columns to an SprPriceRecord.
    /// </summary>
    private SprPriceRecord MapToRecord(string[] columns, int lineNumber)
    {
        return new SprPriceRecord
        {
            SourceLineNumber = lineNumber,

            // Record Type I - Master Item (Columns 1-50, Index 0-49)
            RecordTypeI = GetString(columns, 0),
            StockNumber = GetString(columns, 1),
            StockNumberStripped = GetString(columns, 2),
            ProductDescription = GetString(columns, 3),
            ProductStatus = GetString(columns, 4),
            NewItemNumber = GetString(columns, 5),
            SellingUnitOfMeasure = GetString(columns, 6),
            GeneralLineCatalogPage = GetString(columns, 7),
            SpecialFlyerCatalogPage = GetString(columns, 8),
            FurnitureCatalogPage = GetString(columns, 9),
            Unused1 = GetString(columns, 10),
            Unused2 = GetString(columns, 11),
            PackingQuantity1 = GetInt(columns, 12),
            PackingUom1 = GetString(columns, 13),
            PackedPerUom1 = GetString(columns, 14),
            PackingQuantity2 = GetInt(columns, 15),
            PackingUom2 = GetString(columns, 16),
            PackedPerUom2 = GetString(columns, 17),
            PackingQuantity3 = GetInt(columns, 18),
            PackingUom3 = GetString(columns, 19),
            PackedPerUom3 = GetString(columns, 20),
            WeightLbs = GetDecimal(columns, 21),
            HeightInches = GetDecimal(columns, 22),
            LengthInches = GetDecimal(columns, 23),
            WidthInches = GetDecimal(columns, 24),
            CategoryCode = GetString(columns, 25),
            CountryOfOrigin = GetString(columns, 26),
            IsReadyToAssemble = GetBool(columns, 27),
            IsRecycled = GetBool(columns, 28),
            CanShipUps = GetString(columns, 29),
            BrokenQuantitiesAllowed = GetBool(columns, 30),
            RetailListPrice = GetDecimal(columns, 31),
            RetailUnitOfMeasure = GetString(columns, 32),
            RetailUnitsPerSuom = GetInt(columns, 33),
            MsdsRequired = GetString(columns, 34),
            RecommendedSubstitutions = GetString(columns, 35),
            OldItemNumber = GetString(columns, 36),
            CatalogListPrice = GetDecimal(columns, 37),
            CatalogUom = GetString(columns, 38),
            MinorityVendorFlag = GetString(columns, 39),
            IsCustom = GetBool(columns, 40),
            IsDatedGoods = GetBool(columns, 41),
            QuantityPerSuom = GetInt(columns, 42),
            IsNonReturnable = GetBool(columns, 43),
            IsAlwaysNet = GetBool(columns, 44),
            IsSpecialOrder = GetBool(columns, 45),
            HarmonizedCode = GetString(columns, 46),
            FreightRestricted = GetString(columns, 47),
            SingleUsePlastic = GetString(columns, 48),
            FutureUse1 = GetString(columns, 49),

            // Record Type X - Cross Reference (Columns 51-67, Index 50-66)
            RecordTypeX = GetString(columns, 50),
            XrefStockNumber = GetString(columns, 51),
            XrefStockNumberStripped = GetString(columns, 52),
            Upc = GetString(columns, 53),
            UnitedPrefixStockNumber = GetString(columns, 54),
            MpcNumber = GetString(columns, 55),
            MoorePrefixStockNumber = GetString(columns, 56),
            UpcRetailPackFactor = GetInt(columns, 57),
            UpcRetailPack = GetString(columns, 58),
            UpcIntermediatePackFactor = GetInt(columns, 59),
            UpcIntermediatePack = GetString(columns, 60),
            UpcCasePackFactor = GetInt(columns, 61),
            UpcCasePack = GetString(columns, 62),
            BranchStockingStatus = GetString(columns, 63),
            OldModel = GetString(columns, 64),
            NewModel = GetString(columns, 65),
            FutureUse2 = GetString(columns, 66),

            // Record Type P - Pricing (Columns 68-104, Index 67-103)
            RecordTypeP = GetString(columns, 67),
            PricingStockNumber = GetString(columns, 68),
            PricingStockNumberStripped = GetString(columns, 69),
            PricingProgramName = GetString(columns, 70),
            PricingProgramCode = GetString(columns, 71),
            FutureUse3 = GetString(columns, 72),
            PricingStartDate = GetDateMmDdYyyy(columns, 73),
            PricingEndDate = GetDateMmDdYyyy(columns, 74),
            PricingFlyerPage = GetString(columns, 75),
            MinimumSellingQuantity = GetInt(columns, 76),
            NetCostNonCcp = GetDecimal(columns, 77),
            NetCostCcp3 = GetDecimal(columns, 78),
            NetCostCcp4 = GetDecimal(columns, 79),
            VendorDropShipFlag = GetString(columns, 80),
            ShippingLeadTimeDays = GetInt(columns, 81),
            AutoProcureFromVendor = GetBool(columns, 82),
            ProjectNumberRequired = GetBool(columns, 83),
            FutureUse4 = GetString(columns, 84),
            PromoLevel1Quantity = GetInt(columns, 85),
            PromoLevel1Cost = GetDecimal(columns, 86),
            PromoLevel2Quantity = GetInt(columns, 87),
            PromoLevel2Cost = GetDecimal(columns, 88),
            PromoLevel3Quantity = GetInt(columns, 89),
            PromoLevel3Cost = GetDecimal(columns, 90),
            FutureUse5 = GetString(columns, 91),
            ConsumerPrice1Quantity = GetInt(columns, 92),
            ConsumerPrice1 = GetDecimal(columns, 93),
            ConsumerPrice2Quantity = GetInt(columns, 94),
            ConsumerPrice2 = GetDecimal(columns, 95),
            ConsumerPrice3Quantity = GetInt(columns, 96),
            ConsumerPrice3 = GetDecimal(columns, 97),
            ShippingLeadTimeDescription = GetString(columns, 98),
            ConsumerPriceInCatalog = GetDecimal(columns, 99),
            CatalogPriceUom = GetString(columns, 100),
            PriceCodeIdentifier = GetString(columns, 101),
            IsFirmCost = GetBool(columns, 102),
            IsNetCost = GetBool(columns, 103)
        };
    }

    #region Value Extraction Helpers

    private static string GetString(string[] columns, int index)
    {
        if (index >= columns.Length)
            return string.Empty;

        return columns[index]?.Trim() ?? string.Empty;
    }

    private static int GetInt(string[] columns, int index)
    {
        var value = GetString(columns, index);
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        // Handle SPR format with leading zeros (e.g., "00012")
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0;
    }

    private static decimal GetDecimal(string[] columns, int index)
    {
        var value = GetString(columns, index);
        if (string.IsNullOrWhiteSpace(value))
            return 0m;

        // Handle SPR format with leading zeros (e.g., "00023.96")
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0m;
    }

    private static bool GetBool(string[] columns, int index)
    {
        var value = GetString(columns, index).ToUpperInvariant();
        return value == "Y" || value == "YES" || value == "1" || value == "TRUE";
    }

    private static DateTime? GetDateMmDdYyyy(string[] columns, int index)
    {
        var value = GetString(columns, index);
        if (string.IsNullOrWhiteSpace(value) || value.Length != 8)
            return null;

        // Format: MMDDYYYY (e.g., "04012026" = April 1, 2026)
        if (DateTime.TryParseExact(
            value,
            "MMddyyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result))
        {
            return result;
        }

        return null;
    }

    #endregion

    #region CSV Parsing

    /// <summary>
    /// Parses a CSV line handling quoted fields and escaped quotes.
    /// </summary>
    private static string[] ParseCsvLine(string line, char delimiter = DefaultDelimiter)
    {
        var values = new List<string>();
        var inQuotes = false;
        var currentValue = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    currentValue.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                values.Add(currentValue.ToString());
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(c);
            }
        }

        values.Add(currentValue.ToString());
        return values.ToArray();
    }

    #endregion
}

/// <summary>
/// Result of parsing an SPR feed (generic version for compatibility).
/// </summary>
public class SprParseFeedResult<T>
{
    public bool Success { get; init; }
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public IReadOnlyList<SprParseError> Errors { get; init; } = Array.Empty<SprParseError>();
    public int TotalLinesProcessed { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents a parsing error for a specific line.
/// </summary>
public record SprParseError(int LineNumber, string LineContent, string ErrorMessage);
