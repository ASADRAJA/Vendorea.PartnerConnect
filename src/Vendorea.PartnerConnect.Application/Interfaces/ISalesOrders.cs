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
}

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
