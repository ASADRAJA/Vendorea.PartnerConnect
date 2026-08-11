using System.Reflection;
using FluentAssertions;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.UnitTests.Services;

/// <summary>
/// The cost sent to M360 is the dealer's contract price (PromoLevel1Cost), not the undiscounted
/// reference (NetCostNonCcp). Reading the reference overstated cost by ~24% on average and by up to
/// 18x on individual items, which understates margin everywhere downstream - so the selection rule
/// is pinned here rather than left to the mapping.
/// </summary>
public class SprEffectiveCostTests
{
    private static decimal ResolveEffectiveCost(SprPriceRecord record) =>
        (decimal)typeof(PriceFeedService)
            .GetMethod("ResolveEffectiveCost", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { record })!;

    [Fact]
    public void PrefersPromoCost_WhenAPricingProgramApplies()
    {
        // HONPLWMH66LS1 from the August file - verified at 19.99 in SPR's backend.
        var record = new SprPriceRecord { NetCostNonCcp = 358.80m, PromoLevel1Cost = 19.99m };

        ResolveEffectiveCost(record).Should().Be(19.99m);
    }

    [Fact]
    public void FallsBackToNetCost_WhenNoProgramApplies()
    {
        // Items with no pricing program carry a zero promo; the reference cost is then the real
        // price. ADEAKB630SBTAA - verified at 50.21 in SPR's backend.
        var record = new SprPriceRecord { NetCostNonCcp = 50.21m, PromoLevel1Cost = 0m };

        ResolveEffectiveCost(record).Should().Be(50.21m);
    }

    [Fact]
    public void UsesPromoCost_WhenItEqualsNetCost()
    {
        // The common no-program shape: SPR restates the same figure in both columns rather than
        // zeroing the promo. Either branch gives the same answer, which is the point.
        var record = new SprPriceRecord { NetCostNonCcp = 100.25m, PromoLevel1Cost = 100.25m };

        ResolveEffectiveCost(record).Should().Be(100.25m);
    }

    [Fact]
    public void IgnoresPromoLevels2And3()
    {
        // Levels 2 and 3 are byte-identical duplicates of level 1 in every file examined - they are
        // not quantity breaks. If SPR ever diverges them, this test documents that we read level 1.
        var record = new SprPriceRecord
        {
            NetCostNonCcp = 330.17m,
            PromoLevel1Cost = 250.77m,
            PromoLevel2Cost = 999.99m,
            PromoLevel3Cost = 111.11m
        };

        ResolveEffectiveCost(record).Should().Be(250.77m);
    }

    [Fact]
    public void NeverReturnsTheReferenceCost_WhenADiscountExists()
    {
        // Guards the regression that matters: sending column 78 where a program is active.
        var record = new SprPriceRecord { NetCostNonCcp = 2252.99m, PromoLevel1Cost = 1953.39m };

        ResolveEffectiveCost(record).Should().NotBe(2252.99m);
    }
}
