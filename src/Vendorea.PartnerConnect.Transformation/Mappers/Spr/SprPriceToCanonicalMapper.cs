using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Transformation.Interfaces;

namespace Vendorea.PartnerConnect.Transformation.Mappers.Spr;

/// <summary>
/// Maps SPR-specific price data to canonical PriceUpdate format.
/// </summary>
public class SprPriceToCanonicalMapper : IDocumentMapper<SprPriceRecord, PriceUpdate>
{
    public string PartnerCode => "SPR";
    public string DocumentType => "PriceList";

    /// <summary>
    /// Maps an SPR price record to a canonical PriceUpdate.
    /// </summary>
    public Task<MapperResult<PriceUpdate>> MapAsync(
        SprPriceRecord source,
        MappingContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(source.ItemNumber))
            {
                return Task.FromResult(MapperResult<PriceUpdate>.Failed("ItemNumber is required"));
            }

            if (source.DealerCost <= 0)
            {
                return Task.FromResult(MapperResult<PriceUpdate>.Failed("DealerCost must be greater than 0"));
            }

            var priceUpdate = new PriceUpdate
            {
                CorrelationId = context.CorrelationId,
                DealerId = context.DealerId,
                TradingPartnerCode = PartnerCode,
                PartnerSku = source.ItemNumber,
                Upc = NormalizeUpc(source.Upc),
                ManufacturerPartNumber = source.ManufacturerPartNumber,
                Cost = source.DealerCost,
                ListPrice = source.Msrp > 0 ? source.Msrp : null,
                MapPrice = source.Map > 0 ? source.Map : null,
                Currency = MapCurrencyCode(source.CurrencyCode),
                EffectiveDate = source.EffectiveDate ?? context.ProcessedAt,
                ExpirationDate = source.ExpirationDate,
                ReceivedAt = context.ProcessedAt,
                SourceDocumentId = context.SourceDocumentId,
                Status = CanonicalStatus.Transformed
            };

            var result = MapperResult<PriceUpdate>.Succeeded(priceUpdate);

            // Add warnings for potential issues
            if (source.Msrp.HasValue && source.Msrp < source.DealerCost)
            {
                result.Warnings.Add(new MappingWarning
                {
                    Code = "MSRP_BELOW_COST",
                    Message = "MSRP is less than dealer cost",
                    FieldName = "Msrp"
                });
            }

            if (source.Map.HasValue && source.Map < source.DealerCost)
            {
                result.Warnings.Add(new MappingWarning
                {
                    Code = "MAP_BELOW_COST",
                    Message = "MAP price is less than dealer cost",
                    FieldName = "Map"
                });
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(MapperResult<PriceUpdate>.Failed($"Mapping failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Validates whether the source record can be mapped.
    /// </summary>
    public Task<bool> CanMapAsync(SprPriceRecord source, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            !string.IsNullOrWhiteSpace(source.ItemNumber) &&
            source.DealerCost > 0);
    }

    /// <summary>
    /// Maps multiple SPR price records to canonical PriceUpdates.
    /// </summary>
    public async Task<IReadOnlyList<MapperResult<PriceUpdate>>> MapBatchAsync(
        IEnumerable<SprPriceRecord> sources,
        MappingContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new List<MapperResult<PriceUpdate>>();

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

    private static CurrencyCode MapCurrencyCode(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return CurrencyCode.USD;
        }

        return currencyCode.ToUpperInvariant() switch
        {
            "USD" or "US" or "DOLLAR" => CurrencyCode.USD,
            "CAD" or "CA" => CurrencyCode.CAD,
            "EUR" or "EURO" => CurrencyCode.EUR,
            "GBP" or "POUND" => CurrencyCode.GBP,
            _ => CurrencyCode.USD
        };
    }
}

/// <summary>
/// SPR-specific price record structure.
/// </summary>
public class SprPriceRecord
{
    public string ItemNumber { get; set; } = string.Empty;
    public string? Upc { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public decimal DealerCost { get; set; }
    public decimal? Msrp { get; set; }
    public decimal? Map { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string? Description { get; set; }
}
