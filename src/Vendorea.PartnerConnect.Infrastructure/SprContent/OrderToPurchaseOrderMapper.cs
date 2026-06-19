using System.Text.Json;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent;

/// <summary>
/// Maps a persisted domain <see cref="Order"/> to the canonical <see cref="PurchaseOrder"/>
/// consumed by the SPR EZPO4 generator. Addresses are stored on the order as serialized
/// <see cref="CanonicalAddressInfo"/> (the M360 intake shape).
/// </summary>
public static class OrderToPurchaseOrderMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PurchaseOrder Map(Order order)
    {
        return new PurchaseOrder
        {
            CorrelationId = order.CorrelationId.ToString(),
            DealerId = order.TenantId,
            TradingPartnerCode = order.TradingPartner?.Code ?? string.Empty,
            PoNumber = order.PoNumber,
            CustomerAccountNumber = order.TenantPartnerAccount?.AccountNumber,
            OrderType = string.IsNullOrWhiteSpace(order.OrderType) ? "WrapAndLabel" : order.OrderType,
            DistributionCenterCode = order.DistributionCenterCode,
            OrderDate = order.OrderDate,
            RequestedShipDate = order.RequestedShipDate,
            RequestedDeliveryDate = order.RequestedDeliveryDate,
            ShippingMethod = order.ShippingMethod,
            Notes = order.Notes,
            Attn = order.Attn,
            LabelComments = MapLabelComments(order.LabelCommentsJson),
            Currency = ParseCurrency(order.Currency),
            ShipTo = MapAddress(order.ShipToJson),
            ShipFrom = MapAddress(order.ShipFromJson),
            BillTo = MapAddress(order.BillToJson),
            Lines = order.Lines
                .OrderBy(l => l.LineNumber)
                .Select(MapLine)
                .ToList()
        };
    }

    private static IReadOnlyList<string> MapLabelComments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            var comments = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return comments?.Where(c => !string.IsNullOrWhiteSpace(c)).ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static PurchaseOrderLine MapLine(OrderLine line)
    {
        return new PurchaseOrderLine
        {
            LineNumber = line.LineNumber,
            // The SPR item (CustomerItem) is the supplier's SKU; fall back to the internal SKU.
            PartnerSku = !string.IsNullOrWhiteSpace(line.VendorSku) ? line.VendorSku! : line.Sku,
            DealerSku = line.Sku,
            Upc = line.Upc,
            Description = line.Description,
            QuantityOrdered = (int)line.Quantity,
            UnitOfMeasure = ParseUom(line.UnitOfMeasure),
            UnitPrice = line.UnitPrice,
            Notes = line.Notes
        };
    }

    private static Address? MapAddress(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        CanonicalAddressInfo? info;
        try
        {
            info = JsonSerializer.Deserialize<CanonicalAddressInfo>(json, JsonOptions);
        }
        catch
        {
            return null;
        }

        if (info == null)
            return null;

        return new Address
        {
            // SPR PersonInfoShipTo/@FirstName is the consumer or ship-to company name.
            Name = !string.IsNullOrWhiteSpace(info.Company) ? info.Company : info.Name,
            AddressLine1 = info.Address1,
            AddressLine2 = info.Address2,
            AddressLine3 = info.Address3,
            City = info.City,
            State = info.State,
            PostalCode = info.PostalCode,
            Country = info.Country,
            Phone = info.Phone,
            Email = info.Email,
            // Commercial is the inverse of residential; null when the caller didn't specify.
            IsCommercialAddress = info.IsResidential.HasValue ? !info.IsResidential.Value : null
        };
    }

    private static CurrencyCode ParseCurrency(string? currency)
    {
        return Enum.TryParse<CurrencyCode>(currency, ignoreCase: true, out var parsed)
            ? parsed
            : CurrencyCode.USD;
    }

    private static UnitOfMeasure ParseUom(string? uom)
    {
        return uom?.Trim().ToUpperInvariant() switch
        {
            "EA" or "EACH" => UnitOfMeasure.Each,
            "CS" or "CASE" => UnitOfMeasure.Case,
            "PK" or "PACK" => UnitOfMeasure.Pack,
            "PL" or "PALLET" => UnitOfMeasure.Pallet,
            "LB" or "POUND" => UnitOfMeasure.Pound,
            "KG" or "KILOGRAM" => UnitOfMeasure.Kilogram,
            _ => UnitOfMeasure.Each
        };
    }
}
