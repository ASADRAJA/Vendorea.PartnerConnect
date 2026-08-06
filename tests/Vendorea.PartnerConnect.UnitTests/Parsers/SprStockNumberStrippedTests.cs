using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;

namespace Vendorea.PartnerConnect.UnitTests.Parsers;

/// <summary>
/// StockNumberStripped is the agreed price-to-content join key with M360. Roughly 4% of SPR stock
/// numbers carry a dash, slash, or dot that the content side never has, so those rows join on the
/// stripped form only - if the parser stops carrying it, the join silently drops them.
/// </summary>
public class SprStockNumberStrippedTests
{
    private const int ColumnCount = 104;
    private readonly SprPriceFeedParser _sut = new(Mock.Of<ILogger<SprPriceFeedParser>>());

    [Theory]
    [InlineData("MMM653-24VAD-B", "MMM65324VADB")]  // dash - the common case
    [InlineData("AVE08/888", "AVE08888")]           // slash
    [InlineData("PAC5181.00", "PAC518100")]         // dot
    public async Task ParseToCanonical_CarriesStrippedStockNumber_WhenItDiffersFromRaw(
        string stockNumber, string stripped)
    {
        var result = await ParseSingleItemAsync(stockNumber, stripped);

        var item = result.Items.Should().ContainSingle().Subject;
        item.PartnerSku.Should().Be(stockNumber);
        item.PartnerSkuStripped.Should().Be(stripped);
    }

    [Fact]
    public async Task ParseToCanonical_CarriesStrippedStockNumber_WhenIdenticalToRaw()
    {
        // The majority case: no punctuation, so both forms are the same string. It still has to be
        // populated - M360 joins on the stripped column for every row, not just punctuated ones.
        var result = await ParseSingleItemAsync("GJO11259", "GJO11259");

        result.Items.Should().ContainSingle()
            .Which.PartnerSkuStripped.Should().Be("GJO11259");
    }

    [Fact]
    public async Task ParseToCanonical_LeavesStrippedNull_WhenFeedOmitsIt()
    {
        // A blank column becomes null rather than "" so it never joins to an empty content value.
        var result = await ParseSingleItemAsync("GJO11259", string.Empty);

        result.Items.Should().ContainSingle()
            .Which.PartnerSkuStripped.Should().BeNull();
    }

    private async Task<SprParseFeedResult<Canonical.Models.PriceUpdate>> ParseSingleItemAsync(
        string stockNumber, string stockNumberStripped)
    {
        var columns = new string[ColumnCount];
        Array.Fill(columns, string.Empty);
        columns[0] = "I";                       // record type - Master Item
        columns[1] = stockNumber;               // column 2
        columns[2] = stockNumberStripped;       // column 3

        var csv = string.Join(Environment.NewLine,
            string.Join(",", Enumerable.Repeat("h", ColumnCount)),
            string.Join(",", columns));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await _sut.ParseToCanonicalAsync(
            stream, dealerId: 1, sourceDocumentId: "test", new SprConfiguration());
    }
}
