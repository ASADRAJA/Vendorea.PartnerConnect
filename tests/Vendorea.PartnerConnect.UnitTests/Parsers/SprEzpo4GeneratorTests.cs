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
