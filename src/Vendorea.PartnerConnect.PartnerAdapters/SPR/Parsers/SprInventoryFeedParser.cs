using System.Globalization;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;

/// <summary>
/// Parser for SPR inventory feed CSV files.
/// </summary>
public class SprInventoryFeedParser
{
    private readonly ILogger<SprInventoryFeedParser> _logger;

    // Expected column mappings (SPR-specific)
    private static class Columns
    {
        public const string PartnerSku = "SKU";
        public const string Upc = "UPC";
        public const string ManufacturerPartNumber = "MPN";
        public const string QuantityAvailable = "QTY_AVAILABLE";
        public const string QuantityOnOrder = "QTY_ON_ORDER";
        public const string WarehouseCode = "WAREHOUSE";
        public const string AvailabilityStatus = "STATUS";
        public const string ExpectedRestockDate = "RESTOCK_DATE";
        public const string LeadTime = "LEAD_TIME_DAYS";
        public const string LastUpdated = "LAST_UPDATED";
    }

    public SprInventoryFeedParser(ILogger<SprInventoryFeedParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses an inventory feed from a stream.
    /// </summary>
    public async Task<SprParseFeedResult<InventoryUpdate>> ParseAsync(
        Stream stream,
        int dealerId,
        string sourceDocumentId,
        SprConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var items = new List<InventoryUpdate>();
        var errors = new List<SprParseError>();
        var lineNumber = 0;

        using var reader = new StreamReader(stream);

        // Read header
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        lineNumber++;

        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return new SprParseFeedResult<InventoryUpdate>
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
            return new SprParseFeedResult<InventoryUpdate>
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
                var inventoryUpdate = MapToInventoryUpdate(values, columnMap, dealerId, sourceDocumentId, config);

                if (inventoryUpdate != null)
                {
                    items.Add(inventoryUpdate);
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
            "Parsed SPR inventory feed: {ItemCount} items, {ErrorCount} errors",
            items.Count, errors.Count);

        return new SprParseFeedResult<InventoryUpdate>
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
                case "QTY":
                case "QUANTITY":
                case "STOCK":
                case "ON_HAND":
                    if (!map.ContainsKey(Columns.QuantityAvailable))
                        map[Columns.QuantityAvailable] = i;
                    break;
                case "WAREHOUSE_CODE":
                case "LOCATION":
                case "WH":
                    if (!map.ContainsKey(Columns.WarehouseCode))
                        map[Columns.WarehouseCode] = i;
                    break;
                case "AVAILABILITY":
                case "AVAIL_STATUS":
                    if (!map.ContainsKey(Columns.AvailabilityStatus))
                        map[Columns.AvailabilityStatus] = i;
                    break;
            }
        }

        return map;
    }

    private InventoryUpdate? MapToInventoryUpdate(
        string[] values,
        Dictionary<string, int> columnMap,
        int dealerId,
        string sourceDocumentId,
        SprConfiguration config)
    {
        var partnerSku = GetValue(values, columnMap, Columns.PartnerSku);

        if (string.IsNullOrWhiteSpace(partnerSku))
        {
            return null;
        }

        var qtyStr = GetValue(values, columnMap, Columns.QuantityAvailable);
        if (!int.TryParse(qtyStr, out var quantityAvailable))
        {
            quantityAvailable = 0;
        }

        int? quantityOnOrder = null;
        var qtyOnOrderStr = GetValue(values, columnMap, Columns.QuantityOnOrder);
        if (int.TryParse(qtyOnOrderStr, out var qoo))
        {
            quantityOnOrder = qoo;
        }

        int? leadTimeDays = null;
        var leadTimeStr = GetValue(values, columnMap, Columns.LeadTime);
        if (int.TryParse(leadTimeStr, out var lt))
        {
            leadTimeDays = lt;
        }

        DateTime? expectedRestockDate = null;
        var restockDateStr = GetValue(values, columnMap, Columns.ExpectedRestockDate);
        if (DateTime.TryParse(restockDateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var rd))
        {
            expectedRestockDate = rd;
        }

        DateTime? partnerUpdatedAt = null;
        var lastUpdatedStr = GetValue(values, columnMap, Columns.LastUpdated);
        if (DateTime.TryParse(lastUpdatedStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var lu))
        {
            partnerUpdatedAt = lu;
        }

        // Map warehouse code
        var warehouseCode = GetValue(values, columnMap, Columns.WarehouseCode);
        if (warehouseCode != null && config.WarehouseCodeMappings?.TryGetValue(warehouseCode, out var mappedCode) == true)
        {
            warehouseCode = mappedCode;
        }

        // Map availability status
        var statusStr = GetValue(values, columnMap, Columns.AvailabilityStatus);
        var availabilityStatus = MapAvailabilityStatus(statusStr, quantityAvailable);

        return new InventoryUpdate
        {
            DealerId = dealerId,
            TradingPartnerCode = SprAdapter.AdapterCode,
            PartnerSku = partnerSku,
            Upc = GetValue(values, columnMap, Columns.Upc),
            ManufacturerPartNumber = GetValue(values, columnMap, Columns.ManufacturerPartNumber),
            QuantityAvailable = quantityAvailable,
            QuantityOnOrder = quantityOnOrder,
            WarehouseCode = warehouseCode,
            AvailabilityStatus = availabilityStatus,
            ExpectedRestockDate = expectedRestockDate,
            LeadTimeDays = leadTimeDays,
            PartnerUpdatedAt = partnerUpdatedAt,
            ReceivedAt = DateTime.UtcNow,
            SourceDocumentId = sourceDocumentId,
            Status = CanonicalStatus.Pending
        };
    }

    private static AvailabilityStatus MapAvailabilityStatus(string? statusStr, int quantityAvailable)
    {
        if (string.IsNullOrWhiteSpace(statusStr))
        {
            return quantityAvailable > 0 ? AvailabilityStatus.InStock : AvailabilityStatus.OutOfStock;
        }

        var status = statusStr.ToUpperInvariant();

        return status switch
        {
            "IN_STOCK" or "INSTOCK" or "IN STOCK" or "AVAILABLE" or "A" => AvailabilityStatus.InStock,
            "LOW_STOCK" or "LOWSTOCK" or "LOW STOCK" or "LOW" or "L" => AvailabilityStatus.LowStock,
            "OUT_OF_STOCK" or "OUTOFSTOCK" or "OUT OF STOCK" or "OOS" or "O" => AvailabilityStatus.OutOfStock,
            "BACKORDERED" or "BACKORDER" or "BO" or "B" => AvailabilityStatus.Backordered,
            "DISCONTINUED" or "DISC" or "D" => AvailabilityStatus.Discontinued,
            "PREORDER" or "PRE_ORDER" or "PRE-ORDER" or "P" => AvailabilityStatus.PreOrder,
            _ => quantityAvailable > 0 ? AvailabilityStatus.InStock : AvailabilityStatus.OutOfStock
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
