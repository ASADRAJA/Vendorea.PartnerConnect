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
/// The polling walk — test matrix 3.5 and 3.6.
/// </summary>
/// <remarks>
/// What is being proven is when the watermark moves and when it deliberately does not. Advancing
/// it on an interrupted walk would skip whatever was never read, and an order skipped by the
/// watermark is invisible until a merchant asks where it went.
/// </remarks>
public class SalesOrderPollingServiceTests
{
    private static readonly DateTime Watermark = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

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

    private static (SalesOrderPollingService Service, StubClient Client, StubSyncStates States, StubIngestion Ingestion)
        Build(StubClient client, IntegrationSyncState? existingState = null, int maxPages = 5)
    {
        var states = new StubSyncStates(existingState);
        var ingestion = new StubIngestion();

        var service = new SalesOrderPollingService(
            client,
            ingestion,
            states,
            new StubConnections(),
            Options.Create(new SalesOrderPollingOptions
            {
                MaxPagesPerRun = maxPages,
                WatermarkOverlap = TimeSpan.FromMinutes(5),
                InitialBackfill = TimeSpan.FromDays(30)
            }),
            NullLogger<SalesOrderPollingService>.Instance);

        return (service, client, states, ingestion);
    }

    private static MarketplaceOrderSnapshot Order(string id) => new()
    {
        ExternalOrderId = id,
        Status = SalesOrderStatus.ReadyToFulfill,
        SourceLastUpdatedAt = Watermark.AddMinutes(10)
    };

    // ---- the window ----

    [Fact]
    public async Task A_first_run_backfills_on_created_time()
    {
        var client = StubClient.Pages(MarketplaceOrderPage.Ok(Array.Empty<MarketplaceOrderSnapshot>(), null, null));
        var (service, _, _, _) = Build(client);

        await service.PollAsync(Connection());

        // Last-updated would re-walk every historical order that has ever been touched; created
        // time is the right axis for a backfill.
        client.Queries[0].CreatedAfter.Should().NotBeNull();
        client.Queries[0].LastUpdatedAfter.Should().BeNull();
    }

    [Fact]
    public async Task An_incremental_run_re_reads_a_little_before_the_watermark()
    {
        var client = StubClient.Pages(MarketplaceOrderPage.Ok(Array.Empty<MarketplaceOrderSnapshot>(), null, null));
        var (service, _, _, _) = Build(client, State(Watermark));

        await service.PollAsync(Connection());

        // Provider timestamps are not perfectly ordered against our clock. Re-reading is free —
        // the unique index makes a repeat harmless — whereas missing a change is not.
        client.Queries[0].LastUpdatedAfter.Should().Be(Watermark.AddMinutes(-5));
    }

    // ---- the watermark ----

    [Fact]
    public async Task A_completed_walk_advances_the_watermark_to_the_providers_boundary()
    {
        var boundary = Watermark.AddHours(1);
        var client = StubClient.Pages(
            MarketplaceOrderPage.Ok(new[] { Order("A"), Order("B") }, null, boundary));

        var (service, _, states, _) = Build(client, State(Watermark));

        var result = await service.PollAsync(Connection());

        result.Outcome.Should().Be(SalesOrderPollOutcome.Completed);
        result.Created.Should().Be(2);

        // The boundary Amazon declared, not the newest row seen.
        states.Watermark.Should().Be(boundary);
        states.Cursor.Should().BeNull();
    }

    [Fact]
    public async Task An_interrupted_walk_stores_the_cursor_and_leaves_the_watermark_alone()
    {
        var client = StubClient.Pages(
            MarketplaceOrderPage.Ok(new[] { Order("A") }, "page-2", Watermark.AddHours(1)),
            MarketplaceOrderPage.Failed(MarketplaceCallOutcome.RateLimited, "throttled", TimeSpan.FromMinutes(3)));

        var (service, _, states, _) = Build(client, State(Watermark));

        var result = await service.PollAsync(Connection());

        result.Outcome.Should().Be(SalesOrderPollOutcome.Retryable);

        // The second page was never read. Advancing past it would skip its orders for ever.
        states.Watermark.Should().Be(Watermark);
        states.Cursor.Should().Be("page-2");
    }

    [Fact]
    public async Task A_walk_with_no_declared_boundary_keeps_the_previous_watermark()
    {
        var client = StubClient.Pages(MarketplaceOrderPage.Ok(new[] { Order("A") }, null, null));
        var (service, _, states, _) = Build(client, State(Watermark));

        await service.PollAsync(Connection());

        // Inventing "now" would skip whatever changed during the walk.
        states.Watermark.Should().Be(Watermark);
        states.Cursor.Should().BeNull();
    }

    // ---- paging ----

    [Fact]
    public async Task A_multi_page_walk_follows_the_continuation_token()
    {
        var client = StubClient.Pages(
            MarketplaceOrderPage.Ok(new[] { Order("A") }, "p2", Watermark.AddHours(1)),
            MarketplaceOrderPage.Ok(new[] { Order("B") }, "p3", Watermark.AddHours(1)),
            MarketplaceOrderPage.Ok(new[] { Order("C") }, null, Watermark.AddHours(2)));

        var (service, _, states, _) = Build(client, State(Watermark));

        var result = await service.PollAsync(Connection());

        result.Outcome.Should().Be(SalesOrderPollOutcome.Completed);
        result.Created.Should().Be(3);
        client.Queries[1].PaginationToken.Should().Be("p2");
        client.Queries[2].PaginationToken.Should().Be("p3");
        states.Watermark.Should().Be(Watermark.AddHours(2));
    }

    [Fact]
    public async Task A_continuation_repeats_the_original_window()
    {
        var client = StubClient.Pages(
            MarketplaceOrderPage.Ok(Array.Empty<MarketplaceOrderSnapshot>(), "p2", null),
            MarketplaceOrderPage.Ok(Array.Empty<MarketplaceOrderSnapshot>(), null, null));

        var (service, _, _, _) = Build(client, State(Watermark));

        await service.PollAsync(Connection());

        // Amazon requires every value of the follow-up to match the request that produced the
        // token, apart from maxResultsPerPage and includedData.
        client.Queries[1].LastUpdatedAfter.Should().Be(client.Queries[0].LastUpdatedAfter);
    }

    [Fact]
    public async Task A_long_walk_pauses_after_the_page_cap_and_resumes_next_run()
    {
        var pages = Enumerable.Range(0, 10)
            .Select(i => MarketplaceOrderPage.Ok(new[] { Order($"O{i}") }, $"p{i + 1}", Watermark.AddHours(1)))
            .ToArray();

        var (service, client, states, _) = Build(StubClient.Pages(pages), State(Watermark), maxPages: 3);

        var result = await service.PollAsync(Connection());

        result.Outcome.Should().Be(SalesOrderPollOutcome.Paused);
        client.Queries.Should().HaveCount(3);

        // At roughly one page every three minutes a backfill takes hours. Stopping and
        // persisting the cursor means the next run resumes rather than starting again.
        states.Cursor.Should().Be("p3");
        states.Watermark.Should().Be(Watermark);
    }

    [Fact]
    public async Task A_resumed_run_sends_the_stored_cursor()
    {
        var client = StubClient.Pages(MarketplaceOrderPage.Ok(Array.Empty<MarketplaceOrderSnapshot>(), null, null));
        var (service, _, _, _) = Build(client, State(Watermark, cursor: "p7"));

        await service.PollAsync(Connection());

        client.Queries[0].PaginationToken.Should().Be("p7");
    }

    // ---- 3.6 pagination token expiry ----

    [Fact]
    public async Task An_expired_token_drops_the_cursor_so_the_next_run_restarts_from_the_watermark()
    {
        var client = StubClient.Pages(
            MarketplaceOrderPage.Failed(MarketplaceCallOutcome.PaginationTokenExpired, "expired"));

        var (service, _, states, _) = Build(client, State(Watermark, cursor: "stale-token"));

        var result = await service.PollAsync(Connection());

        result.Outcome.Should().Be(SalesOrderPollOutcome.Retryable);

        // Tokens last 24 hours and a long backfill will outlive one. Restarting from the
        // watermark is slower but correct; treating it as a hard failure would strand the
        // backfill for ever.
        states.Cursor.Should().BeNull();
        states.Watermark.Should().Be(Watermark);
    }

    // ---- failure handling ----

    [Fact]
    public async Task Rate_limiting_is_not_recorded_as_a_connection_failure()
    {
        var client = StubClient.Pages(
            MarketplaceOrderPage.Failed(MarketplaceCallOutcome.RateLimited, "throttled", TimeSpan.FromMinutes(3)));

        var (service, _, states, _) = Build(client, State(Watermark));

        var result = await service.PollAsync(Connection());

        result.Outcome.Should().Be(SalesOrderPollOutcome.Retryable);
        result.RetryAfter.Should().Be(TimeSpan.FromMinutes(3));

        // Expected, not exceptional, at one call every three minutes. A throttled connection is
        // a healthy one, and marking it as failing would bury the connections that really are.
        states.Failures.Should().Be(0);
    }

    [Fact]
    public async Task An_authorization_failure_stops_the_run_without_retrying()
    {
        var client = StubClient.Pages(
            MarketplaceOrderPage.Failed(MarketplaceCallOutcome.Unauthorized, "token rejected"));

        var (service, _, states, _) = Build(client, State(Watermark));

        var result = await service.PollAsync(Connection());

        // The client already recorded it against the connection; nothing here succeeds until a
        // person acts.
        result.Outcome.Should().Be(SalesOrderPollOutcome.Failed);
        states.Failures.Should().Be(0);
    }

    [Fact]
    public async Task A_transient_provider_error_is_recorded_and_retried()
    {
        var client = StubClient.Pages(
            MarketplaceOrderPage.Failed(MarketplaceCallOutcome.Transient, "502"));

        var (service, _, states, _) = Build(client, State(Watermark));

        var result = await service.PollAsync(Connection());

        result.Outcome.Should().Be(SalesOrderPollOutcome.Retryable);
        states.Failures.Should().Be(1);
    }

    [Fact]
    public async Task One_uningestable_order_does_not_abandon_the_page()
    {
        var client = StubClient.Pages(
            MarketplaceOrderPage.Ok(new[] { Order("GOOD-1"), Order("BAD"), Order("GOOD-2") }, null, Watermark.AddHours(1)));

        var (service, _, _, ingestion) = Build(client, State(Watermark));
        ingestion.FailFor = "BAD";

        var result = await service.PollAsync(Connection());

        // Stopping would also strand every order behind the bad one.
        result.Created.Should().Be(2);
        result.Outcome.Should().Be(SalesOrderPollOutcome.Completed);
    }

    // ---- gating ----

    [Fact]
    public async Task A_revoked_connection_is_not_polled()
    {
        var client = StubClient.Pages(MarketplaceOrderPage.Ok(Array.Empty<MarketplaceOrderSnapshot>(), null, null));
        var (service, _, _, _) = Build(client);

        var result = await service.PollAsync(Connection(IntegrationConnectionStatus.Revoked));

        result.Outcome.Should().Be(SalesOrderPollOutcome.Skipped);
        client.Queries.Should().BeEmpty();
    }

    [Fact]
    public async Task A_connection_without_the_ingest_capability_is_not_polled()
    {
        var client = StubClient.Pages(MarketplaceOrderPage.Ok(Array.Empty<MarketplaceOrderSnapshot>(), null, null));
        var (service, _, _, _) = Build(client);

        var result = await service.PollAsync(
            Connection(capabilities: IntegrationCapabilities.ListingSync));

        result.Outcome.Should().Be(SalesOrderPollOutcome.Skipped);
        client.Queries.Should().BeEmpty();
    }

    // ---- stubs ----

    private static IntegrationSyncState State(DateTime watermark, string? cursor = null) => new()
    {
        Capability = IntegrationCapabilities.SalesOrderIngest,
        Watermark = watermark,
        Cursor = cursor
    };

    private sealed class StubClient : IMarketplaceOrdersClient
    {
        private readonly Queue<MarketplaceOrderPage> _pages;

        private StubClient(IEnumerable<MarketplaceOrderPage> pages) => _pages = new Queue<MarketplaceOrderPage>(pages);

        public static StubClient Pages(params MarketplaceOrderPage[] pages) => new(pages);

        public List<MarketplaceOrderQuery> Queries { get; } = new();

        public Task<MarketplaceOrderPage> SearchOrdersAsync(
            int tenantId, Guid connectionId, MarketplaceOrderQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(_pages.Count > 0
                ? _pages.Dequeue()
                : MarketplaceOrderPage.Ok(Array.Empty<MarketplaceOrderSnapshot>(), null, null));
        }

        public Task<MarketplaceOrderResult> GetOrderAsync(
            int tenantId, Guid connectionId, string externalOrderId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubIngestion : ISalesOrderIngestionService
    {
        public string? FailFor { get; set; }

        public Task<SalesOrderIngestResult> IngestAsync(
            IntegrationConnection connection, MarketplaceOrderSnapshot snapshot, string source,
            CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot.ExternalOrderId == FailFor
                ? SalesOrderIngestResult.Failed("no")
                : SalesOrderIngestResult.Created(Guid.NewGuid()));
    }

    private sealed class StubSyncStates : IIntegrationSyncStateRepository
    {
        private readonly IntegrationSyncState? _state;

        public StubSyncStates(IntegrationSyncState? state)
        {
            _state = state;
            Watermark = state?.Watermark;
            Cursor = state?.Cursor;
        }

        public DateTime? Watermark { get; private set; }
        public string? Cursor { get; private set; }
        public int Failures { get; private set; }

        public Task<IntegrationSyncState?> GetAsync(
            Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_state);

        public Task AdvanceWatermarkAsync(
            Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
            DateTime watermark, string? cursor, CancellationToken cancellationToken = default)
        {
            // Mirrors the repository: a watermark never moves backwards.
            if (Watermark is null || watermark > Watermark)
            {
                Watermark = watermark;
            }
            Cursor = cursor;
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(
            Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
            string error, CancellationToken cancellationToken = default)
        {
            Failures++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IntegrationSyncState>> GetForConnectionAsync(
            Guid connectionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> OrderByStalenessAsync(
            IntegrationCapabilities capability, IReadOnlyList<Guid> candidateConnectionIds,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RecordAttemptAsync(
            Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubConnections : IIntegrationConnectionService
    {
        public Task<IntegrationConnection?> GetAsync(int t, Guid c, CancellationToken ct = default)
            => Task.FromResult<IntegrationConnection?>(null);
        public Task<IReadOnlyList<IntegrationConnection>> ListAsync(int t, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IntegrationConnectionResult> BeginAsync(
            int o, int t, string p, string? d, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IntegrationConnectionResult> CompleteAuthorizationAsync(
            int t, Guid p, string e, string r, IntegrationCapabilities g, string? region = null,
            CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IntegrationConnectionResult> DisconnectAsync(int t, Guid c, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task RecordCredentialRejectedAsync(int t, Guid c, string e, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task RecordTransientErrorAsync(int t, Guid c, string e, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task RecordCapabilityDeniedAsync(
            int t, Guid c, IntegrationCapabilities cap, string e, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
