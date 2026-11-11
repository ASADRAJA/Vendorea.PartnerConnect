using System.Text.Json;
using System.Text.Json.Nodes;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Maps sales orders onto the published contract.
/// </summary>
/// <remarks>
/// Static and separate from the controller so it can be tested directly — the mapping is where
/// the contract is actually honoured or broken, and that is worth asserting without an HTTP
/// round trip.
///
/// Nothing here projects buyer identity or address. That is the point of having one mapper
/// rather than inline projections: a future field cannot be added to a response without passing
/// through this file.
/// </remarks>
public static class SalesOrderMapper
{
    public static SalesOrderDto Map(SalesOrder order) => Fill(new SalesOrderDto(), order);

    public static SalesOrderDetailDto MapDetail(SalesOrder order)
    {
        var dto = Fill(new SalesOrderDetailDto(), order);

        dto.Lines = order.Lines
            .OrderBy(l => l.ExternalLineId)
            .Select(MapLine)
            .ToList();

        dto.SyncMetadata = new SalesOrderSyncMetadataDto
        {
            FirstSeenAt = order.FirstSeenAt,
            LastSyncedAt = order.LastSyncedAt,
            LastEventId = order.LastEventId,
            LastProcessingResult = order.LastProcessingResult?.ToString()
        };

        return dto;
    }

    private static T Fill<T>(T dto, SalesOrder order) where T : SalesOrderDto
    {
        dto.PcSalesOrderId = order.Id;
        dto.IntegrationConnectionId = order.IntegrationConnectionId;
        dto.Provider = order.Connection?.Provider?.Code ?? string.Empty;
        dto.ExternalOrderId = order.ExternalOrderId;
        dto.MarketplaceId = order.MarketplaceId;
        dto.Status = order.Status.ToString();
        dto.ExternalStatusRaw = order.ExternalStatusRaw;
        dto.ExternalFulfillmentStatusRaw = order.ExternalFulfillmentStatusRaw;
        dto.FulfillmentChannel = order.FulfillmentChannel.ToString();
        dto.MappingStatus = order.MappingStatus.ToString();
        dto.ProgramsRaw = ParseStringArray(order.ProgramsRawJson);
        dto.PurchaseDate = order.PurchaseDate;
        dto.SourceLastUpdatedAt = order.SourceLastUpdatedAt;
        dto.SourceVersion = order.SourceVersion;
        dto.OrderTotal = MoneyDto.From(order.OrderTotalAmount, order.CurrencyCode);
        dto.BuyerDataAvailable = order.BuyerDataAvailable;
        return dto;
    }

    private static SalesOrderLineDto MapLine(SalesOrderLine line) => new()
    {
        ExternalLineId = line.ExternalLineId,
        SellerSku = line.SellerSku,
        Asin = line.Asin,
        Title = line.Title,
        QuantityOrdered = line.QuantityOrdered,
        QuantityShipped = line.QuantityShipped,
        MappingStatus = line.MappingStatus.ToString(),
        M360CatalogItemId = line.M360CatalogItemId,
        ItemPrice = MoneyDto.From(line.ItemPrice, line.CurrencyCode),
        ItemTax = MoneyDto.From(line.ItemTax, line.CurrencyCode),
        ShippingPrice = MoneyDto.From(line.ShippingPrice, line.CurrencyCode),
        PromotionDiscount = MoneyDto.From(line.PromotionDiscount, line.CurrencyCode),
        ProceedsRaw = ParseNode(line.ProceedsRawJson)
    };

    private static List<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            // Stored JSON that will not parse is a data problem, not a reason to fail the read.
            // An empty list is honest: we cannot say what the programmes were.
            return new List<string>();
        }
    }

    private static JsonNode? ParseNode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
