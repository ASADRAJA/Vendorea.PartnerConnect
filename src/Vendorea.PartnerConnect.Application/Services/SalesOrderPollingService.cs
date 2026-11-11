using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Walks one connection's changed orders and persists them.
/// </summary>
/// <remarks>
/// One connection per call, deliberately. The provider meters per selling partner, so the unit
/// of work that can actually make progress is a connection — and a loop that walked every
/// connection inside one call would let a tenant with a large backlog hold the worker while
/// everyone else waited.
///
/// The walk is bounded by <see cref="SalesOrderPollingOptions.MaxPagesPerRun"/> rather than run
/// to completion. At roughly one page every three minutes a first backfill takes hours; stopping
/// after a few pages and persisting the watermark means the next run resumes where this one
/// stopped instead of starting again.
/// </remarks>
public class SalesOrderPollingService : ISalesOrderPollingService
{
    private const string Source = "poll";
    private const string Capability = nameof(IntegrationCapabilities.SalesOrderIngest);

    private readonly IMarketplaceOrdersClient _client;
    private readonly ISalesOrderIngestionService _ingestion;
    private readonly IIntegrationSyncStateRepository _syncStates;
    private readonly IIntegrationConnectionService _connections;
    private readonly SalesOrderPollingOptions _options;
    private readonly ILogger<SalesOrderPollingService> _logger;

    public SalesOrderPollingService(
        IMarketplaceOrdersClient client,
        ISalesOrderIngestionService ingestion,
        IIntegrationSyncStateRepository syncStates,
        IIntegrationConnectionService connections,
        IOptions<SalesOrderPollingOptions> options,
        ILogger<SalesOrderPollingService> logger)
    {
        _client = client;
        _ingestion = ingestion;
        _syncStates = syncStates;
        _connections = connections;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SalesOrderPollResult> PollAsync(
        IntegrationConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection.Status != IntegrationConnectionStatus.Connected ||
            !connection.GrantedCapabilities.HasFlag(IntegrationCapabilities.SalesOrderIngest))
        {
            return SalesOrderPollResult.Skipped("The connection is not currently ingesting orders.");
        }

        var marketplaceId = _options.MarketplaceId;

        var syncState = await _syncStates.GetAsync(
            connection.Id, IntegrationCapabilities.SalesOrderIngest, marketplaceId, cancellationToken);

        var query = BuildInitialQuery(syncState?.Watermark, syncState?.Cursor);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        DateTime? boundary = null;

        for (var page = 0; page < _options.MaxPagesPerRun; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _client.SearchOrdersAsync(
                connection.TenantId, connection.Id, query, cancellationToken);

            if (!result.Success)
            {
                return await HandleFailureAsync(
                    connection, marketplaceId, result, created, updated, skipped, boundary, cancellationToken);
            }

            foreach (var snapshot in result.Orders)
            {
                var ingested = await _ingestion.IngestAsync(connection, snapshot, Source, cancellationToken);

                switch (ingested.Outcome)
                {
                    case SalesOrderIngestOutcome.Created: created++; break;
                    case SalesOrderIngestOutcome.Updated: updated++; break;
                    case SalesOrderIngestOutcome.SkippedStale: skipped++; break;
                    default:
                        // One bad order must not abandon the page. The failure is recorded and
                        // the walk continues, because stopping would also strand every order
                        // behind it.
                        _logger.LogWarning(
                            "Failed to ingest {ExternalOrderId} for connection {ConnectionId}: {Error}",
                            snapshot.ExternalOrderId, connection.Id, ingested.Error);
                        break;
                }
            }

            boundary = result.Boundary ?? boundary;

            if (result.NextToken is null)
            {
                // The walk is complete. Only now is the watermark advanced to the boundary the
                // provider declared — advancing per page would skip anything still unread if the
                // next page failed.
                await CompleteAsync(connection, marketplaceId, boundary, cancellationToken);

                return SalesOrderPollResult.Completed(created, updated, skipped, boundary);
            }

            // More pages to come. The cursor is persisted so the next run resumes here, and the
            // watermark deliberately is not: an interrupted walk must re-read, not skip.
            await _syncStates.AdvanceWatermarkAsync(
                connection.Id, IntegrationCapabilities.SalesOrderIngest, marketplaceId,
                syncState?.Watermark ?? DateTime.UtcNow.Subtract(_options.InitialBackfill),
                result.NextToken, cancellationToken);

            query = query with { PaginationToken = result.NextToken };
        }

        _logger.LogInformation(
            "Paused the order walk for connection {ConnectionId} after {Pages} pages; it will resume next run.",
            connection.Id, _options.MaxPagesPerRun);

        return SalesOrderPollResult.Paused(created, updated, skipped);
    }

    /// <summary>
    /// Builds the first request of a run.
    /// </summary>
    /// <remarks>
    /// A stored cursor means the previous run stopped mid-walk; it is resumed rather than
    /// restarted, because restarting would re-read pages that cost three minutes each.
    ///
    /// Otherwise the window starts a little before the watermark. Provider timestamps are not
    /// perfectly ordered against our clock, and re-reading a few minutes is free — the unique
    /// index makes a repeat harmless — whereas missing a change is invisible until a merchant
    /// asks where their order went.
    /// </remarks>
    private MarketplaceOrderQuery BuildInitialQuery(DateTime? watermark, string? cursor)
    {
        if (!string.IsNullOrEmpty(cursor) && watermark.HasValue)
        {
            return new MarketplaceOrderQuery
            {
                LastUpdatedAfter = watermark.Value - _options.WatermarkOverlap,
                PaginationToken = cursor
            };
        }

        return watermark.HasValue
            ? new MarketplaceOrderQuery { LastUpdatedAfter = watermark.Value - _options.WatermarkOverlap }
            // A first run has no watermark. Created-time is the right axis for a backfill:
            // last-updated would re-walk every historical order that has ever been touched.
            : new MarketplaceOrderQuery { CreatedAfter = DateTime.UtcNow.Subtract(_options.InitialBackfill) };
    }

    private async Task<SalesOrderPollResult> HandleFailureAsync(
        IntegrationConnection connection, string? marketplaceId, MarketplaceOrderPage result,
        int created, int updated, int skipped, DateTime? boundary, CancellationToken cancellationToken)
    {
        switch (result.Outcome)
        {
            case MarketplaceCallOutcome.PaginationTokenExpired:
                // Tokens last 24 hours and a long backfill will outlive one. Dropping the cursor
                // restarts the walk from the persisted watermark on the next run — slower, but
                // correct. Treating it as a hard failure would strand the backfill for ever.
                _logger.LogWarning(
                    "The pagination token for connection {ConnectionId} expired; the walk restarts from the watermark.",
                    connection.Id);

                await ClearCursorAsync(connection, marketplaceId, cancellationToken);
                return SalesOrderPollResult.Retryable(created, updated, skipped, result.Error);

            case MarketplaceCallOutcome.RateLimited:
                // Expected, not exceptional, at roughly one call every three minutes. Not
                // recorded as a connection error: a throttled connection is a healthy one.
                _logger.LogDebug(
                    "Connection {ConnectionId} is rate limited; retrying in {RetryAfter}.",
                    connection.Id, result.RetryAfter);

                return SalesOrderPollResult.Retryable(created, updated, skipped, result.Error, result.RetryAfter);

            case MarketplaceCallOutcome.Unauthorized:
            case MarketplaceCallOutcome.Forbidden:
                // The client has already recorded this against the connection. Nothing here will
                // succeed until a person acts, so the run stops rather than retrying.
                return SalesOrderPollResult.Failed(created, updated, skipped, result.Error);

            default:
                await _syncStates.RecordFailureAsync(
                    connection.Id, IntegrationCapabilities.SalesOrderIngest, marketplaceId,
                    result.Error ?? "The order search failed.", cancellationToken);

                return SalesOrderPollResult.Retryable(created, updated, skipped, result.Error);
        }
    }

    private async Task CompleteAsync(
        IntegrationConnection connection, string? marketplaceId, DateTime? boundary,
        CancellationToken cancellationToken)
    {
        // No boundary means the provider did not declare one; keeping the previous watermark is
        // safe, where inventing "now" would skip whatever changed during the walk.
        if (boundary is null)
        {
            await ClearCursorAsync(connection, marketplaceId, cancellationToken);
            return;
        }

        await _syncStates.AdvanceWatermarkAsync(
            connection.Id, IntegrationCapabilities.SalesOrderIngest, marketplaceId,
            boundary.Value, cursor: null, cancellationToken);
    }

    private async Task ClearCursorAsync(
        IntegrationConnection connection, string? marketplaceId, CancellationToken cancellationToken)
    {
        var state = await _syncStates.GetAsync(
            connection.Id, IntegrationCapabilities.SalesOrderIngest, marketplaceId, cancellationToken);

        await _syncStates.AdvanceWatermarkAsync(
            connection.Id, IntegrationCapabilities.SalesOrderIngest, marketplaceId,
            state?.Watermark ?? DateTime.UtcNow.Subtract(_options.InitialBackfill),
            cursor: null, cancellationToken);
    }
}

/// <summary>Polling behaviour. The defaults follow the provider's published limits.</summary>
public class SalesOrderPollingOptions
{
    public const string SectionName = "Amazon:OrderPolling";

    /// <summary>
    /// How far back a first run reaches.
    /// </summary>
    /// <remarks>
    /// Thirty days by default. Longer is a business decision with a real cost: at one page every
    /// three minutes, a year of a busy seller's history is days of walking.
    /// </remarks>
    public TimeSpan InitialBackfill { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Re-read window applied to the watermark on every incremental run.</summary>
    public TimeSpan WatermarkOverlap { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Pages per connection per run.
    /// </summary>
    /// <remarks>
    /// Small on purpose. Each page costs a rate-limit token that refills every three minutes, so
    /// a long walk is better spread across runs than held inside one — and a run that ends
    /// promptly lets every other connection have its turn.
    /// </remarks>
    public int MaxPagesPerRun { get; set; } = 5;

    /// <summary>Marketplace the watermark is kept for. Null means the connection's default.</summary>
    public string? MarketplaceId { get; set; }
}
