using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Infrastructure.Edi;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;

namespace Vendorea.PartnerConnect.UnitTests.Parsers;

/// <summary>
/// Conformance tests for the outbound EZPO4 generator against the real SPR schema.
/// </summary>
public class SprEzpo4GeneratorTests
{
    private static SprEzpo4Generator CreateGenerator() =>
        new(NullLogger<SprEzpo4Generator>.Instance);

    private static PurchaseOrder ValidOrder() => new()
    {
        PoNumber = "PO-2026-7788",
        OrderDate = new DateTime(2026, 6, 8),
        ShipTo = new Address
        {
            Name = "ACME Receiving",
            AddressLine1 = "123 Main St",
            City = "Atlanta",
            State = "GA",
            PostalCode = "30339",
            Country = "USA"
        },
        Lines = new List<PurchaseOrderLine>
        {
            new() { LineNumber = 1, PartnerSku = "TCO27900", DealerSku = "DLR-1", QuantityOrdered = 5, UnitOfMeasure = UnitOfMeasure.Each, UnitPrice = 78.21m },
            new() { LineNumber = 2, PartnerSku = "CCR1003", QuantityOrdered = 2, UnitOfMeasure = UnitOfMeasure.Case, UnitPrice = 8.46m }
        }
    };

    [Fact]
    public void Generate_ProducesOnePoPerFile_WithConformingStructure()
    {
        var result = CreateGenerator().Generate(ValidOrder(), "SPR", "9999999.99", "SPR");

        result.Success.Should().BeTrue();
        var doc = XDocument.Parse(result.XmlContent!);

        // One PO per file: a single <Order> root, no batching wrapper.
        doc.Root!.Name.LocalName.Should().Be("Order");

        // Our PO number goes in CustomerPONo; the inbound order carries no OrderNo (SPR assigns it).
        doc.Root.Attribute("CustomerPONo")!.Value.Should().Be("PO-2026-7788");
        doc.Root.Attribute("OrderNo").Should().BeNull();
        doc.Root.Attribute("BuyerOrganizationCode")!.Value.Should().Be("9999999.99");

        var orderLines = doc.Root.Element("OrderLines")!.Elements("OrderLine").ToList();
        orderLines.Should().HaveCount(2);
        orderLines[0].Element("Item")!.Attribute("CustomerItem")!.Value.Should().Be("TCO27900");
        orderLines[0].Element("LinePriceInfo")!.Attribute("UnitPrice")!.Value.Should().Be("78.21");
        orderLines[0].Element("OrderLineTranQuantity")!.Attribute("OrderedQty")!.Value.Should().Be("5");
    }

    [Fact]
    public void Generate_DefaultsOrderTypeTo03_WhenOrderTypeAbsent()
    {
        // ValidOrder() leaves OrderType at the canonical default (WrapAndLabel).
        var result = CreateGenerator().Generate(ValidOrder(), "SPR", "9999999.99", "SPR");

        result.Success.Should().BeTrue();
        XDocument.Parse(result.XmlContent!).Root!.Attribute("OrderType")!.Value.Should().Be("03");
    }

    [Theory]
    [InlineData("StockOrder", "01")]
    [InlineData("WrapAndLabel", "03")]
    [InlineData("DropShip", "04")]
    [InlineData("something-unknown", "03")]
    public void Generate_MapsOrderTypeToSprCode(string canonical, string expected)
    {
        var order = ValidOrder() with { OrderType = canonical };

        var result = CreateGenerator().Generate(order, "SPR", "9999999.99", "SPR");

        result.Success.Should().BeTrue();
        XDocument.Parse(result.XmlContent!).Root!.Attribute("OrderType")!.Value.Should().Be(expected);
    }

    [Fact]
    public void Generate_EmitsShipNode_ShipToExtras_ShipFrom_LineNotes_AndLabelFields()
    {
        var order = ValidOrder() with
        {
            OrderType = "WrapAndLabel",
            DistributionCenterCode = "8",
            Attn = "Receiving Dept",
            LabelComments = new List<string> { "Handle with care", "Fragile", "Gift" },
            ShipTo = new Address
            {
                Name = "Jane Customer",
                AddressLine1 = "1 Consumer Way",
                AddressLine3 = "Building C",
                City = "Reno",
                State = "NV",
                PostalCode = "89501",
                IsCommercialAddress = false
            },
            ShipFrom = new Address
            {
                Name = "ACME Merchant LLC",
                AddressLine1 = "500 Dealer Rd",
                City = "Atlanta",
                State = "GA",
                PostalCode = "30339"
            },
            Lines = new List<PurchaseOrderLine>
            {
                new() { LineNumber = 1, PartnerSku = "TCO27900", QuantityOrdered = 5, UnitOfMeasure = UnitOfMeasure.Each, UnitPrice = 78.21m, Notes = "Leave at dock" }
            }
        };

        var result = CreateGenerator().Generate(order, "SPR", "9999999.99", "SPR");

        result.Success.Should().BeTrue();
        var doc = XDocument.Parse(result.XmlContent!);
        var root = doc.Root!;

        root.Attribute("ShipNode")!.Value.Should().Be("8");

        var shipTo = root.Element("PersonInfoShipTo")!;
        shipTo.Attribute("AddressLine3")!.Value.Should().Be("Building C");
        shipTo.Attribute("IsCommercialAddress")!.Value.Should().Be("N");

        var contact = root.Element("PersonInfoContact")!;
        contact.Attribute("FirstName")!.Value.Should().Be("ACME Merchant LLC");
        contact.Attribute("ZipCode")!.Value.Should().Be("30339");

        var lineNote = root.Element("OrderLines")!.Element("OrderLine")!
            .Element("Notes")!.Element("Note")!;
        lineNote.Attribute("NoteText")!.Value.Should().Be("Leave at dock");

        var header = root.Element("Extn")!.Element("EXTNSprOrderHeaderList")!.Element("EXTNSprOrderHeader")!;
        header.Attribute("DealerAttn")!.Value.Should().Be("Receiving Dept");
        header.Attribute("LabelCmmnts1")!.Value.Should().Be("Handle with care");
        header.Attribute("LabelCmmnts2")!.Value.Should().Be("Fragile");
        header.Attribute("LabelCmmnts3")!.Value.Should().Be("Gift");
        header.Attribute("DealerComp")!.Value.Should().Be("Y");
    }

    [Fact]
    public async Task Generate_WithAllNewFields_ValidatesAgainstRealEzpo4Xsd()
    {
        var order = ValidOrder() with
        {
            OrderType = "DropShip",
            DistributionCenterCode = "16",
            Attn = "Front Desk",
            LabelComments = new List<string> { "Comment one", "Comment two" },
            ShipFrom = new Address
            {
                Name = "ACME Merchant LLC",
                AddressLine1 = "500 Dealer Rd",
                City = "Atlanta",
                State = "GA",
                PostalCode = "30339"
            },
            ShipTo = new Address
            {
                Name = "Jane Customer",
                AddressLine1 = "1 Consumer Way",
                AddressLine3 = "Apt 9",
                City = "Reno",
                State = "NV",
                PostalCode = "89501",
                IsCommercialAddress = true
            },
            Lines = new List<PurchaseOrderLine>
            {
                new() { LineNumber = 1, PartnerSku = "TCO27900", QuantityOrdered = 5, UnitOfMeasure = UnitOfMeasure.Each, UnitPrice = 78.21m, Notes = "Rush" }
            }
        };

        var result = CreateGenerator().Generate(order, "SPR", "9999999.99", "SPR");
        result.Success.Should().BeTrue();

        var schemaProvider = new XsdSchemaProvider(
            Options.Create(new XsdSchemaProviderOptions()),
            NullLogger<XsdSchemaProvider>.Instance);
        var validator = new XsdValidationService(schemaProvider, NullLogger<XsdValidationService>.Instance);

        var validation = await validator.ValidateAsync(result.XmlContent!, "EZPO4", "SPR");

        validation.IsValid.Should().BeTrue(
            because: "EZPO4 with all new fields must still conform to the real SPR schema: "
                + string.Join("; ", validation.Errors.Select(e => e.Message)));
    }

    [Fact]
    public async Task Generate_OutputValidatesAgainstRealEzpo4Xsd()
    {
        var result = CreateGenerator().Generate(ValidOrder(), "SPR", "9999999.99", "SPR");
        result.Success.Should().BeTrue();

        var schemaProvider = new XsdSchemaProvider(
            Options.Create(new XsdSchemaProviderOptions()),
            NullLogger<XsdSchemaProvider>.Instance);
        var validator = new XsdValidationService(schemaProvider, NullLogger<XsdValidationService>.Instance);

        var validation = await validator.ValidateAsync(result.XmlContent!, "EZPO4", "SPR");

        validation.IsValid.Should().BeTrue(
            because: "generated EZPO4 must conform to the real SPR schema: "
                + string.Join("; ", validation.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void Generate_WithoutShipTo_FailsWithActionableError()
    {
        var order = ValidOrder() with { ShipTo = null };

        var result = CreateGenerator().Generate(order, "SPR", "9999999.99", "SPR");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Ship-to"));
    }

    [Fact]
    public void SprConfiguration_XmlExchangeDefaultsTo50022_OtherFlowsUnchanged()
    {
        var config = new SprConfiguration();
        // 50022 is scoped to the SPR XML order-exchange transport only.
        config.SprXmlSftpPort.Should().Be(50022);
        // General SPR SFTP flows (price/inventory feeds) keep the standard default.
        config.SftpPort.Should().Be(22);
    }
}
