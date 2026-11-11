using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests;

/// <summary>
/// The reconciliation sweep — test matrix 3.8.
/// </summary>
/// <remarks>
/// What is being proven is that the sweep catches what the live path lost **without** becoming an
/// expensive second poller. It is built on <c>getOrder</c> (0.5/s) rather than
/// <c>searchOrders</c> (0.0056/s), and it must never move the poller's watermark.
/// </remarks>
public class SalesOrderReconciliationTests
{
    private static IntegrationConnection Connection(
        IntegrationConnectionStatus status = IntegrationConnectionStatus.Connected,
        IntegrationCapabilities capabilities = IntegrationCapabilities.SalesOrderIngest)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = 7,
            ExternalAccountId = "SELLER-A",
            Status = status,
            GrantedCapabilities = capabilities
        };

    private static SalesOrder Stale(string id, SalesOrderStatus status = SalesOrderStatus.ReadyToFulfill)
        => new()
        {
            Id = Guid.NewGuid(),
            ExternalOrderId = id,
            Status = status,
            LastSyncedAt = DateTime.UtcNow.AddDays(-2)
        };

    private static MarketplaceOrderSnapshot Snapshot(string id) => new()
    {
        ExternalOrderId = id,
        Status = SalesOrderStatus.Fulfilled,
        SourceLastUpdatedAt = DateTime.UtcNow
    };

    private static (SalesOrderReconciliationService Service, StubClient Client, StubIngestion Ingestion)
        Build(StubClient client, StubRepository repository, SalesOrderReconciliationOptions? options = null)
    {
        var ingestion = new StubIngestion();
        var service = new SalesOrderReconciliationService(
            client, ingestion, repository,
            Options.Create(options ?? new SalesOrderReconciliationOptions { GapSearchEnabled = false }),
            NullLogger<SalesOrderReconciliationService>.Instance);

        return (service, client, ingestion);
    }

    // ---- stale re-read ----

    [Fact]
    public async Task Stale_orders_are_re_read_with_getOrder_not_a_re_search()
    {
        var repository = new StubRepository { Stale = { Stale("A"), Stale("B") } };
        var (service, client, _) = Build(new StubClient(), repository);

        var result = await service.ReconcileAsync(Connection());

        result.Examined.Should().Be(2);

        // getOrder is roughly ninety times cheaper than searchOrders. Re-searching to find out
        // what happened to two known orders would be the expensive way to ask.
        client.GetOrderCalls.Should().BeEquivalentTo(new[] { "A", "B" });
        client.SearchCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task An_order_that_has_moved_is_counted_as_changed()
    {
        var repository = new StubRepository { Stale = { Stale("A") } };
        var (service, _, ingestion) = Build(new StubClient(), repository);
        ingestion.Outcome = SalesOrderIngestOutcome.Updated;

        var result = await service.ReconcileAsync(Connection());

        // A stale order that turns out to have moved means the live path missed a transition.
        result.Changed.Should().Be(1);
    }

    [Fact]
    public async Task An_order_that_has_not_moved_is_examined_but_not_counted_as_changed()
    {
        var repository = new StubRepository { Stale = { Stale("A") } };
        var (service, _, ingestion) = Build(new StubClient(), repository);
        ingestion.Outcome = SalesOrderIngestOutcome.SkippedStale;

        var result = await service.ReconcileAsync(Connection());

        result.Examined.Should().Be(1);
        result.Changed.Should().Be(0);
    }

    [Fact]
    public async Task Running_out_of_budget_stops_the_sweep_rather_than_pressing_on()
    {
        var repository = new StubRepository
        {
            Stale = { Stale("A"), Stale("B"), Stale("C"), Stale("D") }
        };
        var client = new StubClient { RateLimitAfter = 2 };
        var (service, _, _) = Build(client, repository);

        var result = await service.ReconcileAsync(Connection());

        // The remaining orders are still stale and will be picked up next run. Pressing on would
        // earn 429s that cost the live poller its own budget.
        result.Examined.Should().Be(2);
        client.GetOrderCalls.Should().HaveCount(3);   // the third is the one that was refused
    }

    [Fact]
    public async Task A_failed_re_read_does_not_abandon_the_rest()
    {
        var repository = new StubRepository { Stale = { Stale("A"), Stale("BAD"), Stale("C") } };
        var client = new StubClient { FailFor = "BAD" };
        var (service, _, ingestion) = Build(client, repository);

        var result = await service.ReconcileAsync(Connection());

        result.Examined.Should().Be(3);
        ingestion.Ingested.Should().BeEquivalentTo(new[] { "A", "C" });
    }

    [Fact]
    public async Task Terminal_orders_are_never_asked_for()
    {
        var repository = new StubRepository { Stale = { Stale("A") } };
        var (service, _, _) = Build(new StubClient(), repository);

        await service.ReconcileAsync(Connection());

        // Nothing further happens to a shipped or cancelled order, so re-reading one spends
        // budget to learn nothing. The repository is asked to exclude them.
        repository.ExcludedStatuses.Should().BeEquivalentTo(
            new[] { SalesOrderStatus.Fulfilled, SalesOrderStatus.Cancelled });
    }

    [Fact]
    public async Task The_stale_threshold_and_batch_size_are_passed_through()
    {
        var repository = new StubRepository();
        var (service, _, _) = Build(new StubClient(), repository,
            new SalesOrderReconciliationOptions
            {
                StaleAfter = TimeSpan.FromHours(6),
                MaxOrdersPerRun = 25,
                GapSearchEnabled = false
            });

        await service.ReconcileAsync(Connection());

        repository.Limit.Should().Be(25);
        repository.NotSyncedSince.Should().BeCloseTo(
            DateTime.UtcNow.AddHours(-6), TimeSpan.FromMinutes(1));
    }

    // ---- gap search ----

    [Fact]
    public async Task The_gap_search_looks_at_a_lagging_window_not_up_to_the_present()
    {
        var repository = new StubRepository();
        var (service, client, _) = Build(new StubClient(), repository,
            new SalesOrderReconciliationOptions
            {
                GapSearchEnabled = true,
                GapSearchLag = TimeSpan.FromHours(24),
                GapSearchWindow = TimeSpan.FromHours(24)
            });

        await service.ReconcileAsync(Connection());

        var query = client.SearchCalls.Should().ContainSingle().Subject;

        // Searching up to now would mostly rediscover orders the live poller is about to collect
        // anyway, and would spend the expensive call to do it.
        query.CreatedBefore.Should().BeCloseTo(DateTime.UtcNow.AddHours(-24), TimeSpan.FromMinutes(1));
        query.CreatedAfter.Should().BeCloseTo(DateTime.UtcNow.AddHours(-48), TimeSpan.FromMinutes(1));

        // Created-time, not last-updated: an order we never saw has no update history to match on.
        query.LastUpdatedAfter.Should().BeNull();
    }

    [Fact]
    public async Task An_order_the_live_path_never_ingested_is_recovered()
    {
        var repository = new StubRepository { Known = { "SEEN-1" } };
        var client = new StubClient
        {
            GapPage = MarketplaceOrderPage.Ok(
                new[] { Snapshot("SEEN-1"), Snapshot("MISSED-1"), Snapshot("MISSED-2") }, null, null)
        };
        var (service, _, ingestion) = Build(client, repository,
            new SalesOrderReconciliationOptions { GapSearchEnabled = true });

        var result = await service.ReconcileAsync(Connection());

        result.Recovered.Should().Be(2);

        // Only the unknown ones are ingested — re-ingesting what we already hold would be a
        // no-op, but asking is cheaper than writing.
        ingestion.Ingested.Should().BeEquivalentTo(new[] { "MISSED-1", "MISSED-2" });
    }

    [Fact]
    public async Task A_gap_search_that_finds_nothing_new_recovers_nothing()
    {
        var repository = new StubRepository { Known = { "A", "B" } };
        var client = new StubClient
        {
            GapPage = MarketplaceOrderPage.Ok(new[] { Snapshot("A"), Snapshot("B") }, null, null)
        };
        var (service, _, ingestion) = Build(client, repository,
            new SalesOrderReconciliationOptions { GapSearchEnabled = true });

        var result = await service.ReconcileAsync(Connection());

        result.Recovered.Should().Be(0);
        result.GapPagesRead.Should().Be(1);
        ingestion.Ingested.Should().BeEmpty();
    }

    [Fact]
    public async Task Only_the_first_gap_page_is_read()
    {
        var repository = new StubRepository();
        var client = new StubClient
        {
            GapPage = MarketplaceOrderPage.Ok(new[] { Snapshot("A") }, "more-pages", null)
        };
        var (service, _, _) = Build(client, repository,
            new SalesOrderReconciliationOptions { GapSearchEnabled = true });

        var result = await service.ReconcileAsync(Connection());

        // A gap of more than one page is not a gap, it is an outage, and the right response to an
        // outage is a visible alert rather than a quiet backfill.
        result.GapPagesRead.Should().Be(1);
        client.SearchCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task The_gap_search_can_be_turned_off()
    {
        var repository = new StubRepository();
        var (service, client, _) = Build(new StubClient(), repository,
            new SalesOrderReconciliationOptions { GapSearchEnabled = false });

        await service.ReconcileAsync(Connection());

        client.SearchCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_rate_limited_gap_search_is_not_an_error()
    {
        var repository = new StubRepository();
        var client = new StubClient
        {
            GapPage = MarketplaceOrderPage.Failed(MarketplaceCallOutcome.RateLimited, "throttled")
        };
        var (service, _, _) = Build(client, repository,
            new SalesOrderReconciliationOptions { GapSearchEnabled = true });

        var result = await service.ReconcileAsync(Connection());

        result.Recovered.Should().Be(0);
        result.GapPagesRead.Should().Be(0);
    }

    // ---- gating ----

    [Theory]
    [InlineData(IntegrationConnectionStatus.Revoked)]
    [InlineData(IntegrationConnectionStatus.Expired)]
    [InlineData(IntegrationConnectionStatus.Pending)]
    public async Task A_connection_that_is_not_connected_is_skipped(IntegrationConnectionStatus status)
    {
        var repository = new StubRepository { Stale = { Stale("A") } };
        var (service, client, _) = Build(new StubClient(), repository);

        var result = await service.ReconcileAsync(Connection(status));

        result.Examined.Should().Be(0);
        result.Note.Should().NotBeNull();
        client.GetOrderCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_connection_without_the_ingest_capability_is_skipped()
    {
        var repository = new StubRepository { Stale = { Stale("A") } };
        var (service, client, _) = Build(new StubClient(), repository);

        var result = await service.ReconcileAsync(
            Connection(capabilities: IntegrationCapabilities.ListingSync));

        result.Examined.Should().Be(0);
        client.GetOrderCalls.Should().BeEmpty();
    }

    // ---- stubs ----

    private sealed class StubClient : IMarketplaceOrdersClient
    {
        public List<string> GetOrderCalls { get; } = new();
        public List<MarketplaceOrderQuery> SearchCalls { get; } = new();

        public int? RateLimitAfter { get; set; }
        public string? FailFor { get; set; }
        public MarketplaceOrderPage? GapPage { get; set; }

        public Task<MarketplaceOrderPage> SearchOrdersAsync(
            int tenantId, Guid connectionId, MarketplaceOrderQuery query,
            CancellationToken cancellationToken = default)
        {
            SearchCalls.Add(query);
            return Task.FromResult(GapPage
                ?? MarketplaceOrderPage.Ok(Array.Empty<MarketplaceOrderSnapshot>(), null, null));
        }

        public Task<MarketplaceOrderResult> GetOrderAsync(
            int tenantId, Guid connectionId, string externalOrderId,
            CancellationToken cancellationToken = default)
        {
            GetOrderCalls.Add(externalOrderId);

            if (RateLimitAfter.HasValue && GetOrderCalls.Count > RateLimitAfter.Value)
            {
                return Task.FromResult(MarketplaceOrderResult.Failed(
                    MarketplaceCallOutcome.RateLimited, "throttled"));
            }

            if (externalOrderId == FailFor)
            {
                return Task.FromResult(MarketplaceOrderResult.Failed(
                    MarketplaceCallOutcome.Transient, "502"));
            }

            return Task.FromResult(MarketplaceOrderResult.Ok(Snapshot(externalOrderId)));
        }
    }

    private sealed class StubIngestion : ISalesOrderIngestionService
    {
        public List<string> Ingested { get; } = new();
        public SalesOrderIngestOutcome Outcome { get; set; } = SalesOrderIngestOutcome.Created;

        public Task<SalesOrderIngestResult> IngestAsync(
            IntegrationConnection connection, MarketplaceOrderSnapshot snapshot, string source,
            CancellationToken cancellationToken = default)
        {
            Ingested.Add(snapshot.ExternalOrderId);
            return Task.FromResult(new SalesOrderIngestResult(Outcome, Guid.NewGuid()));
        }
    }

    private sealed class StubRepository : ISalesOrderReconciliationRepository
    {
        public List<SalesOrder> Stale { get; } = new();
        public HashSet<string> Known { get; } = new(StringComparer.Ordinal);

        public int Limit { get; private set; }
        public DateTime NotSyncedSince { get; private set; }
        public IReadOnlyList<SalesOrderStatus> ExcludedStatuses { get; private set; }
            = Array.Empty<SalesOrderStatus>();

        public Task<IReadOnlyList<SalesOrder>> GetStaleAsync(
            Guid connectionId, DateTime notSyncedSince,
            IReadOnlyList<SalesOrderStatus> terminalStatuses, int limit,
            CancellationToken cancellationToken = default)
        {
            NotSyncedSince = notSyncedSince;
            ExcludedStatuses = terminalStatuses;
            Limit = limit;
            return Task.FromResult<IReadOnlyList<SalesOrder>>(Stale);
        }

        public Task<IReadOnlyList<string>> GetKnownExternalIdsAsync(
            Guid connectionId, IReadOnlyList<string> externalOrderIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(
                externalOrderIds.Where(Known.Contains).ToList());
    }
}
