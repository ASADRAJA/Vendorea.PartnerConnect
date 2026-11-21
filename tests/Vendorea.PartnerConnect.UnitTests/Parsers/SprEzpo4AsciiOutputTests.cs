using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Vendorea.PartnerConnect.Canonical.Enums;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;

namespace Vendorea.PartnerConnect.UnitTests.Parsers;

/// <summary>
/// The two orders SPR rejected on 2026-11-21, replayed end to end through the generator.
/// </summary>
/// <remarks>
/// WPO-20261121-0001 and WPO-20261121-0002 were refused with "XML Schema Validation Failed",
/// which was not the reason: SPR's nested report named an XML parser error at line 3, column 42,
/// the position of an em dash in the ship-to name. WPO-20261022-0001, structurally identical and
/// pure ASCII, was accepted on 2026-11-02. These tests pin the difference.
/// </remarks>
public class SprEzpo4AsciiOutputTests
{
    /// <summary>U+2014.</summary>
    private const string EmDash = "\u2014";

    private static SprEzpo4Generator CreateGenerator() =>
        new(NullLogger<SprEzpo4Generator>.Instance);

    /// <summary>The real order, as Merchant360 submitted it.</summary>
    private static PurchaseOrder RejectedOrder(string shipToName) => new()
    {
        PoNumber = "WPO-20261121-0001",
        OrderDate = new DateTime(2026, 11, 21),
        ShipTo = new Address
        {
            Name = shipToName,
            AddressLine1 = "2821 coral valley",
            City = "Leander",
            State = "TX",
            PostalCode = "78641",
            Country = "US"
        },
        Lines = new List<PurchaseOrderLine>
        {
            new() { LineNumber = 1, PartnerSku = "ZPE17601", QuantityOrdered = 1, UnitOfMeasure = UnitOfMeasure.Each, UnitPrice = 10.04m },
            new() { LineNumber = 2, PartnerSku = "STCUSB3AUB50CMB", QuantityOrdered = 1, UnitOfMeasure = UnitOfMeasure.Each, UnitPrice = 16.09m }
        }
    };

    private static bool IsPureAscii(string xml) => Encoding.UTF8.GetBytes(xml).All(b => b < 0x80);

    [Fact]
    public void The_rejected_order_now_generates_a_document_SPR_can_parse()
    {
        var order = RejectedOrder("Customer1 " + EmDash + " Attn: John Smith");

        var result = CreateGenerator().Generate(order, "SPR", "0033822.00", "SPR");

        result.Success.Should().BeTrue();
        IsPureAscii(result.XmlContent!).Should().BeTrue(
            "SPR's intake reads the file as a single-byte character set, so any byte above 0x7F fails it");
        result.XmlContent.Should().Contain("Customer1 - Attn: John Smith");
        result.XmlContent.Should().NotContain(EmDash);
    }

    [Fact]
    public void Every_byte_of_the_generated_document_is_below_0x80()
    {
        // Stated as bytes rather than characters, because that is the level SPR's parser failed
        // at: "Invalid byte 1 of 1-byte UTF-8 sequence".
        var order = RejectedOrder("Peña Supply Co. " + EmDash + " Attn: José");

        var result = CreateGenerator().Generate(order, "SPR", "0033822.00", "SPR");

        result.Success.Should().BeTrue();
        var offending = Encoding.UTF8.GetBytes(result.XmlContent!).Where(b => b >= 0x80).ToList();
        offending.Should().BeEmpty();
        result.XmlContent.Should().Contain("Pena Supply Co. - Attn: Jose");
    }

    [Fact]
    public void The_order_SPR_accepted_is_unchanged_by_the_fix()
    {
        // WPO-20261022-0001's ship-to name. A fix that altered an already-working document would
        // be a regression, not a fix.
        var order = RejectedOrder("John Smith");

        var result = CreateGenerator().Generate(order, "SPR", "0033822.00", "SPR");

        result.Success.Should().BeTrue();
        result.XmlContent.Should().Contain("FirstName=\"John Smith\"");
    }

    [Fact]
    public void A_ship_to_name_with_no_ascii_content_fails_locally_with_a_readable_error()
    {
        // FirstName is use="required". Sending it empty would earn another opaque rejection three
        // minutes later; refusing here names the order and the field immediately.
        var order = RejectedOrder("张三");

        var result = CreateGenerator().Generate(order, "SPR", "0033822.00", "SPR");

        result.Success.Should().BeFalse();
        result.XmlContent.Should().BeNullOrEmpty();
        result.Errors.Should().ContainSingle(e =>
            e.Contains("Ship-to name") && e.Contains("PersonInfoShipTo/@FirstName"));
    }

    [Fact]
    public void Folding_is_applied_before_truncation()
    {
        // An ellipsis becomes three characters, so folding after truncation could still overflow
        // the field. Ship-to name is capped at 64.
        var longName = new string('A', 62) + "…";

        var result = CreateGenerator().Generate(RejectedOrder(longName), "SPR", "0033822.00", "SPR");

        result.Success.Should().BeTrue();
        var doc = System.Xml.Linq.XDocument.Parse(result.XmlContent!);
        var name = doc.Descendants().First(e => e.Name.LocalName == "PersonInfoShipTo")
                      .Attribute("FirstName")!.Value;

        name.Length.Should().BeLessThanOrEqualTo(64);
        IsPureAscii(result.XmlContent!).Should().BeTrue();
    }

    [Fact]
    public void The_generated_document_still_parses_as_xml()
    {
        var order = RejectedOrder("Customer1 " + EmDash + " Attn: John Smith");

        var result = CreateGenerator().Generate(order, "SPR", "0033822.00", "SPR");

        var act = () => System.Xml.Linq.XDocument.Parse(result.XmlContent!);
        act.Should().NotThrow();
    }
}
