using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Edi.X12.Documents;
using Vendorea.PartnerConnect.Transformation.Interfaces;

namespace Vendorea.PartnerConnect.Transformation.Mappers.Edi;

/// <summary>
/// Maps EDI 810 invoice content to canonical SupplierInvoice model.
/// </summary>
public class Edi810ToInvoiceMapper : IDocumentMapper<string, SupplierInvoice>
{
    private readonly Edi810Parser _parser;

    public Edi810ToInvoiceMapper()
    {
        _parser = new Edi810Parser();
    }

    public string PartnerCode => "*";
    public string DocumentType => "810";

    public Task<MapperResult<SupplierInvoice>> MapAsync(
        string source,
        MappingContext context,
        CancellationToken cancellationToken = default)
    {
        var parseResult = _parser.Parse(source, context.DealerId, context.SourceDocumentId);

        if (!parseResult.Success || parseResult.Invoices.Count == 0)
        {
            return Task.FromResult(MapperResult<SupplierInvoice>.Failed(
                parseResult.ErrorMessage ?? "No invoices found in document"));
        }

        // Use 'with' expression to enrich with context (records are immutable)
        var invoice = parseResult.Invoices.First() with
        {
            TradingPartnerCode = context.TradingPartnerCode,
            CorrelationId = context.CorrelationId
        };

        var result = MapperResult<SupplierInvoice>.Succeeded(invoice);

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
