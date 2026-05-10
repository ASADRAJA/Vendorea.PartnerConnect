using System.Globalization;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;

/// <summary>
/// Parser for SPR price feed CSV files.
/// </summary>
public class SprPriceFeedParser
{
    private readonly ILogger<SprPriceFeedParser> _logger;

    // Expected column mappings (SPR-specific)
    private static class Columns
    {
        public const string PartnerSku = "SKU";
        public const string Upc = "UPC";
        public const string ManufacturerPartNumber = "MPN";
        public const string Cost = "DEALER_COST";
        public const string ListPrice = "MSRP";
        public const string MapPrice = "MAP";
        public const string EffectiveDate = "EFFECTIVE_DATE";
        public const string ExpirationDate = "EXPIRATION_DATE";
        public const string Currency = "CURRENCY";
    }

    public SprPriceFeedParser(ILogger<SprPriceFeedParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a price feed from a stream.
    /// </summary>
    public async Task<SprParseFeedResult<PriceUpdate>> ParseAsync(
        Stream stream,
        int dealerId,
        string sourceDocumentId,
        SprConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var items = new List<PriceUpdate>();
        var errors = new List<SprParseError>();
        var lineNumber = 0;

        using var reader = new StreamReader(stream);

        // Read header
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        lineNumber++;

        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return new SprParseFeedResult<PriceUpdate>
            {
                Success = false,
                Items = items,
                Errors = errors,
                ErrorMessage = "Empty file or missing header"
            };
        }

        var headers = ParseCsvLine(headerLine, config.CsvDelimiter);
        var columnMap = BuildColumnMap(headers);

        // Validate required columns
        if (!columnMap.ContainsKey(Columns.PartnerSku))
        {
            return new SprParseFeedResult<PriceUpdate>
            {
                Success = false,
                Items = items,
                Errors = errors,
                ErrorMessage = $"Missing required column: {Columns.PartnerSku}"
            };
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
                continue;
            }

            try
            {
                var values = ParseCsvLine(line, config.CsvDelimiter);
                var priceUpdate = MapToPriceUpdate(values, columnMap, dealerId, sourceDocumentId);

                if (priceUpdate != null)
                {
                    items.Add(priceUpdate);
                }
            }
            catch (Exception ex)
            {
                errors.Add(new SprParseError(lineNumber, line, ex.Message));
                _logger.LogWarning(
                    "Error parsing line {LineNumber}: {Error}",
                    lineNumber, ex.Message);
            }
        }

        _logger.LogInformation(
            "Parsed SPR price feed: {ItemCount} items, {ErrorCount} errors",
            items.Count, errors.Count);

        return new SprParseFeedResult<PriceUpdate>
        {
            Success = errors.Count == 0,
            Items = items,
            Errors = errors,
            TotalLinesProcessed = lineNumber,
            ErrorMessage = errors.Count > 0 ? $"{errors.Count} lines failed to parse" : null
        };
    }

    private Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim().ToUpperInvariant();
            map[header] = i;

            // Handle common variations
            switch (header)
            {
                case "ITEM_NUMBER":
                case "PRODUCT_SKU":
                case "ITEM_SKU":
                    if (!map.ContainsKey(Columns.PartnerSku))
                        map[Columns.PartnerSku] = i;
                    break;
                case "UPC_CODE":
                case "BARCODE":
                    if (!map.ContainsKey(Columns.Upc))
                        map[Columns.Upc] = i;
                    break;
                case "PART_NUMBER":
                case "MFG_PART":
                    if (!map.ContainsKey(Columns.ManufacturerPartNumber))
                        map[Columns.ManufacturerPartNumber] = i;
                    break;
                case "COST":
                case "PRICE":
                case "WHOLESALE":
                    if (!map.ContainsKey(Columns.Cost))
                        map[Columns.Cost] = i;
                    break;
                case "LIST":
                case "RETAIL":
                case "RETAIL_PRICE":
                    if (!map.ContainsKey(Columns.ListPrice))
                        map[Columns.ListPrice] = i;
                    break;
            }
        }

        return map;
    }

    private PriceUpdate? MapToPriceUpdate(
        string[] values,
        Dictionary<string, int> columnMap,
        int dealerId,
        string sourceDocumentId)
    {
        var partnerSku = GetValue(values, columnMap, Columns.PartnerSku);

        if (string.IsNullOrWhiteSpace(partnerSku))
        {
            return null;
        }

        var costStr = GetValue(values, columnMap, Columns.Cost);
        if (!decimal.TryParse(costStr, NumberStyles.Currency, CultureInfo.InvariantCulture, out var cost))
        {
            cost = 0;
        }

        decimal? listPrice = null;
        var listPriceStr = GetValue(values, columnMap, Columns.ListPrice);
        if (decimal.TryParse(listPriceStr, NumberStyles.Currency, CultureInfo.InvariantCulture, out var lp))
        {
            listPrice = lp;
        }

        decimal? mapPrice = null;
        var mapPriceStr = GetValue(values, columnMap, Columns.MapPrice);
        if (decimal.TryParse(mapPriceStr, NumberStyles.Currency, CultureInfo.InvariantCulture, out var mp))
        {
            mapPrice = mp;
        }

        var effectiveDate = DateTime.UtcNow;
        var effectiveDateStr = GetValue(values, columnMap, Columns.EffectiveDate);
        if (DateTime.TryParse(effectiveDateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ed))
        {
            effectiveDate = ed;
        }

        DateTime? expirationDate = null;
        var expirationDateStr = GetValue(values, columnMap, Columns.ExpirationDate);
        if (DateTime.TryParse(expirationDateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expd))
        {
            expirationDate = expd;
        }

        return new PriceUpdate
        {
            DealerId = dealerId,
            TradingPartnerCode = SprAdapter.AdapterCode,
            PartnerSku = partnerSku,
            Upc = GetValue(values, columnMap, Columns.Upc),
            ManufacturerPartNumber = GetValue(values, columnMap, Columns.ManufacturerPartNumber),
            Cost = cost,
            ListPrice = listPrice,
            MapPrice = mapPrice,
            Currency = CurrencyCode.USD,
            EffectiveDate = effectiveDate,
            ExpirationDate = expirationDate,
            ReceivedAt = DateTime.UtcNow,
            SourceDocumentId = sourceDocumentId,
            Status = CanonicalStatus.Pending
        };
    }

    private static string? GetValue(string[] values, Dictionary<string, int> columnMap, string columnName)
    {
        if (!columnMap.TryGetValue(columnName, out var index) || index >= values.Length)
        {
            return null;
        }

        var value = values[index].Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string[] ParseCsvLine(string line, char delimiter)
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
}

/// <summary>
/// Result of parsing an SPR feed.
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
