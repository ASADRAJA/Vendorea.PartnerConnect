using FluentAssertions;
using Vendorea.PartnerConnect.Domain.Pricing;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests;

/// <summary>
/// The SPR dealer-cost rule.
/// </summary>
/// <remarks>
/// This is a pricing rule, so the tests are the specification. The values are taken from the live
/// 27 Oct 2026 file so a failure points at real money rather than an invented example.
/// </remarks>
public class SprDealerCostTests
{
    [Fact]
    public void A_contract_cost_beats_the_programme_cost()
    {
        // ICX90781294, PAPER;ROLL;3"X225;24PK, PRCID IR3 — the dealer pays 97.16, not 118.38.
        SprDealerCost.Resolve(
            contractCost: 97.16m,
            nonCcpCost: 118.38m, ccp3Cost: 118.38m, ccp4Cost: 118.38m,
            program: SprCcpProgram.NonCcp)
            .Should().Be(97.16m);
    }

    [Theory]
    [InlineData("ACRUMWV7AAH02", 109.99, 179.98)]   // SNX — a 38.9% gap
    [InlineData("AAG70-EP01-05", 28.36, 40.70)]     // IR3
    [InlineData("BOP16606", 124.00, 161.20)]        // QS3
    [InlineData("ABACOR3F", 96.97, 100.44)]         // CS0 — only 3.5%, but still the contract
    [InlineData("AAG8015005", 14.15, 15.03)]        // BO3
    public void Real_items_resolve_to_their_contract_cost(string stockNumber, double contract, double programme)
    {
        SprDealerCost.Resolve(
            contractCost: (decimal)contract,
            nonCcpCost: (decimal)programme, ccp3Cost: (decimal)programme, ccp4Cost: (decimal)programme,
            program: SprCcpProgram.NonCcp)
            .Should().Be((decimal)contract, $"{stockNumber} is under contract");
    }

    [Fact]
    public void A_zero_filled_contract_cost_falls_back_to_the_programme_cost()
    {
        // SPR's wording: "If the item does not have a promotional cost, these fields will be zero
        // filled. The dealer should then use CSTP1, 3 & 4."
        SprDealerCost.Resolve(
            contractCost: 0m,
            nonCcpCost: 118.38m, ccp3Cost: 110.00m, ccp4Cost: 105.00m,
            program: SprCcpProgram.NonCcp)
            .Should().Be(118.38m);
    }

    [Theory]
    [InlineData(SprCcpProgram.NonCcp, 118.38)]
    [InlineData(SprCcpProgram.Ccp3, 110.00)]
    [InlineData(SprCcpProgram.Ccp4, 105.00)]
    public void The_fallback_honours_the_dealers_CCP_programme(SprCcpProgram program, double expected)
    {
        // Invisible on every account seen so far, because all three columns carry the same value.
        // It stops being invisible the first time a CCP dealer is onboarded.
        SprDealerCost.Resolve(
            contractCost: 0m,
            nonCcpCost: 118.38m, ccp3Cost: 110.00m, ccp4Cost: 105.00m,
            program: program)
            .Should().Be((decimal)expected);
    }

    [Fact]
    public void The_programme_is_irrelevant_when_a_contract_cost_exists()
    {
        // A contract is negotiated with this dealer. Which CCP programme they are also on does not
        // change what they agreed to pay.
        foreach (var program in Enum.GetValues<SprCcpProgram>())
        {
            SprDealerCost.Resolve(
                contractCost: 97.16m,
                nonCcpCost: 118.38m, ccp3Cost: 110.00m, ccp4Cost: 105.00m,
                program: program)
                .Should().Be(97.16m);
        }
    }

    [Fact]
    public void A_contract_cost_above_the_programme_cost_is_still_the_contract_cost()
    {
        // Not seen in any live file — every contract cost is below programme — but the rule is
        // "the contract governs", not "the cheaper wins". Quietly taking the lower number would be
        // inventing a pricing policy SPR never stated.
        SprDealerCost.Resolve(
            contractCost: 130.00m,
            nonCcpCost: 118.38m, ccp3Cost: 118.38m, ccp4Cost: 118.38m,
            program: SprCcpProgram.NonCcp)
            .Should().Be(130.00m);
    }

    [Fact]
    public void A_negative_contract_cost_is_treated_as_absent()
    {
        // The signal is "greater than zero", so a nonsense negative falls back rather than
        // propagating into a credit.
        SprDealerCost.Resolve(
            contractCost: -5m,
            nonCcpCost: 118.38m, ccp3Cost: 118.38m, ccp4Cost: 118.38m,
            program: SprCcpProgram.NonCcp)
            .Should().Be(118.38m);
    }

    [Fact]
    public void Scale_is_preserved_to_four_places()
    {
        // The file carries four decimal places and money must not be rounded on the way through.
        SprDealerCost.Resolve(
            contractCost: 96.2400m,
            nonCcpCost: 118.3800m, ccp3Cost: 118.3800m, ccp4Cost: 118.3800m,
            program: SprCcpProgram.NonCcp)
            .Should().Be(96.2400m);
    }

    [Theory]
    [InlineData(97.16, true)]
    [InlineData(0, false)]
    public void Whether_an_item_is_under_contract_is_reportable(double contractCost, bool expected)
    {
        // An item on the fallback is not an error. A file where nothing carries a contract cost
        // would mean a lapsed agreement, and that is worth being able to see.
        SprDealerCost.HasContractCost((decimal)contractCost).Should().Be(expected);
    }
}
