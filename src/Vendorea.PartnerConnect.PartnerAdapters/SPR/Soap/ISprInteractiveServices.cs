namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

/// <summary>
/// Client for SPR's interactive (real-time, request/response) SOAP web services — live stock and
/// price checks. Auth (GroupCode/UserID/Password) and the dealer's CustNumber come from the
/// supplied <see cref="SprWebServiceConfig"/>. Order submission is NOT done here (that's the
/// document pipeline); these are read-only lookups.
///
/// Stock-check family:
///  - StockCheck: availability across all DCs (no pricing).
///  - DealerStockCheck: availability across all DCs + dealer net price (needs CustNumber).
///  - QuickCheckPlus: item + dealer price + availability at up to 8 specified DCs.
/// Freight services (Find/Lowest Freight Rate) are added in a later phase.
/// </summary>
public interface ISprInteractiveServices
{
    /// <summary>Connectivity heartbeat (Action="?") — verifies endpoint + credentials.</summary>
    Task<SprPingResult> PingAsync(SprWebServiceConfig config, CancellationToken cancellationToken = default);

    /// <summary>Stock Check: availability at every stocking DC (no pricing).</summary>
    Task<SprStockCheckResult> StockCheckAsync(SprWebServiceConfig config, SprStockCheckQuery query, CancellationToken cancellationToken = default);

    /// <summary>Dealer Stock Check: Stock Check + dealer net price/discountable (uses CustNumber).</summary>
    Task<SprStockCheckResult> DealerStockCheckAsync(SprWebServiceConfig config, SprStockCheckQuery query, CancellationToken cancellationToken = default);

    /// <summary>Quick Check Plus: item + dealer price + availability at the (≤8) specified DCs.</summary>
    Task<SprStockCheckResult> QuickCheckPlusAsync(SprWebServiceConfig config, SprStockCheckQuery query, CancellationToken cancellationToken = default);
}
