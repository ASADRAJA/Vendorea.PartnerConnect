using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Parsers;

/// <summary>
/// Which cost the parser puts on the canonical price update.
/// </summary>
/// <remarks>
/// Written because the existing parser tests are hardcoded to a Windows path
/// (<c>C:\VsCodeProjects\...</c>) and <c>return</c> silently when the file is absent, so on any
/// other machine they pass without executing. That is how the parser came to read the fallback
/// cost family for as long as it did while the Merchant360 push path was found, fixed and pinned.
///
/// These build the CSV in memory, so they run everywhere and fail when the rule changes.
/// </remarks>
public class SprPriceFeedCostSelectionTests
{
    private const int ColumnCount = 104;

    // Column indices the parser reads, from SprPriceFeedParser.
    private const int StockNumber = 1;
    private const int StockNumberStripped = 2;
    private const int Description = 3;
    private const int ProductStatus = 4;
    private const int NetCostNonCcp = 77;    // CSTP1
    private const int NetCostCcp3 = 78;      // CSTP3
    private const int NetCostCcp4 = 79;      // CSTP4
    private const int PromoLevel1Cost = 86;  // PCSTC1
    private const int PriceCodeIdentifier = 101;

    private static Stream BuildFile(
        decimal contractCost, decimal nonCcp, decimal ccp3, decimal ccp4, string priceCode = "IR3")
    {
        var row = new string[ColumnCount];
        for (var i = 0; i < ColumnCount; i++)
        {
            row[i] = string.Empty;
        }

        row[0] = "I";                        // record type
        row[StockNumber] = "ICX90781294";
        row[StockNumberStripped] = "ICX90781294";
        row[Description] = "PAPER;ROLL;3\"X225;24PK";
        row[ProductStatus] = "U";
        row[NetCostNonCcp] = nonCcp.ToString("0.0000");
        row[NetCostCcp3] = ccp3.ToString("0.0000");
        row[NetCostCcp4] = ccp4.ToString("0.0000");
        row[PromoLevel1Cost] = contractCost.ToString("0.0000");
        row[PriceCodeIdentifier] = priceCode;

        var header = string.Join(',', Enumerable.Range(0, ColumnCount).Select(i => $"COL{i}"));
        var content = header + "\n" + string.Join(',', row) + "\n";

        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private static SprPriceFeedParser Parser() => new(NullLogger<SprPriceFeedParser>.Instance);

    private async Task<decimal> ResolvedCostAsync(
        decimal contractCost, decimal nonCcp, decimal ccp3, decimal ccp4,
        SprPricingTier tier = SprPricingTier.Standard)
    {
        await using var stream = BuildFile(contractCost, nonCcp, ccp3, ccp4);

        var result = await Parser().ParseToCanonicalAsync(
            stream, dealerId: 1, sourceDocumentId: "doc-1",
            new SprConfiguration { PricingTier = tier });

        result.Items.Should().ContainSingle("the fixture is one item");
        return result.Items.Single().Cost;
    }

    [Fact]
    public async Task The_canonical_cost_is_the_contract_cost_when_SPR_supplied_one()
    {
        // The real values for ICX90781294 in the 27 Oct 2026 file. Before the fix this returned
        // 118.38 — the programme cost — on every item carrying a contract.
        (await ResolvedCostAsync(contractCost: 97.16m, nonCcp: 118.38m, ccp3: 118.38m, ccp4: 118.38m))
            .Should().Be(97.16m);
    }

    [Fact]
    public async Task The_canonical_cost_falls_back_when_the_contract_cost_is_zero_filled()
    {
        (await ResolvedCostAsync(contractCost: 0m, nonCcp: 118.38m, ccp3: 110.00m, ccp4: 105.00m))
            .Should().Be(118.38m);
    }

    [Theory]
    [InlineData(SprPricingTier.Standard, 118.38)]
    [InlineData(SprPricingTier.Ccp3, 110.00)]
    [InlineData(SprPricingTier.Ccp4, 105.00)]
    public async Task The_configured_tier_selects_among_the_fallbacks(SprPricingTier tier, double expected)
    {
        (await ResolvedCostAsync(
                contractCost: 0m, nonCcp: 118.38m, ccp3: 110.00m, ccp4: 105.00m, tier: tier))
            .Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(SprPricingTier.Standard)]
    [InlineData(SprPricingTier.Ccp3)]
    [InlineData(SprPricingTier.Ccp4)]
    public async Task The_tier_is_irrelevant_when_a_contract_cost_exists(SprPricingTier tier)
    {
        // The dealer negotiated 97.16. Which CCP programme they are also on does not change that.
        (await ResolvedCostAsync(
                contractCost: 97.16m, nonCcp: 118.38m, ccp3: 110.00m, ccp4: 105.00m, tier: tier))
            .Should().Be(97.16m);
    }

    [Fact]
    public async Task A_contract_cost_with_no_price_code_is_still_used()
    {
        // 269 of 46,585 live rows carry a contract cost and no PRCID. The test is zero-fill, not
        // the presence of a code — a rule keyed on PRCID would send these to the fallback.
        await using var stream = BuildFile(
            contractCost: 97.16m, nonCcp: 118.38m, ccp3: 118.38m, ccp4: 118.38m, priceCode: "");

        var result = await Parser().ParseToCanonicalAsync(
            stream, 1, "doc-1", new SprConfiguration { PricingTier = SprPricingTier.Standard });

        result.Items.Single().Cost.Should().Be(97.16m);
    }

    [Fact]
    public async Task Four_decimal_places_survive_the_mapping()
    {
        (await ResolvedCostAsync(contractCost: 96.2400m, nonCcp: 118.3800m, ccp3: 118.3800m, ccp4: 118.3800m))
            .Should().Be(96.2400m);
    }
}
