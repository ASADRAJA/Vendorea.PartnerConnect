using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Edi.X12.Documents;
using Vendorea.PartnerConnect.Edi.X12.Generation;
using Vendorea.PartnerConnect.Transformation.Interfaces;

namespace Vendorea.PartnerConnect.Transformation.Mappers.Edi;

/// <summary>
/// Maps a canonical PurchaseOrder to an EDI 855 Purchase Order Acknowledgment.
/// Used to generate acknowledgments for received purchase orders.
/// </summary>
public class PurchaseOrderToEdi855Mapper : IDocumentMapper<PurchaseOrder, string>
{
    private readonly Edi855Generator _generator;

    public PurchaseOrderToEdi855Mapper()
    {
        _generator = new Edi855Generator();
    }

    public string PartnerCode => "*"; // Generic - works for any partner
    public string DocumentType => "855";

    /// <summary>
    /// Maps a PurchaseOrder to an EDI 855 acknowledgment string.
    /// </summary>
    public Task<MapperResult<string>> MapAsync(
        PurchaseOrder source,
        MappingContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(source.PoNumber))
            {
                return Task.FromResult(MapperResult<string>.Failed("PO Number is required"));
            }

            if (source.Lines == null || source.Lines.Count == 0)
            {
                return Task.FromResult(MapperResult<string>.Failed("Purchase order must have at least one line item"));
            }

            // Convert PurchaseOrder to PurchaseOrderAcknowledgment
            var acknowledgment = CreateAcknowledgment(source, context);

            // Get generator options from context or use defaults
            var options = CreateGeneratorOptions(source, context);

            // Generate the EDI 855 document
            var ediContent = _generator.Generate(acknowledgment, options);

            var result = MapperResult<string>.Succeeded(ediContent);

            // Add warnings for potential issues
            if (source.Lines.Any(l => l.QuantityOrdered <= 0))
            {
                result.Warnings.Add(new MappingWarning
                {
                    Code = "ZERO_QUANTITY",
                    Message = "One or more line items have zero or negative quantity",
                    FieldName = "Lines.QuantityOrdered"
                });
            }

            if (string.IsNullOrEmpty(source.CustomerAccountNumber))
            {
                result.Warnings.Add(new MappingWarning
                {
                    Code = "NO_CUSTOMER_ACCOUNT",
                    Message = "Customer account number not specified",
                    FieldName = "CustomerAccountNumber"
                });
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(MapperResult<string>.Failed($"Mapping failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Validates whether the source PurchaseOrder can be mapped.
    /// </summary>
    public Task<bool> CanMapAsync(PurchaseOrder source, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            !string.IsNullOrWhiteSpace(source.PoNumber) &&
            source.Lines != null &&
            source.Lines.Count > 0);
    }

    /// <summary>
    /// Creates a PurchaseOrderAcknowledgment from a PurchaseOrder.
    /// </summary>
    private static PurchaseOrderAcknowledgment CreateAcknowledgment(
        PurchaseOrder source,
        MappingContext context)
    {
        var acknowledgment = new PurchaseOrderAcknowledgment
        {
            PurchaseOrderNumber = source.PoNumber,
            AcknowledgmentDate = DateTime.UtcNow,
            AcknowledgmentType = "AC", // Acknowledge with Detail and Change
            VendorOrderNumber = GenerateVendorOrderNumber(source, context),
            EstimatedShipDate = source.RequestedShipDate,
            EstimatedDeliveryDate = source.RequestedDeliveryDate,
            TotalLineItems = source.Lines.Count,
            LineItems = source.Lines.Select(CreateAcknowledgmentLineItem).ToList()
        };

        return acknowledgment;
    }

    /// <summary>
    /// Creates an AcknowledgmentLineItem from a PurchaseOrderLine.
    /// </summary>
    private static AcknowledgmentLineItem CreateAcknowledgmentLineItem(PurchaseOrderLine line)
    {
        return new AcknowledgmentLineItem
        {
            LineNumber = line.LineNumber,
            PartnerSku = line.PartnerSku,
            Upc = line.Upc,
            QuantityOrdered = line.QuantityOrdered,
            QuantityAcknowledged = line.QuantityOrdered, // Default: acknowledge full quantity
            UnitOfMeasure = MapUnitOfMeasure(line.UnitOfMeasure),
            AcknowledgmentUnitOfMeasure = MapUnitOfMeasure(line.UnitOfMeasure),
            UnitPrice = line.UnitPrice,
            AcknowledgmentCode = "IA", // Item Accepted
            PromisedShipDate = line.RequestedDeliveryDate
        };
    }

    /// <summary>
    /// Creates generator options from context.
    /// </summary>
    private static Edi855GeneratorOptions CreateGeneratorOptions(
        PurchaseOrder source,
        MappingContext context)
    {
        // Try to get custom options from context
        var senderId = GetContextValue(context, "SenderId", source.TradingPartnerCode);
        var receiverId = GetContextValue(context, "ReceiverId", source.DealerId.ToString());

        return new Edi855GeneratorOptions
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            SenderQualifier = GetContextValue(context, "SenderQualifier", "ZZ"),
            ReceiverQualifier = GetContextValue(context, "ReceiverQualifier", "ZZ"),
            ApplicationSenderId = GetContextValue(context, "ApplicationSenderId", senderId),
            ApplicationReceiverId = GetContextValue(context, "ApplicationReceiverId", receiverId),
            InterchangeControlNumber = GetContextValue(context, "InterchangeControlNumber", GenerateControlNumber()),
            GroupControlNumber = GetContextValue(context, "GroupControlNumber", "1"),
            TransactionSetControlNumber = GetContextValue(context, "TransactionSetControlNumber", "0001"),
            IsProduction = GetContextValue(context, "IsProduction", "true") == "true"
        };
    }

    /// <summary>
    /// Gets a value from context or returns default.
    /// </summary>
    private static string GetContextValue(MappingContext context, string key, string defaultValue)
    {
        if (context.AdditionalData.TryGetValue(key, out var value) && value is string strValue)
        {
            return strValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Generates a vendor order number.
    /// </summary>
    private static string GenerateVendorOrderNumber(PurchaseOrder source, MappingContext context)
    {
        // Format: VND-{DealerId}-{Date}-{Sequence}
        return $"VND-{source.DealerId}-{DateTime.UtcNow:yyyyMMdd}-{source.PoNumber}";
    }

    /// <summary>
    /// Generates a unique control number.
    /// </summary>
    private static string GenerateControlNumber()
    {
        // Use timestamp-based control number
        return DateTime.UtcNow.ToString("HHmmssfff");
    }

    /// <summary>
    /// Maps canonical UnitOfMeasure to EDI unit code.
    /// </summary>
    private static string MapUnitOfMeasure(Canonical.Enums.UnitOfMeasure uom)
    {
        return uom switch
        {
            Canonical.Enums.UnitOfMeasure.Each => "EA",
            Canonical.Enums.UnitOfMeasure.Case => "CA",
            Canonical.Enums.UnitOfMeasure.Pack => "PK",
            Canonical.Enums.UnitOfMeasure.Box => "BX",
            Canonical.Enums.UnitOfMeasure.Pallet => "PL",
            Canonical.Enums.UnitOfMeasure.Dozen => "DZ",
            Canonical.Enums.UnitOfMeasure.Piece => "PC",
            Canonical.Enums.UnitOfMeasure.Pound => "LB",
            Canonical.Enums.UnitOfMeasure.Kilogram => "KG",
            Canonical.Enums.UnitOfMeasure.Liter => "LT",
            Canonical.Enums.UnitOfMeasure.Gallon => "GA",
            Canonical.Enums.UnitOfMeasure.Roll => "RL",
            _ => "EA"
        };
    }
}
