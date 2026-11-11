using System.Text.Json;
using FluentAssertions;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Vendorea.PartnerConnect.Infrastructure.Amazon;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests;

/// <summary>
/// Amazon Orders v2026-01-01 to canonical snapshot — test matrix 3.1 and 3.3.
/// </summary>
/// <remarks>
/// Fixtures follow the published v2026-01-01 shape: status at
/// <c>fulfillment.fulfillmentStatus</c>, marketplace at <c>salesChannel.marketplaceId</c>, money
/// at <c>proceeds.grandTotal</c> as a decimal string. A fixture written to the v0 shape would
/// make every one of these pass while the real client returned nothing.
/// </remarks>
public class AmazonOrderMapperTests
{
    private static MarketplaceOrderSnapshotShim Map(string json)
        => new(AmazonOrderMapper.Map(JsonDocument.Parse(json).RootElement));

    /// <summary>Thin wrapper so each test reads as one assertion about one fact.</summary>
    private sealed record MarketplaceOrderSnapshotShim(
        Vendorea.PartnerConnect.Application.Interfaces.MarketplaceOrderSnapshot Value);

    private const string ShippedOrder = """
    {
      "orderId": "111-2223334-5556667",
      "createdTime": "2026-11-05T13:58:02Z",
      "lastUpdatedTime": "2026-11-05T14:02:11Z",
      "salesChannel": { "channelName": "Amazon.com", "marketplaceId": "ATVPDKIKX0DER" },
      "fulfillment": { "fulfillmentStatus": "SHIPPED", "fulfilledBy": "MERCHANT" },
      "programs": ["PRIME", "AMAZON_BUSINESS"],
      "proceeds": { "grandTotal": { "amount": "48.97", "currencyCode": "USD" } },
      "buyer": { "buyerName": "J. Carter", "buyerEmail": "x1y2z3@marketplace.amazon.com" },
      "recipient": {
        "deliveryAddress": {
          "name": "J. Carter", "addressLine1": "414 W Main St", "addressLine2": "Apt 3",
          "addressLine3": "Rear entrance", "city": "Richmond", "stateOrRegion": "VA",
          "postalCode": "23220", "countryCode": "US"
        }
      },
      "orderItems": [
        {
          "orderItemId": "58392014771234",
          "product": {
            "sellerSku": "VND-COPY-8511", "asin": "B07XYZ1234",
            "title": "Copy paper, 8.5x11, 500 sheets",
            "price": { "unitPrice": { "amount": "19.99", "currencyCode": "USD" } }
          },
          "quantityOrdered": 2,
          "fulfillment": { "quantityFulfilled": 2 },
          "proceeds": {
            "proceedsTotal": { "amount": "39.98", "currencyCode": "USD" },
            "breakdowns": [
              { "type": "Principal", "subtotal": { "amount": "39.98", "currencyCode": "USD" } },
              { "type": "Tax", "subtotal": { "amount": "2.40", "currencyCode": "USD" } },
              { "type": "ShippingCharge", "subtotal": { "amount": "6.59", "currencyCode": "USD" } },
              { "type": "PromotionDiscount", "subtotal": { "amount": "-2.00", "currencyCode": "USD" } }
            ]
          }
        }
      ]
    }
    """;

    [Fact]
    public void The_nested_v2026_fields_land_in_the_canonical_snapshot()
    {
        var order = Map(ShippedOrder).Value;

        order.ExternalOrderId.Should().Be("111-2223334-5556667");
        order.MarketplaceId.Should().Be("ATVPDKIKX0DER");
        order.Status.Should().Be(SalesOrderStatus.Fulfilled);
        order.ExternalStatusRaw.Should().Be("SHIPPED");
        order.FulfillmentChannel.Should().Be(FulfillmentChannel.MerchantFulfilled);
        order.PurchaseDate.Should().Be(new DateTime(2026, 11, 5, 13, 58, 2, DateTimeKind.Utc));
        order.SourceLastUpdatedAt.Should().Be(new DateTime(2026, 11, 5, 14, 2, 11, DateTimeKind.Utc));
    }

    [Fact]
    public void Money_arrives_as_decimal_not_double()
    {
        var order = Map(ShippedOrder).Value;

        order.OrderTotalAmount.Should().Be(48.97m);
        order.CurrencyCode.Should().Be("USD");

        // Amazon sends money as a string so it is not rounded in transit. Parsing it into a
        // float would undo that in the first line of our own code.
        order.OrderTotalAmount.Should().BeOfType(typeof(decimal));
    }

    [Fact]
    public void Programs_are_kept_verbatim_rather_than_turned_into_booleans()
    {
        var order = Map(ShippedOrder).Value;

        order.Programs.Should().BeEquivalentTo(new[] { "PRIME", "AMAZON_BUSINESS" });
    }

    [Fact]
    public void A_line_maps_its_product_quantities_and_proceeds()
    {
        var line = Map(ShippedOrder).Value.Lines.Single();

        line.ExternalLineId.Should().Be("58392014771234");
        line.SellerSku.Should().Be("VND-COPY-8511");
        line.Asin.Should().Be("B07XYZ1234");
        line.QuantityOrdered.Should().Be(2);
        line.QuantityShipped.Should().Be(2);
        line.ItemPrice.Should().Be(39.98m);
        line.ItemTax.Should().Be(2.40m);
        line.ShippingPrice.Should().Be(6.59m);
        line.PromotionDiscount.Should().Be(-2.00m);

        // The derived columns above are a convenience; this is the authoritative record.
        line.ProceedsRawJson.Should().Contain("ShippingCharge");
    }

    [Fact]
    public void An_unfamiliar_breakdown_type_is_preserved_but_not_guessed_at()
    {
        var order = Map("""
        {
          "orderId": "111-0000000-0000000",
          "orderItems": [{
            "orderItemId": "L1", "quantityOrdered": 1,
            "proceeds": { "breakdowns": [
              { "type": "SomeNewAmazonCharge", "subtotal": { "amount": "1.23", "currencyCode": "USD" } }
            ] }
          }]
        }
        """).Value;

        var line = order.Lines.Single();
        line.ItemTax.Should().BeNull();
        line.ShippingPrice.Should().BeNull();

        // Nothing is lost — only left underived.
        line.ProceedsRawJson.Should().Contain("SomeNewAmazonCharge");
    }

    [Theory]
    [InlineData("PENDING", SalesOrderStatus.Pending)]
    [InlineData("PENDING_AVAILABILITY", SalesOrderStatus.Pending)]
    [InlineData("UNSHIPPED", SalesOrderStatus.ReadyToFulfill)]
    [InlineData("PARTIALLY_SHIPPED", SalesOrderStatus.PartiallyFulfilled)]
    [InlineData("SHIPPED", SalesOrderStatus.Fulfilled)]
    [InlineData("CANCELLED", SalesOrderStatus.Cancelled)]
    [InlineData("UNFULFILLABLE", SalesOrderStatus.OnHold)]
    public void Every_documented_fulfillment_status_maps(string raw, SalesOrderStatus expected)
    {
        var order = Map($$"""
        { "orderId": "1", "fulfillment": { "fulfillmentStatus": "{{raw}}" } }
        """).Value;

        order.Status.Should().Be(expected);
        order.ExternalStatusRaw.Should().Be(raw);
    }

    [Fact]
    public void The_v0_spelling_of_Canceled_does_not_silently_map()
    {
        // v0 spelled it with one L. A mapper carrying the old spelling forward would classify
        // every cancellation as something else and keep shipping orders the buyer had cancelled.
        var order = Map("""
        { "orderId": "1", "fulfillment": { "fulfillmentStatus": "Canceled" } }
        """).Value;

        order.Status.Should().Be(SalesOrderStatus.Unknown);
        order.ExternalStatusRaw.Should().Be("Canceled");
    }

    [Fact]
    public void An_unrecognised_status_becomes_Unknown_and_keeps_its_text()
    {
        var order = Map("""
        { "orderId": "1", "fulfillment": { "fulfillmentStatus": "SOME_FUTURE_STATUS" } }
        """).Value;

        order.Status.Should().Be(SalesOrderStatus.Unknown);
        order.ExternalStatusRaw.Should().Be("SOME_FUTURE_STATUS");
    }

    [Fact]
    public void An_Amazon_fulfilled_order_is_recognised()
    {
        Map("""
        { "orderId": "1", "fulfillment": { "fulfilledBy": "AMAZON" } }
        """).Value.FulfillmentChannel.Should().Be(FulfillmentChannel.AmazonFulfilled);
    }

    [Fact]
    public void A_pending_order_without_buyer_data_maps_without_error()
    {
        // Matrix 3.3. Amazon withholds buyer data on PENDING orders by design.
        var order = Map("""
        {
          "orderId": "111-9999999-9999999",
          "createdTime": "2026-11-05T13:58:02Z",
          "lastUpdatedTime": "2026-11-05T13:58:02Z",
          "fulfillment": { "fulfillmentStatus": "PENDING", "fulfilledBy": "MERCHANT" },
          "orderItems": [{ "orderItemId": "L1", "quantityOrdered": 1 }]
        }
        """).Value;

        order.Status.Should().Be(SalesOrderStatus.Pending);
        order.BuyerDataAvailable.Should().BeFalse();
        order.BuyerName.Should().BeNull();
        order.ShipTo.Should().BeNull();

        // Absence is a normal state, not an error — and the order still carries its lines.
        order.Lines.Should().ContainSingle();
    }

    [Fact]
    public void An_order_missing_every_optional_block_still_maps()
    {
        // What arrives when no includedData role has been granted yet.
        var order = Map("""{ "orderId": "111-1111111-1111111" }""").Value;

        order.ExternalOrderId.Should().Be("111-1111111-1111111");
        order.OrderTotalAmount.Should().BeNull();
        order.Programs.Should().BeEmpty();
        order.Lines.Should().BeEmpty();
        order.Status.Should().Be(SalesOrderStatus.Unknown);
    }

    [Fact]
    public void A_third_address_line_is_joined_rather_than_dropped()
    {
        var address = Map(ShippedOrder).Value.ShipTo!;

        address.Line1.Should().Be("414 W Main St");

        // A delivery address missing a line is a delivery that fails.
        address.Line2.Should().Be("Apt 3, Rear entrance");
        address.CountryCode.Should().Be("US");
    }

    [Fact]
    public void A_multi_line_order_keeps_every_line()
    {
        var order = Map("""
        {
          "orderId": "111-2222222-2222222",
          "orderItems": [
            { "orderItemId": "L1", "quantityOrdered": 1, "product": { "sellerSku": "A" } },
            { "orderItemId": "L2", "quantityOrdered": 3, "product": { "sellerSku": "B" } },
            { "orderItemId": "L3", "quantityOrdered": 2, "product": { "sellerSku": "C" } }
          ]
        }
        """).Value;

        order.Lines.Should().HaveCount(3);
        order.Lines.Select(l => l.SellerSku).Should().BeEquivalentTo(new[] { "A", "B", "C" });
        order.Lines.Sum(l => l.QuantityOrdered).Should().Be(6);
    }

    [Fact]
    public void A_non_USD_order_keeps_its_own_currency_and_scale()
    {
        var order = Map("""
        {
          "orderId": "250-1234567-8901234",
          "proceeds": { "grandTotal": { "amount": "12500", "currencyCode": "JPY" } }
        }
        """).Value;

        // JPY has no minor unit. The scale is the currency's business, not the parser's.
        order.OrderTotalAmount.Should().Be(12500m);
        order.CurrencyCode.Should().Be("JPY");
    }

    [Fact]
    public void A_timestamp_with_an_offset_is_normalised_to_UTC()
    {
        var order = Map("""
        { "orderId": "1", "createdTime": "2026-11-05T09:58:02-04:00" }
        """).Value;

        order.PurchaseDate.Should().Be(new DateTime(2026, 11, 5, 13, 58, 2, DateTimeKind.Utc));
        order.PurchaseDate.Kind.Should().Be(DateTimeKind.Utc);
    }
}
