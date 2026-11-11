using System.Text.Json;
using FluentAssertions;
using Vendorea.PartnerConnect.Api.Controllers.V1;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Xunit;

namespace Vendorea.PartnerConnect.IntegrationTests.ApiTests;

/// <summary>
/// The sales-order read contract — test matrix 3.11.
/// </summary>
/// <remarks>
/// The mapper is where the published contract is honoured or broken, so it is asserted directly
/// rather than through an HTTP round trip. The property that matters most is negative: no
/// response may carry buyer identity or address.
/// </remarks>
public class SalesOrderMapperTests
{
    private static SalesOrder Order() => new()
    {
        Id = Guid.Parse("9b2d7e10-33af-4c58-a0e1-5d6f8b4c2a71"),
        IntegrationConnectionId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        TenantId = 7,
        ExternalOrderId = "111-2223334-5556667",
        MarketplaceId = "ATVPDKIKX0DER",
        Status = SalesOrderStatus.ReadyToFulfill,
        ExternalStatusRaw = "UNSHIPPED",
        FulfillmentChannel = FulfillmentChannel.MerchantFulfilled,
        MappingStatus = MappingStatus.Mapped,
        ProgramsRawJson = """["PRIME","AMAZON_BUSINESS"]""",
        PurchaseDate = new DateTime(2026, 11, 5, 13, 58, 2, DateTimeKind.Utc),
        SourceLastUpdatedAt = new DateTime(2026, 11, 5, 14, 2, 11, DateTimeKind.Utc),
        SourceVersion = "3",
        OrderTotalAmount = 48.97m,
        CurrencyCode = "USD",
        BuyerDataAvailable = true,

        // Present on the row, and must not reach the wire.
        BuyerName = "J. Carter",
        BuyerEmailRef = "x1y2z3@marketplace.amazon.com",
        ShipToName = "J. Carter",
        ShipToLine1 = "414 W Main St",
        ShipToPostalCode = "23220",

        FirstSeenAt = new DateTime(2026, 11, 5, 14, 0, 0, DateTimeKind.Utc),
        LastSyncedAt = new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc),
        LastProcessingResult = SalesOrderProcessingResult.Processed,

        Connection = new IntegrationConnection
        {
            Provider = new IntegrationProvider { Code = "AMAZON_SP", Name = "Amazon" }
        },

        Lines =
        {
            new SalesOrderLine
            {
                ExternalLineId = "58392014771234",
                SellerSku = "VND-COPY-8511",
                Asin = "B07XYZ1234",
                Title = "Copy paper, 8.5x11",
                QuantityOrdered = 2,
                QuantityShipped = 0,
                MappingStatus = MappingStatus.Mapped,
                M360CatalogItemId = "ITEM-44821",
                ItemPrice = 39.98m,
                ItemTax = 2.40m,
                CurrencyCode = "USD",
                ProceedsRawJson = """{"type":"Principal","charges":[{"type":"Principal","amount":"39.98"}]}"""
            }
        }
    };

    private static string Serialize(object dto) => JsonSerializer.Serialize(dto);

    [Fact]
    public void The_contract_fields_are_populated()
    {
        var dto = SalesOrderMapper.Map(Order());

        dto.PcSalesOrderId.Should().Be(Guid.Parse("9b2d7e10-33af-4c58-a0e1-5d6f8b4c2a71"));
        dto.Provider.Should().Be("AMAZON_SP");
        dto.ExternalOrderId.Should().Be("111-2223334-5556667");
        dto.MarketplaceId.Should().Be("ATVPDKIKX0DER");
        dto.Status.Should().Be("ReadyToFulfill");
        dto.ExternalStatusRaw.Should().Be("UNSHIPPED");
        dto.FulfillmentChannel.Should().Be("MerchantFulfilled");
        dto.MappingStatus.Should().Be("Mapped");
        dto.SourceVersion.Should().Be("3");
        dto.BuyerDataAvailable.Should().BeTrue();
    }

    [Fact]
    public void No_response_carries_buyer_identity_or_address()
    {
        var json = Serialize(SalesOrderMapper.MapDetail(Order()));

        // The order row holds these because a merchant-fulfilled order cannot ship without them.
        // This API is a read surface for operators and for Merchant360's views, and an endpoint
        // that returns PII because it happened to be in the row is how PII reaches a log, a cache
        // or a screenshot.
        foreach (var personal in new[]
                 {
                     "J. Carter", "x1y2z3@marketplace.amazon.com", "414 W Main St", "23220"
                 })
        {
            json.Should().NotContain(personal);
        }

        // Availability is reported without disclosure.
        json.Should().Contain("BuyerDataAvailable");
    }

    [Fact]
    public void Money_is_a_decimal_string_with_its_currency()
    {
        var dto = SalesOrderMapper.Map(Order());

        // A string, not a number: a JSON number is a double to most parsers, and a consumer that
        // reads 48.97 as a float has lost the value before doing anything with it.
        dto.OrderTotal!.Amount.Should().Be("48.97");
        dto.OrderTotal.Currency.Should().Be("USD");

        Serialize(dto).Should().Contain("\"Amount\":\"48.97\"");
    }

    [Fact]
    public void Money_keeps_its_scale_and_ignores_the_server_locale()
    {
        var order = Order();
        order.OrderTotalAmount = 1234.5678m;

        SalesOrderMapper.Map(order).OrderTotal!.Amount.Should().Be("1234.5678");
    }

    [Fact]
    public void A_missing_amount_is_null_rather_than_zero()
    {
        var order = Order();
        order.OrderTotalAmount = null;

        // Without the PROCEEDS role an order ingests with no monetary values at all. Reporting
        // that as "0.00" would be a number nobody sent and a total nobody owes.
        SalesOrderMapper.Map(order).OrderTotal.Should().BeNull();
    }

    [Fact]
    public void Programs_are_an_array_not_booleans()
    {
        var dto = SalesOrderMapper.Map(Order());

        dto.ProgramsRaw.Should().BeEquivalentTo(new[] { "PRIME", "AMAZON_BUSINESS" });

        // No isPrime boolean crosses the wire. Amazon adds programmes, and a boolean per
        // programme would answer false for every one that did not exist when it was added.
        Serialize(dto).Should().NotContain("isPrime");
    }

    [Fact]
    public void Unparseable_stored_programs_do_not_fail_the_read()
    {
        var order = Order();
        order.ProgramsRawJson = "{not json";

        // Stored JSON that will not parse is a data problem, not a reason to refuse the order.
        // An empty list is honest: we cannot say what the programmes were.
        SalesOrderMapper.Map(order).ProgramsRaw.Should().BeEmpty();
    }

    [Fact]
    public void Proceeds_are_emitted_as_an_object_not_a_quoted_string()
    {
        var json = Serialize(SalesOrderMapper.MapDetail(Order()));

        // So a consumer reads JSON rather than parsing it twice.
        json.Should().Contain("\"ProceedsRaw\":{");
        json.Should().NotContain("\"ProceedsRaw\":\"{");
    }

    [Fact]
    public void Detail_carries_lines_and_sync_metadata()
    {
        var dto = SalesOrderMapper.MapDetail(Order());

        var line = dto.Lines.Should().ContainSingle().Subject;
        line.ExternalLineId.Should().Be("58392014771234");
        line.SellerSku.Should().Be("VND-COPY-8511");
        line.QuantityOrdered.Should().Be(2);
        line.M360CatalogItemId.Should().Be("ITEM-44821");
        line.ItemPrice!.Amount.Should().Be("39.98");
        line.ItemTax!.Amount.Should().Be("2.40");

        dto.SyncMetadata.FirstSeenAt.Should().Be(new DateTime(2026, 11, 5, 14, 0, 0, DateTimeKind.Utc));
        dto.SyncMetadata.LastSyncedAt.Should().Be(new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc));
        dto.SyncMetadata.LastProcessingResult.Should().Be("Processed");
    }

    [Fact]
    public void An_unmapped_line_reports_no_catalogue_item()
    {
        var order = Order();
        order.Lines.Single().MappingStatus = MappingStatus.UnmappedProduct;
        order.Lines.Single().M360CatalogItemId = null;

        var line = SalesOrderMapper.MapDetail(order).Lines.Single();

        // Null, never a placeholder. PartnerConnect does not invent a product identity.
        line.M360CatalogItemId.Should().BeNull();
        line.MappingStatus.Should().Be("UnmappedProduct");
    }

    [Fact]
    public void An_unrecognised_provider_status_surfaces_as_Unknown_with_its_raw_text()
    {
        var order = Order();
        order.Status = SalesOrderStatus.Unknown;
        order.ExternalStatusRaw = "SOME_NEW_AMAZON_STATUS";

        var dto = SalesOrderMapper.Map(order);

        // The consumer is told plainly that we could not map it, and told what it actually said.
        dto.Status.Should().Be("Unknown");
        dto.ExternalStatusRaw.Should().Be("SOME_NEW_AMAZON_STATUS");
    }

    [Fact]
    public void A_detail_response_with_no_provider_loaded_does_not_throw()
    {
        var order = Order();
        order.Connection = null;

        // The list query includes the connection; a caller that forgot would otherwise get a
        // null-reference failure rather than an obviously empty field.
        SalesOrderMapper.MapDetail(order).Provider.Should().BeEmpty();
    }
}
