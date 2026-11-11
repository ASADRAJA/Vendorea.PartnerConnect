using System.Text.Json;
using FluentAssertions;
using Vendorea.PartnerConnect.Application.Services;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests;

/// <summary>
/// Payload redaction — test matrix 3.15.
/// </summary>
/// <remarks>
/// The archive must never become a second copy of anyone's personal data, so these tests are the
/// specification rather than illustration. The fixture is the real v2026-01-01 shape, nested as
/// Amazon actually nests it.
/// </remarks>
public class PayloadRedactorTests
{
    private const string OrderPayload = """
    {
      "orderId": "111-2223334-5556667",
      "createdTime": "2026-11-05T13:58:02Z",
      "salesChannel": { "channelName": "Amazon.com", "marketplaceId": "ATVPDKIKX0DER", "marketplaceName": "Amazon.com" },
      "fulfillment": { "fulfillmentStatus": "UNSHIPPED", "fulfilledBy": "MERCHANT" },
      "programs": ["PRIME"],
      "proceeds": { "grandTotal": { "amount": "48.97", "currencyCode": "USD" } },
      "buyer": {
        "buyerName": "J. Carter",
        "buyerEmail": "x1y2z3@marketplace.amazon.com",
        "buyerCompanyName": "Carter Supplies LLC",
        "buyerPurchaseOrderNumber": "PO-99812"
      },
      "recipient": {
        "deliveryAddress": {
          "name": "J. Carter",
          "companyName": "Carter Supplies LLC",
          "addressLine1": "414 W Main St",
          "addressLine2": "Apt 3",
          "addressLine3": "Rear entrance",
          "city": "Richmond",
          "districtOrCounty": "Henrico",
          "stateOrRegion": "VA",
          "postalCode": "23220",
          "countryCode": "US",
          "phone": "+1-804-555-0142",
          "extendedFields": { "streetName": "W Main St", "streetNumber": "414", "neighborhood": "Fan District" }
        }
      },
      "orderItems": [
        {
          "orderItemId": "58392014771234",
          "product": { "sellerSku": "VND-COPY-8511", "asin": "B07XYZ1234", "title": "Copy paper" },
          "quantityOrdered": 2
        }
      ]
    }
    """;

    private static JsonElement Redacted(string json)
        => JsonDocument.Parse(PayloadRedactor.Redact(json).Content).RootElement;

    [Fact]
    public void Buyer_identity_is_removed()
    {
        var buyer = Redacted(OrderPayload).GetProperty("buyer");

        buyer.GetProperty("buyerName").GetString().Should().Be(PayloadRedactor.Marker);
        buyer.GetProperty("buyerEmail").GetString().Should().Be(PayloadRedactor.Marker);
        buyer.GetProperty("buyerCompanyName").GetString().Should().Be(PayloadRedactor.Marker);
        buyer.GetProperty("buyerPurchaseOrderNumber").GetString().Should().Be(PayloadRedactor.Marker);
    }

    [Fact]
    public void The_street_address_is_removed()
    {
        var address = Redacted(OrderPayload).GetProperty("recipient").GetProperty("deliveryAddress");

        foreach (var field in new[] { "name", "companyName", "addressLine1", "addressLine2", "addressLine3", "postalCode", "phone" })
        {
            address.GetProperty(field).GetString().Should().Be(PayloadRedactor.Marker, $"{field} identifies a person");
        }

        var extended = address.GetProperty("extendedFields");
        extended.GetProperty("streetName").GetString().Should().Be(PayloadRedactor.Marker);
        extended.GetProperty("streetNumber").GetString().Should().Be(PayloadRedactor.Marker);
        extended.GetProperty("neighborhood").GetString().Should().Be(PayloadRedactor.Marker);
    }

    [Fact]
    public void Coarse_geography_is_kept()
    {
        var address = Redacted(OrderPayload).GetProperty("recipient").GetProperty("deliveryAddress");

        // Not identifying on its own, and the first thing anyone looks at for a tax, currency or
        // marketplace question.
        address.GetProperty("city").GetString().Should().Be("Richmond");
        address.GetProperty("stateOrRegion").GetString().Should().Be("VA");
        address.GetProperty("countryCode").GetString().Should().Be("US");
        address.GetProperty("districtOrCounty").GetString().Should().Be("Henrico");
    }

    [Fact]
    public void Everything_needed_for_diagnosis_survives()
    {
        var order = Redacted(OrderPayload);

        order.GetProperty("orderId").GetString().Should().Be("111-2223334-5556667");
        order.GetProperty("fulfillment").GetProperty("fulfillmentStatus").GetString().Should().Be("UNSHIPPED");
        order.GetProperty("salesChannel").GetProperty("marketplaceId").GetString().Should().Be("ATVPDKIKX0DER");
        order.GetProperty("proceeds").GetProperty("grandTotal").GetProperty("amount").GetString().Should().Be("48.97");
        order.GetProperty("programs")[0].GetString().Should().Be("PRIME");

        // The archive answers "what shape did this arrive in and what did it say?" — it can do
        // that without a buyer's street.
        var item = order.GetProperty("orderItems")[0];
        item.GetProperty("product").GetProperty("sellerSku").GetString().Should().Be("VND-COPY-8511");
        item.GetProperty("product").GetProperty("title").GetString().Should().Be("Copy paper");
        item.GetProperty("quantityOrdered").GetInt32().Should().Be(2);
    }

    [Fact]
    public void Compound_names_that_are_not_personal_survive()
    {
        var order = Redacted(OrderPayload);

        // `name` alone is the recipient's; channelName and marketplaceName are not.
        order.GetProperty("salesChannel").GetProperty("channelName").GetString().Should().Be("Amazon.com");
        order.GetProperty("salesChannel").GetProperty("marketplaceName").GetString().Should().Be("Amazon.com");
    }

    [Fact]
    public void What_was_removed_is_recorded()
    {
        var result = PayloadRedactor.Redact(OrderPayload);

        // "The address is missing" and "we redacted the address" look identical in the stored
        // document and mean entirely different things when working out why an order would not ship.
        result.RedactedPaths.Should().Contain("recipient.deliveryAddress.addressLine1");
        result.RedactedPaths.Should().Contain("buyer.buyerEmail");
        result.RedactedPaths.Should().NotContain("recipient.deliveryAddress.city");
    }

    [Fact]
    public void A_field_name_the_provider_has_not_invented_yet_is_still_caught()
    {
        // Matching is on property name, not path, precisely so this works. A path-based list
        // would have gone on reporting that it redacted nothing.
        var redacted = Redacted("""
        { "buyer": { "secondaryEmail": "x@y.com", "mobilePhoneNumber": "+1-555-0100" } }
        """).GetProperty("buyer");

        redacted.GetProperty("secondaryEmail").GetString().Should().Be(PayloadRedactor.Marker);
        redacted.GetProperty("mobilePhoneNumber").GetString().Should().Be(PayloadRedactor.Marker);
    }

    [Fact]
    public void A_field_that_moves_is_still_caught()
    {
        // v0 kept the address flat; v2026-01-01 nested it. A redactor pinned to either shape
        // would silently stop working when the provider changed it — Amazon just did.
        var flat = Redacted("""
        { "ShippingAddress": { "Name": "J. Carter", "AddressLine1": "414 W Main St", "City": "Richmond" } }
        """).GetProperty("ShippingAddress");

        flat.GetProperty("Name").GetString().Should().Be(PayloadRedactor.Marker);
        flat.GetProperty("AddressLine1").GetString().Should().Be(PayloadRedactor.Marker);
        flat.GetProperty("City").GetString().Should().Be("Richmond");
    }

    [Fact]
    public void Personal_data_nested_inside_an_array_is_caught()
    {
        var redacted = Redacted("""
        { "shipments": [ { "recipient": { "name": "A. Person", "city": "Leeds" } },
                         { "recipient": { "name": "B. Person", "city": "York" } } ] }
        """);

        var shipments = redacted.GetProperty("shipments");
        shipments[0].GetProperty("recipient").GetProperty("name").GetString().Should().Be(PayloadRedactor.Marker);
        shipments[1].GetProperty("recipient").GetProperty("name").GetString().Should().Be(PayloadRedactor.Marker);
        shipments[1].GetProperty("recipient").GetProperty("city").GetString().Should().Be("York");
    }

    [Fact]
    public void Array_paths_are_recorded_with_their_index()
    {
        var result = PayloadRedactor.Redact("""
        { "shipments": [ { "name": "A" }, { "name": "B" } ] }
        """);

        result.RedactedPaths.Should().BeEquivalentTo(new[] { "shipments[0].name", "shipments[1].name" });
    }

    [Fact]
    public void The_redacted_output_is_still_valid_json()
    {
        var act = () => JsonDocument.Parse(PayloadRedactor.Redact(OrderPayload).Content);

        act.Should().NotThrow();
    }

    [Fact]
    public void No_fragment_of_the_original_personal_data_survives_anywhere_in_the_output()
    {
        var content = PayloadRedactor.Redact(OrderPayload).Content;

        // The assertion that matters most: a search of the stored text finds none of it.
        foreach (var secret in new[]
                 {
                     "J. Carter", "x1y2z3@marketplace.amazon.com", "Carter Supplies LLC",
                     "414 W Main St", "Apt 3", "Rear entrance", "23220",
                     "+1-804-555-0142", "W Main St", "Fan District", "PO-99812"
                 })
        {
            content.Should().NotContain(secret);
        }
    }

    [Fact]
    public void An_unparseable_body_is_not_stored()
    {
        var result = PayloadRedactor.Redact("<html>503 Service Unavailable</html>");

        // Storing something we have not inspected would defeat the point. Only the fact that
        // there was something is kept.
        result.Content.Should().NotContain("html");
        result.Content.Should().Contain("unparseable");
        result.RedactedPaths.Should().BeEquivalentTo(new[] { "$" });
    }

    [Fact]
    public void A_payload_with_nothing_personal_is_passed_through_unchanged_in_substance()
    {
        var result = PayloadRedactor.Redact("""{"errors":[{"code":"QuotaExceeded","message":"Too many requests"}]}""");

        result.RedactedPaths.Should().BeEmpty();
        result.Content.Should().Contain("QuotaExceeded");
    }

    [Fact]
    public void The_marker_is_distinguishable_from_absent_and_from_empty()
    {
        var result = PayloadRedactor.Redact("""{"recipient":{"name":"X"}}""");

        PayloadRedactor.Marker.Should().NotBeEmpty();
        result.Content.Should().Contain(PayloadRedactor.Marker);

        // A removed field must not look like one the provider never sent, nor like one it sent
        // blank — those are three different facts.
        PayloadRedactor.Marker.Should().NotBe("");
        result.Content.Should().Contain("\"name\"");
    }
}
