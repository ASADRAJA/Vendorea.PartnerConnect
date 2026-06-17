using FluentAssertions;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Parsers;

public class SprEzohInventoryParserTests
{
    // Build fixed-width records matching the SPRSTATUS layout (RECTYP 2, ITEMNO 15, ASTAT 1, UOM 2,
    // then 6-digit quantity per DC).
    private static string I1(params int[] dcs) =>
        "I1" + new string(' ', 15) + string.Concat(dcs.Select(d => d.ToString("D3")));

    private static string Q1(string item, char status, string uom, params int[] qtys) =>
        "Q1" + item.PadRight(15) + status + uom.PadRight(2) + string.Concat(qtys.Select(q => q.ToString("D6")));

    [Fact]
    public void Parse_ReadsDcListAndQuantities()
    {
        var content = string.Join("\n",
            "H0999999999 EZONHAND2",
            I1(1, 2, 3),
            Q1("SAN64329", 'A', "DZ", 34, 40, 46));

        var result = SprEzohInventoryParser.Parse(content);

        result.DcNumbers.Should().Equal(1, 2, 3);
        result.Items.Should().ContainSingle();

        var item = result.Items[0];
        item.ItemNumber.Should().Be("SAN64329");
        item.Status.Should().Be('A');
        item.UnitOfMeasure.Should().Be("DZ");
        item.TotalQuantity.Should().Be(120);
        item.Quantities.Should().Equal(
            new SprDcQuantity(1, 34), new SprDcQuantity(2, 40), new SprDcQuantity(3, 46));
    }

    [Fact]
    public void Parse_OmitsZeroQuantityDcs()
    {
        var content = string.Join("\n",
            I1(1, 2, 3),
            Q1("ITEM1", 'A', "EA", 34, 0, 46));

        var result = SprEzohInventoryParser.Parse(content);

        result.Items[0].Quantities.Should().Equal(
            new SprDcQuantity(1, 34), new SprDcQuantity(3, 46));
        result.Items[0].TotalQuantity.Should().Be(80);
    }

    [Fact]
    public void Parse_StripsSpecialCharsFromItemNumber()
    {
        var content = string.Join("\n", I1(31), Q1("AAG200-0106", 'A', "EA", 128));

        var result = SprEzohInventoryParser.Parse(content);

        result.Items[0].ItemNumber.Should().Be("AAG200-0106");
        result.Items[0].StrippedItemNumber.Should().Be("AAG2000106");
    }

    [Fact]
    public void Parse_HandlesTruncatedRecord_StopsAtAvailableColumns()
    {
        // I1 declares 3 DCs but the Q1 only carries 2 quantity columns (trailing zero DCs omitted).
        var content = string.Join("\n", I1(1, 2, 3), Q1("ITEM2", 'A', "EA", 5, 9));

        var result = SprEzohInventoryParser.Parse(content);

        result.Items[0].Quantities.Should().Equal(
            new SprDcQuantity(1, 5), new SprDcQuantity(2, 9));
    }

    [Theory]
    [InlineData('A')]
    [InlineData('D')]
    [InlineData('X')]
    public void Parse_PreservesStatusFlag(char status)
    {
        var content = string.Join("\n", I1(1), Q1("ITEM3", status, "EA", 7));

        var result = SprEzohInventoryParser.Parse(content);

        result.Items[0].Status.Should().Be(status);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsNoItems()
    {
        var result = SprEzohInventoryParser.Parse("");
        result.Items.Should().BeEmpty();
        result.DcNumbers.Should().BeEmpty();
    }
}
