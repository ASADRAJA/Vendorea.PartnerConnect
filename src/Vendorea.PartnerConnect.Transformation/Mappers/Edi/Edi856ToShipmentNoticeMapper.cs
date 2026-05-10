using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Edi.X12.Documents;
using Vendorea.PartnerConnect.Transformation.Interfaces;

namespace Vendorea.PartnerConnect.Transformation.Mappers.Edi;

/// <summary>
/// Maps EDI 856 ASN content to canonical ShipmentNotice model.
/// </summary>
public class Edi856ToShipmentNoticeMapper : IDocumentMapper<string, ShipmentNotice>
{
    private readonly Edi856Parser _parser;

    public Edi856ToShipmentNoticeMapper()
    {
        _parser = new Edi856Parser();
    }

    public string PartnerCode => "*";
    public string DocumentType => "856";

    public Task<MapperResult<ShipmentNotice>> MapAsync(
        string source,
        MappingContext context,
        CancellationToken cancellationToken = default)
    {
        var parseResult = _parser.Parse(source, context.DealerId, context.SourceDocumentId);

        if (!parseResult.Success || parseResult.ShipmentNotices.Count == 0)
        {
            return Task.FromResult(MapperResult<ShipmentNotice>.Failed(
                parseResult.ErrorMessage ?? "No shipment notices found in document"));
        }

        // Use 'with' expression to enrich with context (records are immutable)
        var asn = parseResult.ShipmentNotices.First() with
        {
            TradingPartnerCode = context.TradingPartnerCode,
            CorrelationId = context.CorrelationId
        };

        var result = MapperResult<ShipmentNotice>.Succeeded(asn);

        foreach (var error in parseResult.Errors)
        {
            result.Warnings.Add(new MappingWarning
            {
                Code = "PARSE_ERROR",
                Message = error
            });
        }

        return Task.FromResult(result);
    }

    public Task<bool> CanMapAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Task.FromResult(false);
        }

        var trimmed = source.Trim();
        return Task.FromResult(trimmed.StartsWith("ISA", StringComparison.OrdinalIgnoreCase));
    }
}
