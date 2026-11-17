namespace Vendorea.PartnerConnect.Domain.Pricing;

/// <summary>Which SPR buying program a dealer is on, selecting the fallback cost column.</summary>
public enum SprCcpProgram
{
    /// <summary>Not on a CCP program — the dealer cost falls back to CSTP1.</summary>
    NonCcp,

    /// <summary>SPR CCP-3 — falls back to CSTP3.</summary>
    Ccp3,

    /// <summary>SPR CCP-4 — falls back to CSTP4.</summary>
    Ccp4
}

/// <summary>
/// The one place that decides what a dealer actually pays for an SPR item.
/// </summary>
/// <remarks>
/// SPR's Enhanced price file carries dealer cost in two families, and they mean different things.
///
/// <c>PCSTC1/2/3</c> (our <c>PromoLevel1Cost</c> and siblings) hold the cost under a specific
/// negotiated contract or promotional programme, named by <c>PRCID</c>. <c>CSTP1/3/4</c> (our
/// <c>NetCostNonCcp</c>, <c>NetCostCcp3</c>, <c>NetCostCcp4</c>) hold the programme-level cost for
/// a non-CCP, CCP-3 or CCP-4 dealer respectively.
///
/// **Contract beats programme.** SPR's layout document (Enhanced Version 3.1, 2021-10-01) states
/// it directly: "The dealer cost will be in the PCSTC1, 2 or 3 field. If the item does not have a
/// promotional cost, these fields will be zero filled. The dealer should then use CSTP1, 3 &amp; 4
/// as they would on an ST or DP record."
///
/// So **zero-fill is the signal**, not the presence of a <c>PRCID</c>. That distinction is not
/// academic: in the live file 269 of 46,585 rows carry a populated contract cost with no price
/// code at all, and a rule keyed on <c>PRCID</c> would have sent those to the fallback.
///
/// The gap is large and in the dealer's favour — 6% to 39% below programme cost depending on the
/// code — so reading the fallback when a contract cost exists means quoting a cost the dealer does
/// not pay.
///
/// Validated against SPR's own backend on 17 items spanning 8 pricing programmes, 5 units of
/// measure and discounts from 0% to 94%: 16 matched to the cent, the 17th explained by a stale
/// monthly file.
///
/// **Only level 1 is read.** Across all 141,277 rows of every file examined, <c>PCSTC1</c>,
/// <c>2</c> and <c>3</c> hold identical costs and identical quantities (always 1), so they are not
/// a quantity ladder here and there is no evidence of what would distinguish them. SPR has been
/// asked what the levels mean; until they answer, level 1 is the documented first choice and the
/// assumption is recorded rather than buried.
/// </remarks>
public static class SprDealerCost
{
    /// <summary>
    /// The cost the dealer transacts at: the contract cost when SPR supplied one, otherwise the
    /// programme cost for their CCP tier.
    /// </summary>
    /// <param name="contractCost">PCSTC1 — the contract or promotional cost. Zero means none.</param>
    /// <param name="nonCcpCost">CSTP1.</param>
    /// <param name="ccp3Cost">CSTP3.</param>
    /// <param name="ccp4Cost">CSTP4.</param>
    /// <param name="program">The dealer's buying programme, selecting among the fallbacks.</param>
    public static decimal Resolve(
        decimal contractCost,
        decimal nonCcpCost,
        decimal ccp3Cost,
        decimal ccp4Cost,
        SprCcpProgram program)
    {
        if (contractCost > 0)
        {
            return contractCost;
        }

        return program switch
        {
            SprCcpProgram.Ccp3 => ccp3Cost,
            SprCcpProgram.Ccp4 => ccp4Cost,
            _ => nonCcpCost
        };
    }

    /// <summary>True when SPR supplied a contract cost for this item.</summary>
    /// <remarks>
    /// Useful for reporting. An item on the fallback is not an error — it simply has no programme
    /// — but a file where suddenly *nothing* carries a contract cost would mean a lapsed
    /// agreement, and that is worth being able to see.
    /// </remarks>
    public static bool HasContractCost(decimal contractCost) => contractCost > 0;
}
