namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// A single, source-agnostic entry in a tenant's activity feed. Assembled from the real
/// tenant-scoped sources that exist (price-feed uploads, order status changes, connection state,
/// quarantined documents) rather than a dedicated activity table.
/// </summary>
public sealed class ActivityEvent
{
    /// <summary>When the event happened (UTC).</summary>
    public DateTime At { get; set; }

    /// <summary>Source category: PriceFeed | Order | Connection | Exception.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Severity: Info | Warning | Error.</summary>
    public string Level { get; set; } = "Info";

    /// <summary>Short human-readable headline.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional supporting detail (record counts, error message, etc.).</summary>
    public string? Detail { get; set; }

    /// <summary>Correlation id for tracing, when the source carries one.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Relative portal link to the related screen, when applicable.</summary>
    public string? Link { get; set; }
}

/// <summary>
/// Assembles a unified, tenant-scoped activity feed from the underlying operational tables. Every
/// source query is filtered to the tenant in SQL; see the implementation for the paging model.
/// </summary>
public interface IActivityRepository
{
    /// <summary>
    /// A page of the tenant's activity feed (newest first). <paramref name="type"/> selects a single
    /// source (PriceFeed | Order | Connection | Exception) when supplied; <paramref name="level"/>
    /// filters by severity (Info | Warning | Error). Both filters and the date range are applied in
    /// SQL per source, so <c>Total</c> is exact.
    /// </summary>
    Task<(IReadOnlyList<ActivityEvent> Items, int Total)> GetTenantActivityPageAsync(
        int tenantId,
        string? type,
        string? level,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
