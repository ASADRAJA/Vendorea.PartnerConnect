namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Cheap, aggregated per-tenant health signals for the customer portal dashboard. Every value is a
/// single SQL aggregate over an existing operational table (no in-memory scans), assembled here so
/// the dashboard lands with one round trip.
/// </summary>
public sealed class TenantSummarySignals
{
    /// <summary>When the tenant's most recent successful (completed/published) price feed landed.</summary>
    public DateTime? LastPriceSyncAt { get; set; }

    /// <summary>The tenant's most recent full content refresh (across its content subscriptions).</summary>
    public DateTime? LastContentSyncAt { get; set; }

    /// <summary>
    /// Count of open error conditions for the tenant: failed price feeds (parse/publish),
    /// failed orders, and unresolved quarantined documents.
    /// </summary>
    public int OpenErrorCount { get; set; }
}

/// <summary>Assembles the tenant dashboard summary signals from the underlying operational tables.</summary>
public interface ITenantSummaryRepository
{
    /// <summary>
    /// Aggregated dashboard signals for one tenant. <paramref name="tenantId"/> is PC's internal
    /// Tenant.Id (== DealerId on price/content rows and TenantId on orders/quarantines).
    /// </summary>
    Task<TenantSummarySignals> GetSummarySignalsAsync(int tenantId, CancellationToken cancellationToken = default);
}
