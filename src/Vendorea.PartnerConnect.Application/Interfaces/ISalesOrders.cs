using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Application.Interfaces;

public interface ISalesOrderRepository
{
    /// <summary>Finds an order by provider identity, with its lines.</summary>
    Task<SalesOrder?> FindAsync(
        Guid connectionId, string externalOrderId, CancellationToken cancellationToken = default);

    Task<SalesOrder?> GetAsync(int tenantId, Guid salesOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts an order, returning false when one already exists for that provider identity.
    /// </summary>
    /// <remarks>
    /// A bool rather than an exception because losing this race is the design, not a fault:
    /// polling and notifications reach the same order concurrently on purpose. Keeping the
    /// uniqueness-violation detection inside the repository also keeps persistence types out of
    /// the application layer.
    /// </remarks>
    Task<bool> TryAddAsync(SalesOrder order, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>A tenant's orders, newest first.</summary>
    Task<IReadOnlyList<SalesOrder>> ListAsync(
        int tenantId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// A filtered, paged page of a tenant's orders with the total matching count.
    /// </summary>
    /// <remarks>
    /// Returns the total alongside the page because a caller showing "51–100 of 1,284" otherwise
    /// has to ask twice for one answer. The provider code is projected from the connection in the
    /// same query rather than resolved per row.
    /// </remarks>
    Task<SalesOrderPage> SearchAsync(
        int tenantId, SalesOrderQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Filters for a sales-order listing.</summary>
public sealed record SalesOrderQuery
{
    public Guid? ConnectionId { get; init; }
    public SalesOrderStatus? Status { get; init; }
    public MappingStatus? MappingStatus { get; init; }

    /// <summary>Orders whose provider timestamp is at or after this instant.</summary>
    public DateTime? UpdatedSince { get; init; }

    public int Skip { get; init; }
    public int Take { get; init; } = 50;
}

public sealed record SalesOrderPage(IReadOnlyList<SalesOrder> Items, int Total, int Skip, int Take);

/// <summary>Writes a provider snapshot into the sales-order model.</summary>
public interface ISalesOrderIngestionService
{
    Task<SalesOrderIngestResult> IngestAsync(
        IntegrationConnection connection, MarketplaceOrderSnapshot snapshot, string source,
        CancellationToken cancellationToken = default);
}

public enum SalesOrderIngestOutcome
{
    Created,
    Updated,

    /// <summary>
    /// The snapshot was older than what is stored, so nothing changed.
    /// </summary>
    /// <remarks>
    /// Routine, not exceptional. Polling and notifications overlap by design, so the same order
    /// arrives twice and often out of order; the older arrival must lose.
    /// </remarks>
    SkippedStale,

    Failed
}

public sealed record SalesOrderIngestResult(
    SalesOrderIngestOutcome Outcome, Guid? SalesOrderId, string? Error = null)
{
    public bool Success => Outcome != SalesOrderIngestOutcome.Failed;

    public static SalesOrderIngestResult Created(Guid id) => new(SalesOrderIngestOutcome.Created, id);
    public static SalesOrderIngestResult Updated(Guid id) => new(SalesOrderIngestOutcome.Updated, id);
    public static SalesOrderIngestResult Stale(Guid id) => new(SalesOrderIngestOutcome.SkippedStale, id);
    public static SalesOrderIngestResult Failed(string error) => new(SalesOrderIngestOutcome.Failed, null, error);
}

/// <summary>Polls one connection for changed orders.</summary>
public interface ISalesOrderPollingService
{
    Task<SalesOrderPollResult> PollAsync(
        Domain.Entities.Integration.IntegrationConnection connection,
        CancellationToken cancellationToken = default);
}

public enum SalesOrderPollOutcome
{
    /// <summary>The walk reached the end and the watermark advanced.</summary>
    Completed,

    /// <summary>Pages remain. The cursor is stored and the next run resumes.</summary>
    Paused,

    /// <summary>Stopped for a reason that will clear on its own.</summary>
    Retryable,

    /// <summary>Stopped for a reason that needs a person.</summary>
    Failed,

    /// <summary>The connection is not currently ingesting orders.</summary>
    Skipped
}

public sealed record SalesOrderPollResult(
    SalesOrderPollOutcome Outcome,
    int Created,
    int Updated,
    int SkippedStale,
    DateTime? Watermark = null,
    string? Error = null,
    TimeSpan? RetryAfter = null)
{
    public int Total => Created + Updated + SkippedStale;

    public static SalesOrderPollResult Completed(int c, int u, int s, DateTime? watermark)
        => new(SalesOrderPollOutcome.Completed, c, u, s, watermark);

    public static SalesOrderPollResult Paused(int c, int u, int s)
        => new(SalesOrderPollOutcome.Paused, c, u, s);

    public static SalesOrderPollResult Retryable(int c, int u, int s, string? error, TimeSpan? retryAfter = null)
        => new(SalesOrderPollOutcome.Retryable, c, u, s, null, error, retryAfter);

    public static SalesOrderPollResult Failed(int c, int u, int s, string? error)
        => new(SalesOrderPollOutcome.Failed, c, u, s, null, error);

    public static SalesOrderPollResult Skipped(string reason)
        => new(SalesOrderPollOutcome.Skipped, 0, 0, 0, null, reason);
}

/// <summary>Reads and saves the orders a retention sweep acts on.</summary>
/// <remarks>
/// Separate from <see cref="ISalesOrderRepository"/> because it is the one read that is
/// deliberately not tenant-scoped: retention is a platform obligation applied across every
/// tenant, and scoping it per tenant would mean an obligation nobody was responsible for
/// discharging. It returns orders only to redact them and writes nothing else.
/// </remarks>
public interface ISalesOrderRetentionRepository
{
    /// <summary>
    /// Orders due for redaction: settled ones past the ordinary window, plus any order at all
    /// past the hard limit.
    /// </summary>
    Task<IReadOnlyList<SalesOrder>> GetDueForRedactionAsync(
        DateTime settledBefore, DateTime hardLimitBefore,
        IReadOnlyList<SalesOrderStatus> settledStatuses, int batchSize,
        CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}

/// <summary>Finds orders a reconciliation sweep should look at again.</summary>
public interface ISalesOrderReconciliationRepository
{
    /// <summary>
    /// Non-terminal orders for a connection that have not been synced recently, oldest first.
    /// </summary>
    /// <remarks>
    /// Terminal orders are excluded: nothing further will happen to a shipped or cancelled order,
    /// so re-reading one spends budget to learn nothing. Oldest first because the longest-stuck
    /// order is the one most likely to be genuinely wrong.
    /// </remarks>
    Task<IReadOnlyList<SalesOrder>> GetStaleAsync(
        Guid connectionId, DateTime notSyncedSince,
        IReadOnlyList<SalesOrderStatus> terminalStatuses, int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Which of these provider order ids this connection already holds.</summary>
    Task<IReadOnlyList<string>> GetKnownExternalIdsAsync(
        Guid connectionId, IReadOnlyList<string> externalOrderIds,
        CancellationToken cancellationToken = default);
}

/// <summary>Re-reads what may have been missed or gone stale.</summary>
public interface ISalesOrderReconciliationService
{
    Task<SalesOrderReconciliationResult> ReconcileAsync(
        Domain.Entities.Integration.IntegrationConnection connection,
        CancellationToken cancellationToken = default);
}

public sealed record SalesOrderReconciliationResult(
    int Examined,
    int Changed,
    int Recovered,
    int GapPagesRead,
    string? Note = null)
{
    public static SalesOrderReconciliationResult Skipped(string note) => new(0, 0, 0, 0, note);
}

/// <summary>
/// The operator's cross-tenant view of marketplace orders.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="ISalesOrderRepository"/>, whose every read takes a tenant
/// id. This one does not, because a PartnerConnect operator diagnosing ingestion needs to see
/// across tenants — and separating the two means the tenant-scoped interface stays tenant-scoped
/// rather than growing an optional parameter that makes isolation conditional.
///
/// Reached only through the admin API, which requires an admin key. An organization key cannot
/// get here.
/// </remarks>
public interface ISalesOrderAdminRepository
{
    Task<SalesOrderAdminPage> SearchAsync(
        SalesOrderAdminQuery query, CancellationToken cancellationToken = default);

    /// <summary>One order with its lines, unscoped — for operator diagnosis.</summary>
    Task<SalesOrder?> GetAsync(Guid salesOrderId, CancellationToken cancellationToken = default);
}

public sealed record SalesOrderAdminQuery
{
    public int? OrganizationId { get; init; }
    public int? TenantId { get; init; }
    public Guid? ConnectionId { get; init; }
    public SalesOrderStatus? Status { get; init; }
    public MappingStatus? MappingStatus { get; init; }

    /// <summary>Matches a provider order id or a seller SKU on any line.</summary>
    public string? Search { get; init; }

    public int Skip { get; init; }
    public int Take { get; init; } = 50;
}

public sealed record SalesOrderAdminPage(
    IReadOnlyList<SalesOrder> Items,
    int Total,
    int Skip,
    int Take,
    IReadOnlyDictionary<SalesOrderStatus, int> CountsByStatus,
    int UnmappedCount);
