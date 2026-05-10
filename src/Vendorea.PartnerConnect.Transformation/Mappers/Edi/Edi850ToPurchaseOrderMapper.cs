using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Edi.X12.Documents;
using Vendorea.PartnerConnect.Transformation.Interfaces;

namespace Vendorea.PartnerConnect.Transformation.Mappers.Edi;

/// <summary>
/// Maps EDI 850 purchase order content to canonical PurchaseOrder model.
/// </summary>
public class Edi850ToPurchaseOrderMapper : IDocumentMapper<string, PurchaseOrder>
{
    private readonly Edi850Parser _parser;

    public Edi850ToPurchaseOrderMapper()
    {
        _parser = new Edi850Parser();
    }

    public string PartnerCode => "*"; // Generic - works for any partner
    public string DocumentType => "850";

    public Task<MapperResult<PurchaseOrder>> MapAsync(
        string source,
        MappingContext context,
        CancellationToken cancellationToken = default)
    {
        var parseResult = _parser.Parse(source, context.DealerId, context.SourceDocumentId);

        if (!parseResult.Success || parseResult.PurchaseOrders.Count == 0)
        {
            return Task.FromResult(MapperResult<PurchaseOrder>.Failed(
                parseResult.ErrorMessage ?? "No purchase orders found in document"));
        }

        // Return the first PO (typically there's one per document)
        // Use 'with' expression to enrich with context (records are immutable)
        var po = parseResult.PurchaseOrders.First() with
        {
            TradingPartnerCode = context.TradingPartnerCode,
            CorrelationId = context.CorrelationId
        };

        var result = MapperResult<PurchaseOrder>.Succeeded(po);

        // Add warnings for any parsing errors
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
        // Basic validation - check if it looks like an X12 document
        if (string.IsNullOrWhiteSpace(source))
        {
            return Task.FromResult(false);
        }

        var trimmed = source.Trim();
        return Task.FromResult(trimmed.StartsWith("ISA", StringComparison.OrdinalIgnoreCase));
    }
}
