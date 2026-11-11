using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Catches what the live path missed.
/// </summary>
/// <remarks>
/// Polling and notifications are both lossy in their own ways — a notification can be dropped, a
/// poll can fail after its watermark moved, a pagination token can expire mid-walk. None of those
/// is visible from inside the live path, which is why something has to come along afterwards and
/// look.
///
/// It is built around <c>getOrder</c> rather than <c>searchOrders</c>, because the rate limits
/// make that roughly **ninety times cheaper**: 0.5/s against 0.0056/s. Re-reading twenty-five
/// specific suspect orders costs a fraction of one broad re-search, so targeted reads are the
/// primary mechanism and a lagging gap search is the occasional backstop.
///
/// The sweep never advances the poller's watermark. It reads, it ingests through the ordinary
/// monotonic path, and it leaves the live position alone — a reconciliation run that moved the
/// watermark could skip orders the live poll had not reached yet.
/// </remarks>
public class SalesOrderReconciliationService : ISalesOrderReconciliationService
{
    private const string Source = "reconcile";

    /// <summary>Nothing further will happen to these, so re-reading one learns nothing.</summary>
    private static readonly SalesOrderStatus[] Terminal =
    {
        SalesOrderStatus.Fulfilled,
        SalesOrderStatus.Cancelled
    };

    private readonly IMarketplaceOrdersClient _client;
    private readonly ISalesOrderIngestionService _ingestion;
    private readonly ISalesOrderReconciliationRepository _orders;
    private readonly SalesOrderReconciliationOptions _options;
    private readonly ILogger<SalesOrderReconciliationService> _logger;

    public SalesOrderReconciliationService(
        IMarketplaceOrdersClient client,
        ISalesOrderIngestionService ingestion,
        ISalesOrderReconciliationRepository orders,
        IOptions<SalesOrderReconciliationOptions> options,
        ILogger<SalesOrderReconciliationService> logger)
    {
        _client = client;
        _ingestion = ingestion;
        _orders = orders;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SalesOrderReconciliationResult> ReconcileAsync(
        IntegrationConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection.Status != IntegrationConnectionStatus.Connected ||
            !connection.GrantedCapabilities.HasFlag(IntegrationCapabilities.SalesOrderIngest))
        {
            return SalesOrderReconciliationResult.Skipped("The connection is not currently ingesting orders.");
        }

        var (examined, changed) = await ReReadStaleAsync(connection, cancellationToken);
        var (recovered, pages) = await SearchForGapsAsync(connection, cancellationToken);

        return new SalesOrderReconciliationResult(examined, changed, recovered, pages);
    }

    /// <summary>
    /// Re-reads orders that have gone quiet without reaching a terminal state.
    /// </summary>
    private async Task<(int Examined, int Changed)> ReReadStaleAsync(
        IntegrationConnection connection, CancellationToken cancellationToken)
    {
        var stale = await _orders.GetStaleAsync(
            connection.Id,
            DateTime.UtcNow.Subtract(_options.StaleAfter),
            Terminal,
            _options.MaxOrdersPerRun,
            cancellationToken);

        var examined = 0;
        var changed = 0;

        foreach (var order in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _client.GetOrderAsync(
                connection.TenantId, connection.Id, order.ExternalOrderId, cancellationToken);

            if (result.Outcome == MarketplaceCallOutcome.RateLimited)
            {
                // Out of budget. Stopping is right: the remaining orders are still stale and will
                // be picked up next run, whereas pressing on earns 429s that cost the live poller.
                _logger.LogDebug(
                    "Reconciliation for connection {ConnectionId} stopped early: rate limited after {Examined}.",
                    connection.Id, examined);
                break;
            }

            examined++;

            if (!result.Success)
            {
                _logger.LogDebug(
                    "Reconciliation could not re-read {ExternalOrderId}: {Outcome}",
                    order.ExternalOrderId, result.Outcome);
                continue;
            }

            var ingested = await _ingestion.IngestAsync(
                connection, result.Order!, Source, cancellationToken);

            if (ingested.Outcome == SalesOrderIngestOutcome.Updated)
            {
                changed++;

                // A stale order that turns out to have moved means the live path missed a
                // transition. Worth saying so — quietly healing it would hide the fact.
                _logger.LogInformation(
                    "Reconciliation updated {ExternalOrderId} on connection {ConnectionId}; "
                    + "the live path had not seen this change.",
                    order.ExternalOrderId, connection.Id);
            }
        }

        return (examined, changed);
    }

    /// <summary>
    /// Re-searches a window that ends well before now, looking for orders never ingested.
    /// </summary>
    /// <remarks>
    /// Deliberately **lagging**. Searching up to the present would mostly rediscover orders the
    /// live poller is about to collect anyway, and would spend the expensive call to do it. A
    /// window a day behind contains only orders that have had every chance to arrive and did not.
    ///
    /// Stateless on purpose: the same window is re-read every run, which is self-healing and
    /// cannot drift. Repeats are free — the unique index and the monotonic ingest make a
    /// re-arrival a no-op.
    /// </remarks>
    private async Task<(int Recovered, int Pages)> SearchForGapsAsync(
        IntegrationConnection connection, CancellationToken cancellationToken)
    {
        if (!_options.GapSearchEnabled)
        {
            return (0, 0);
        }

        var until = DateTime.UtcNow.Subtract(_options.GapSearchLag);
        var from = until.Subtract(_options.GapSearchWindow);

        var page = await _client.SearchOrdersAsync(
            connection.TenantId, connection.Id,
            new MarketplaceOrderQuery
            {
                // Created-time, not last-updated: the question is "was this order ever ingested?",
                // and an order we never saw has no update history to match on.
                CreatedAfter = from,
                CreatedBefore = until
            },
            cancellationToken);

        if (!page.Success)
        {
            if (page.Outcome != MarketplaceCallOutcome.RateLimited)
            {
                _logger.LogDebug(
                    "Gap search for connection {ConnectionId} failed: {Outcome}",
                    connection.Id, page.Outcome);
            }
            return (0, 0);
        }

        if (page.Orders.Count == 0)
        {
            return (0, 1);
        }

        var known = await _orders.GetKnownExternalIdsAsync(
            connection.Id, page.Orders.Select(o => o.ExternalOrderId).ToList(), cancellationToken);

        var knownSet = known.ToHashSet(StringComparer.Ordinal);
        var recovered = 0;

        foreach (var snapshot in page.Orders.Where(o => !knownSet.Contains(o.ExternalOrderId)))
        {
            var ingested = await _ingestion.IngestAsync(connection, snapshot, Source, cancellationToken);

            if (ingested.Outcome == SalesOrderIngestOutcome.Created)
            {
                recovered++;
            }
        }

        if (recovered > 0)
        {
            // This is a real finding, not routine housekeeping. An order that reached neither the
            // poller nor a notification is evidence of a problem in the live path, and the sweep
            // having fixed it does not make the problem go away.
            _logger.LogWarning(
                "Gap search recovered {Count} order(s) on connection {ConnectionId} that the live "
                + "path never ingested, created between {From:u} and {Until:u}.",
                recovered, connection.Id, from, until);
        }

        // Only the first page is read. A gap of more than one page is not a gap, it is an outage,
        // and the right response to an outage is a visible alert rather than a quiet backfill.
        if (page.NextToken is not null)
        {
            _logger.LogWarning(
                "Gap search for connection {ConnectionId} found more than one page in the lagging "
                + "window. That is an ingestion outage rather than a gap; investigate the poller.",
                connection.Id);
        }

        return (recovered, 1);
    }
}

/// <summary>Reconciliation behaviour.</summary>
public class SalesOrderReconciliationOptions
{
    public const string SectionName = "Amazon:OrderReconciliation";

    /// <summary>
    /// How quiet a non-terminal order must go before it is re-read.
    /// </summary>
    /// <remarks>
    /// Six hours. Short enough that a missed transition is caught the same day; long enough that
    /// an order simply waiting for a buyer's payment is not re-read repeatedly for no reason.
    /// </remarks>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Orders re-read per connection per run.
    /// </summary>
    /// <remarks>
    /// Twenty-five fits inside one <c>getOrder</c> burst (30), so a sweep does not eat into the
    /// budget the live path needs.
    /// </remarks>
    public int MaxOrdersPerRun { get; set; } = 25;

    public bool GapSearchEnabled { get; set; } = true;

    /// <summary>How far behind the present the gap search looks.</summary>
    public TimeSpan GapSearchLag { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How wide a window it covers.</summary>
    public TimeSpan GapSearchWindow { get; set; } = TimeSpan.FromHours(24);
}
