using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Transformation.Interfaces;

namespace Vendorea.PartnerConnect.Transformation.Mappers.Spr;

/// <summary>
/// Maps SPR-specific inventory data to canonical InventoryUpdate format.
/// </summary>
public class SprInventoryToCanonicalMapper : IDocumentMapper<SprInventoryRecord, InventoryUpdate>
{
    public string PartnerCode => "SPR";
    public string DocumentType => "InventoryFeed";

    /// <summary>
    /// Maps an SPR inventory record to a canonical InventoryUpdate.
    /// </summary>
    public Task<MapperResult<InventoryUpdate>> MapAsync(
        SprInventoryRecord source,
        MappingContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(source.ItemNumber))
            {
                return Task.FromResult(MapperResult<InventoryUpdate>.Failed("ItemNumber is required"));
            }

            if (source.QuantityAvailable < 0)
            {
                return Task.FromResult(MapperResult<InventoryUpdate>.Failed("QuantityAvailable cannot be negative"));
            }

            var inventoryUpdate = new InventoryUpdate
            {
                CorrelationId = context.CorrelationId,
                DealerId = context.DealerId,
                TradingPartnerCode = PartnerCode,
                PartnerSku = source.ItemNumber,
                Upc = NormalizeUpc(source.Upc),
                ManufacturerPartNumber = source.ManufacturerPartNumber,
                QuantityAvailable = source.QuantityAvailable,
                QuantityOnOrder = source.QuantityOnOrder,
                QuantityReserved = source.QuantityReserved,
                WarehouseCode = NormalizeWarehouseCode(source.WarehouseCode),
                AvailabilityStatus = MapAvailabilityStatus(source.Status, source.QuantityAvailable),
                ExpectedRestockDate = source.ExpectedRestockDate,
                LeadTimeDays = source.LeadTimeDays,
                ReceivedAt = context.ProcessedAt,
                PartnerUpdatedAt = source.LastUpdated,
                SourceDocumentId = context.SourceDocumentId,
                Status = CanonicalStatus.Transformed
            };

            var result = MapperResult<InventoryUpdate>.Succeeded(inventoryUpdate);

            // Add warnings for potential issues
            if (source.QuantityAvailable == 0 && source.QuantityOnOrder.GetValueOrDefault() == 0)
            {
                result.Warnings.Add(new MappingWarning
                {
                    Code = "ZERO_INVENTORY",
                    Message = "Item has zero available and zero on order",
                    FieldName = "QuantityAvailable"
                });
            }

            if (source.LeadTimeDays.HasValue && source.LeadTimeDays > 90)
            {
                result.Warnings.Add(new MappingWarning
                {
                    Code = "LONG_LEAD_TIME",
                    Message = $"Lead time of {source.LeadTimeDays} days exceeds 90 days",
                    FieldName = "LeadTimeDays"
                });
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(MapperResult<InventoryUpdate>.Failed($"Mapping failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Validates whether the source record can be mapped.
    /// </summary>
    public Task<bool> CanMapAsync(SprInventoryRecord source, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            !string.IsNullOrWhiteSpace(source.ItemNumber) &&
            source.QuantityAvailable >= 0);
    }

    /// <summary>
    /// Maps multiple SPR inventory records to canonical InventoryUpdates.
    /// </summary>
    public async Task<IReadOnlyList<MapperResult<InventoryUpdate>>> MapBatchAsync(
        IEnumerable<SprInventoryRecord> sources,
        MappingContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new List<MapperResult<InventoryUpdate>>();

        foreach (var source in sources)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var result = await MapAsync(source, context, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private static string? NormalizeUpc(string? upc)
    {
        if (string.IsNullOrWhiteSpace(upc))
        {
            return null;
        }

        // Remove any non-digit characters
        var digits = new string(upc.Where(char.IsDigit).ToArray());

        // Pad to 12 digits if needed (UPC-A)
        if (digits.Length > 0 && digits.Length < 12)
        {
            digits = digits.PadLeft(12, '0');
        }

        return digits.Length > 0 ? digits : null;
    }

    private static string? NormalizeWarehouseCode(string? warehouseCode)
    {
        if (string.IsNullOrWhiteSpace(warehouseCode))
        {
            return null;
        }

        // SPR warehouse code mappings
        return warehouseCode.ToUpperInvariant() switch
        {
            "MAIN" or "PRIMARY" or "1" => "WH-MAIN",
            "WEST" or "W" or "2" => "WH-WEST",
            "EAST" or "E" or "3" => "WH-EAST",
            "CENTRAL" or "C" or "4" => "WH-CENTRAL",
            _ => warehouseCode.ToUpperInvariant()
        };
    }

    private static AvailabilityStatus MapAvailabilityStatus(string? status, int quantityAvailable)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            if (quantityAvailable > 10)
                return AvailabilityStatus.InStock;
            if (quantityAvailable > 0)
                return AvailabilityStatus.LowStock;
            return AvailabilityStatus.OutOfStock;
        }

        return status.ToUpperInvariant() switch
        {
            "A" or "AVAILABLE" or "IN_STOCK" or "INSTOCK" => AvailabilityStatus.InStock,
            "L" or "LOW" or "LOW_STOCK" or "LOWSTOCK" => AvailabilityStatus.LowStock,
            "O" or "OOS" or "OUT_OF_STOCK" or "OUTOFSTOCK" => AvailabilityStatus.OutOfStock,
            "B" or "BO" or "BACKORDER" or "BACKORDERED" => AvailabilityStatus.Backordered,
            "D" or "DISC" or "DISCONTINUED" => AvailabilityStatus.Discontinued,
            "P" or "PRE" or "PREORDER" or "PRE_ORDER" => AvailabilityStatus.PreOrder,
            _ => quantityAvailable > 0 ? AvailabilityStatus.InStock : AvailabilityStatus.OutOfStock
        };
    }
}

/// <summary>
/// SPR-specific inventory record structure.
/// </summary>
public class SprInventoryRecord
{
    public string ItemNumber { get; set; } = string.Empty;
    public string? Upc { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public int QuantityAvailable { get; set; }
    public int? QuantityOnOrder { get; set; }
    public int? QuantityReserved { get; set; }
    public string? WarehouseCode { get; set; }
    public string? Status { get; set; }
    public DateTime? ExpectedRestockDate { get; set; }
    public int? LeadTimeDays { get; set; }
    public DateTime? LastUpdated { get; set; }
}
