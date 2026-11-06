using System.Globalization;
using System.Text.Json;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Infrastructure.Amazon;

/// <summary>
/// Turns an Amazon Orders v2026-01-01 order into PartnerConnect's canonical snapshot.
/// </summary>
/// <remarks>
/// The only place in the solution that knows Amazon's field names. Everything above it works in
/// the canonical vocabulary, which is what keeps a second marketplace an adapter rather than a
/// rewrite.
///
/// v2026-01-01 nests what v0 kept flat — status lives at <c>fulfillment.fulfillmentStatus</c>,
/// the marketplace at <c>salesChannel.marketplaceId</c>, money at <c>proceeds.grandTotal</c> —
/// and money arrives as a decimal *string*, which is parsed as decimal and never as double.
/// </remarks>
public static class AmazonOrderMapper
{
    /// <summary>
    /// Amazon's fulfilment statuses, mapped to the canonical set.
    /// </summary>
    /// <remarks>
    /// <c>CANCELLED</c> has two Ls in this version; v0 spelled it <c>Canceled</c>. A mapper
    /// carrying the old spelling forward would classify every cancellation as Unknown and
    /// keep shipping orders the buyer had cancelled.
    /// </remarks>
    private static readonly Dictionary<string, SalesOrderStatus> StatusMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PENDING"] = SalesOrderStatus.Pending,
            ["PENDING_AVAILABILITY"] = SalesOrderStatus.Pending,
            ["UNSHIPPED"] = SalesOrderStatus.ReadyToFulfill,
            ["PARTIALLY_SHIPPED"] = SalesOrderStatus.PartiallyFulfilled,
            ["SHIPPED"] = SalesOrderStatus.Fulfilled,
            ["CANCELLED"] = SalesOrderStatus.Cancelled,
            ["UNFULFILLABLE"] = SalesOrderStatus.OnHold,
            ["INVOICE_UNCONFIRMED"] = SalesOrderStatus.OnHold
        };

    public static MarketplaceOrderSnapshot Map(JsonElement order)
    {
        var fulfillment = Child(order, "fulfillment");
        var statusRaw = String(fulfillment, "fulfillmentStatus");

        var proceeds = Child(order, "proceeds");
        var grandTotal = Child(proceeds, "grandTotal");

        var buyer = Child(order, "buyer");
        var address = Child(Child(order, "recipient"), "deliveryAddress");

        // Buyer data is absent on PENDING orders by Amazon's own design, and absent without the
        // BUYER/RECIPIENT roles. Both are recorded as "not available", never as an error.
        var buyerAvailable = buyer.HasValue || address.HasValue;

        return new MarketplaceOrderSnapshot
        {
            ExternalOrderId = String(order, "orderId") ?? string.Empty,
            MarketplaceId = String(Child(order, "salesChannel"), "marketplaceId"),

            Status = MapStatus(statusRaw),
            ExternalStatusRaw = statusRaw,

            // MERCHANT / AMAZON in this version; v0 said MFN / AFN. Anything unrecognised is
            // treated as merchant-fulfilled only if Amazon says so — never by default, because
            // confirming shipment on an Amazon-fulfilled order is an error we cannot take back.
            FulfillmentChannel =
                string.Equals(String(fulfillment, "fulfilledBy"), "AMAZON", StringComparison.OrdinalIgnoreCase)
                    ? FulfillmentChannel.AmazonFulfilled
                    : FulfillmentChannel.MerchantFulfilled,

            Programs = StringArray(order, "programs"),

            PurchaseDate = DateTimeOrDefault(order, "createdTime"),
            SourceLastUpdatedAt = DateTimeOrDefault(order, "lastUpdatedTime"),

            OrderTotalAmount = Decimal(grandTotal, "amount"),
            CurrencyCode = String(grandTotal, "currencyCode"),

            BuyerDataAvailable = buyerAvailable,
            BuyerEmailRef = String(buyer, "buyerEmail"),
            BuyerName = String(buyer, "buyerName"),
            ShipTo = MapAddress(address),

            Lines = MapLines(order)
        };
    }

    private static SalesOrderStatus MapStatus(string? raw)
    {
        if (raw is not null && StatusMap.TryGetValue(raw, out var mapped))
        {
            return mapped;
        }

        // Unknown, with the raw value preserved by the caller. Amazon adds statuses; guessing
        // which existing one a new value resembles is how an order gets shipped twice.
        return SalesOrderStatus.Unknown;
    }

    private static MarketplaceAddress? MapAddress(JsonElement? address)
    {
        if (address is null)
        {
            return null;
        }

        return new MarketplaceAddress(
            Name: String(address, "name"),
            Line1: String(address, "addressLine1"),
            // Amazon allows three address lines; the third is joined rather than dropped, because
            // a delivery address missing a line is a delivery that fails.
            Line2: JoinNonEmpty(String(address, "addressLine2"), String(address, "addressLine3")),
            City: String(address, "city"),
            StateOrRegion: String(address, "stateOrRegion"),
            PostalCode: String(address, "postalCode"),
            CountryCode: String(address, "countryCode"));
    }

    private static List<MarketplaceOrderLineSnapshot> MapLines(JsonElement order)
    {
        var lines = new List<MarketplaceOrderLineSnapshot>();

        if (!order.TryGetProperty("orderItems", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return lines;
        }

        foreach (var item in items.EnumerateArray())
        {
            var product = Child(item, "product");
            var itemProceeds = Child(item, "proceeds");
            var itemFulfillment = Child(item, "fulfillment");

            var breakdowns = Breakdowns(itemProceeds);
            var total = Child(itemProceeds, "proceedsTotal");

            lines.Add(new MarketplaceOrderLineSnapshot
            {
                ExternalLineId = String(item, "orderItemId") ?? string.Empty,
                SellerSku = String(product, "sellerSku"),
                Asin = String(product, "asin"),
                Title = String(product, "title"),

                QuantityOrdered = Int(item, "quantityOrdered"),
                QuantityShipped = Int(itemFulfillment, "quantityFulfilled"),

                ItemPrice = Decimal(total, "amount") ?? BreakdownAmount(breakdowns, "principal"),
                ItemTax = BreakdownAmount(breakdowns, "tax"),
                ShippingPrice = BreakdownAmount(breakdowns, "shipping"),
                PromotionDiscount = BreakdownAmount(breakdowns, "promotion"),

                CurrencyCode = String(total, "currencyCode")
                    ?? String(Child(Child(product, "price"), "unitPrice"), "currencyCode"),

                // The authoritative record. The four columns above are a convenience derived from
                // it, and they are null rather than guessed when a breakdown type is unfamiliar.
                ProceedsRawJson = itemProceeds?.GetRawText()
            });
        }

        return lines;
    }

    private static List<(string Type, decimal? Amount)> Breakdowns(JsonElement? proceeds)
    {
        var result = new List<(string, decimal?)>();

        if (proceeds is null ||
            !proceeds.Value.TryGetProperty("breakdowns", out var breakdowns) ||
            breakdowns.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var breakdown in breakdowns.EnumerateArray())
        {
            var type = String(breakdown, "type");
            if (type is not null)
            {
                result.Add((type, Decimal(Child(breakdown, "subtotal"), "amount")));
            }
        }

        return result;
    }

    /// <summary>
    /// Sums the breakdowns whose type mentions a given concept.
    /// </summary>
    /// <remarks>
    /// Matched loosely on purpose. Amazon's published reference types the breakdown's <c>type</c>
    /// as a bare string without enumerating its values, so an exact-match table would be a guess
    /// presented as a fact. A substring match covers the documented families, and anything it
    /// does not recognise is still preserved verbatim in <c>ProceedsRawJson</c> — nothing is
    /// lost, only left underived.
    ///
    /// **These four derived columns must be confirmed against a real seller response before
    /// Phase 4 publishes money to Merchant360.**
    /// </remarks>
    private static decimal? BreakdownAmount(List<(string Type, decimal? Amount)> breakdowns, string concept)
    {
        var matching = breakdowns
            .Where(b => b.Type.Contains(concept, StringComparison.OrdinalIgnoreCase) && b.Amount.HasValue)
            .Select(b => b.Amount!.Value)
            .ToList();

        return matching.Count == 0 ? null : matching.Sum();
    }

    // ---- JSON helpers. Every one of them tolerates absence: includedData opt-ins and PENDING
    // orders both mean whole blocks are legitimately missing. ----

    private static JsonElement? Child(JsonElement? parent, string name) =>
        parent is not null
        && parent.Value.ValueKind == JsonValueKind.Object
        && parent.Value.TryGetProperty(name, out var child)
        && child.ValueKind != JsonValueKind.Null
            ? child
            : null;

    private static string? String(JsonElement? parent, string name)
    {
        var child = Child(parent, name);
        return child?.ValueKind == JsonValueKind.String ? child.Value.GetString() : null;
    }

    private static int Int(JsonElement? parent, string name)
    {
        var child = Child(parent, name);
        return child?.ValueKind == JsonValueKind.Number && child.Value.TryGetInt32(out var value)
            ? value
            : 0;
    }

    /// <summary>
    /// Parses Amazon's decimal-as-string money.
    /// </summary>
    /// <remarks>
    /// Invariant culture, and decimal rather than double. Amazon sends money as a string
    /// precisely so it is not rounded in transit; parsing it into a float would undo that in the
    /// first line of our own code.
    /// </remarks>
    private static decimal? Decimal(JsonElement? parent, string name)
    {
        var child = Child(parent, name);
        if (child is null)
        {
            return null;
        }

        return child.Value.ValueKind switch
        {
            JsonValueKind.String when decimal.TryParse(
                child.Value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                => parsed,
            JsonValueKind.Number when child.Value.TryGetDecimal(out var number) => number,
            _ => null
        };
    }

    private static DateTime DateTimeOrDefault(JsonElement? parent, string name)
    {
        var text = String(parent, name);
        return DateTime.TryParse(
            text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : default;
    }

    private static IReadOnlyList<string> StringArray(JsonElement? parent, string name)
    {
        var child = Child(parent, name);
        if (child?.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return child.Value.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToList();
    }

    private static string? JoinNonEmpty(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first)) return string.IsNullOrWhiteSpace(second) ? null : second;
        return string.IsNullOrWhiteSpace(second) ? first : $"{first}, {second}";
    }
}
