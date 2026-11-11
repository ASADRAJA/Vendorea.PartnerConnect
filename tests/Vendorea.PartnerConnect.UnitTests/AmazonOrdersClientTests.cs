using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Infrastructure.Amazon;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests;

/// <summary>
/// The Orders client's request shaping and failure classification.
/// </summary>
/// <remarks>
/// Classification is the part worth testing hardest. The caller's recovery differs sharply by
/// kind — a 429 is a wait, an expired pagination token is a restart from the watermark, a 403 is
/// a missing role no retry will supply — so a misclassification either strands a backfill
/// permanently or retries something that can never succeed.
/// </remarks>
public class AmazonOrdersClientTests
{
    private const int TenantId = 7;
    private static readonly Guid ConnectionId = Guid.NewGuid();

    private const string EmptyPage = """{ "orders": [], "pagination": {} }""";

    private static (AmazonOrdersClient Client, StubHandler Handler, StubConnections Connections) Build(
        HttpStatusCode status = HttpStatusCode.OK,
        string body = EmptyPage,
        IntegrationCapabilities capabilities = IntegrationCapabilities.SalesOrderIngest,
        string? accessToken = "Atza|ACCESS",
        string? region = "NA",
        StubRateLimiter? rateLimiter = null,
        StubArchive? archive = null)
    {
        var handler = new StubHandler(status, body);
        var connections = new StubConnections(capabilities, region);

        var client = new AmazonOrdersClient(
            new HttpClient(handler),
            new StubTokenProvider(accessToken),
            connections,
            rateLimiter ?? new StubRateLimiter(),
            archive ?? new StubArchive(),
            Options.Create(new AmazonSpApiOptions()),
            NullLogger<AmazonOrdersClient>.Instance);

        return (client, handler, connections);
    }

    private static MarketplaceOrderQuery Incremental => new()
    {
        LastUpdatedAfter = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc)
    };

    // ---- request shaping ----

    [Fact]
    public async Task The_request_targets_the_v2026_orders_path_on_the_connections_region()
    {
        var (client, handler, _) = Build(region: "EU");

        await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        handler.LastUrl.Should().StartWith("https://sellingpartnerapi-eu.amazon.com/orders/2026-01-01/orders");
    }

    [Fact]
    public async Task The_access_token_travels_in_the_x_amz_access_token_header()
    {
        var (client, handler, _) = Build();

        await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        // SP-API dropped SigV4 in 2023; this header is the whole of the authentication.
        handler.LastHeaders.Should().ContainKey("x-amz-access-token");
        handler.LastHeaders["x-amz-access-token"].Should().Be("Atza|ACCESS");
        handler.LastHeaders.Should().NotContainKey("Authorization");
    }

    [Fact]
    public async Task An_incremental_query_sends_lastUpdatedAfter_and_includedData()
    {
        var (client, handler, _) = Build();

        await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        handler.LastUrl.Should().Contain("lastUpdatedAfter=2026-11-05T12%3A00%3A00Z");
        handler.LastUrl.Should().Contain("maxResultsPerPage=100");
        handler.LastUrl.Should().Contain("PROCEEDS");
    }

    [Fact]
    public async Task A_query_with_neither_bound_is_refused_before_any_call_is_made()
    {
        var (client, handler, _) = Build();

        var page = await client.SearchOrdersAsync(TenantId, ConnectionId, new MarketplaceOrderQuery());

        page.Outcome.Should().Be(MarketplaceCallOutcome.Permanent);

        // searchOrders allows roughly one call every three minutes. Discovering a malformed
        // query by spending one of those is the expensive way to fail.
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task A_continuation_carries_the_token_and_the_original_window()
    {
        var (client, handler, _) = Build();

        await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental with
        {
            PaginationToken = "next-page-token"
        });

        // Amazon requires every value of the follow-up to match the request that produced the
        // token, apart from maxResultsPerPage and includedData.
        handler.LastUrl.Should().Contain("paginationToken=next-page-token");
        handler.LastUrl.Should().Contain("lastUpdatedAfter=");
    }

    [Fact]
    public async Task A_connection_without_the_ingest_capability_never_reaches_Amazon()
    {
        var (client, handler, _) = Build(capabilities: IntegrationCapabilities.ListingSync);

        var page = await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        page.Outcome.Should().Be(MarketplaceCallOutcome.Forbidden);
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task A_connection_with_no_usable_token_never_reaches_Amazon()
    {
        var (client, handler, _) = Build(accessToken: null);

        var page = await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        page.Outcome.Should().Be(MarketplaceCallOutcome.Unauthorized);
        handler.Calls.Should().Be(0);
    }

    // ---- response reading ----

    [Fact]
    public async Task The_page_boundary_comes_from_the_response_not_the_rows()
    {
        var (client, _, _) = Build(body: """
        {
          "orders": [
            { "orderId": "1", "lastUpdatedTime": "2026-11-05T12:30:00Z" },
            { "orderId": "2", "lastUpdatedTime": "2026-11-05T12:45:00Z" }
          ],
          "pagination": { "nextToken": "tok" },
          "lastUpdatedBefore": "2026-11-05T13:00:00Z"
        }
        """);

        var page = await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        page.Success.Should().BeTrue();
        page.Orders.Should().HaveCount(2);
        page.NextToken.Should().Be("tok");

        // 13:00, not 12:45. An order modified during the walk may appear on no page at all;
        // advancing to the newest row seen would skip it for ever.
        page.Boundary.Should().Be(new DateTime(2026, 11, 5, 13, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task A_final_page_reports_no_continuation()
    {
        var (client, _, _) = Build(body: """{ "orders": [{ "orderId": "1" }], "pagination": {} }""");

        (await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental))
            .NextToken.Should().BeNull();
    }

    // ---- failure classification ----

    [Fact]
    public async Task A_429_is_rate_limiting_and_carries_its_retry_delay()
    {
        var (client, _, _) = Build(HttpStatusCode.TooManyRequests, """{"errors":[{"code":"QuotaExceeded"}]}""");

        var page = await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        page.Outcome.Should().Be(MarketplaceCallOutcome.RateLimited);
        page.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task An_expired_pagination_token_is_its_own_outcome()
    {
        var (client, _, _) = Build(HttpStatusCode.BadRequest, """
        {"errors":[{"code":"InvalidInput","message":"The pagination token has expired."}]}
        """);

        var page = await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental with
        {
            PaginationToken = "stale"
        });

        // Tokens expire after 24 hours, and a long backfill will hit this. Treating it as a
        // generic bad request would strand the walk permanently.
        page.Outcome.Should().Be(MarketplaceCallOutcome.PaginationTokenExpired);
    }

    [Fact]
    public async Task Another_kind_of_400_is_not_mistaken_for_an_expired_token()
    {
        var (client, _, _) = Build(HttpStatusCode.BadRequest, """
        {"errors":[{"code":"InvalidInput","message":"marketplaceIds is not valid."}]}
        """);

        // Misreading this as "expired" would restart a walk that should have stopped.
        (await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental))
            .Outcome.Should().Be(MarketplaceCallOutcome.Permanent);
    }

    [Fact]
    public async Task A_401_marks_the_connection_as_needing_reauthorization()
    {
        var (client, _, connections) = Build(HttpStatusCode.Unauthorized, """{"errors":[{"code":"Unauthorized"}]}""");

        var page = await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        page.Outcome.Should().Be(MarketplaceCallOutcome.Unauthorized);

        // LWA minted the token and SP-API rejected it, so the grant itself is gone.
        connections.CredentialRejected.Should().BeTrue();
    }

    [Fact]
    public async Task A_403_is_a_missing_role_and_does_not_revoke_anything()
    {
        var (client, _, connections) = Build(HttpStatusCode.Forbidden, """{"errors":[{"code":"Unauthorized"}]}""");

        var page = await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        page.Outcome.Should().Be(MarketplaceCallOutcome.Forbidden);

        // The credentials are fine; the application lacks a role. Expiring the connection would
        // send the merchant to reauthorize, which would not help.
        connections.CredentialRejected.Should().BeFalse();
    }

    [Fact]
    public async Task A_500_is_transient()
    {
        var (client, _, _) = Build(HttpStatusCode.InternalServerError, "{}");

        (await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental))
            .Outcome.Should().Be(MarketplaceCallOutcome.Transient);
    }

    [Fact]
    public async Task An_unreadable_body_is_transient_rather_than_a_crash()
    {
        var (client, _, _) = Build(HttpStatusCode.OK, "<html>not json</html>");

        (await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental))
            .Outcome.Should().Be(MarketplaceCallOutcome.Transient);
    }

    [Fact]
    public async Task Amazons_error_code_is_surfaced_to_the_caller()
    {
        var (client, _, _) = Build(HttpStatusCode.BadRequest, """
        {"errors":[{"code":"InvalidInput","message":"marketplaceIds is not valid."}]}
        """);

        (await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental))
            .Error.Should().Contain("InvalidInput");
    }

    // ---- getOrder ----

    [Fact]
    public async Task GetOrder_targets_the_order_by_id_and_maps_it()
    {
        var (client, handler, _) = Build(body: """
        { "orderId": "111-2223334-5556667", "fulfillment": { "fulfillmentStatus": "UNSHIPPED" } }
        """);

        var result = await client.GetOrderAsync(TenantId, ConnectionId, "111-2223334-5556667");

        result.Success.Should().BeTrue();
        result.Order!.ExternalOrderId.Should().Be("111-2223334-5556667");
        handler.LastUrl.Should().Contain("/orders/2026-01-01/orders/111-2223334-5556667");
    }

    [Fact]
    public async Task GetOrder_accepts_the_order_wrapped_or_at_the_root()
    {
        var (client, _, _) = Build(body: """{ "order": { "orderId": "111-0000000-0000000" } }""");

        // Accepting both shapes means a wrapper change does not become a silent empty ingest.
        (await client.GetOrderAsync(TenantId, ConnectionId, "111-0000000-0000000"))
            .Order!.ExternalOrderId.Should().Be("111-0000000-0000000");
    }

    // ---- rate limiting ----

    [Fact]
    public async Task An_exhausted_local_budget_stops_the_call_before_it_is_made()
    {
        var limiter = new StubRateLimiter { Allow = false, RetryAfter = TimeSpan.FromSeconds(179) };
        var (client, handler, _) = Build(rateLimiter: limiter);

        var page = await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        page.Outcome.Should().Be(MarketplaceCallOutcome.RateLimited);
        page.RetryAfter.Should().Be(TimeSpan.FromSeconds(179));

        // Sending anyway would earn a 429 that costs more than the call it replaced.
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task The_budget_is_keyed_on_the_selling_partner_not_the_connection()
    {
        var limiter = new StubRateLimiter();
        var (client, _, _) = Build(rateLimiter: limiter);

        await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        // Two tenants may connect the same seller account; keyed by connection they would each
        // get a full budget and jointly overspend the one bucket Amazon keeps.
        limiter.LastKey.Should().Be("AMAZON_SP:A3EXAMPLE1SELLER");
        limiter.LastOperation.Should().Be("searchOrders");
    }

    [Fact]
    public async Task getOrder_is_metered_separately_from_searchOrders()
    {
        var limiter = new StubRateLimiter();
        var (client, _, _) = Build(body: """{ "orderId": "1" }""", rateLimiter: limiter);

        await client.GetOrderAsync(TenantId, ConnectionId, "1");

        // getOrder is roughly ninety times cheaper. One budget for both would make the cheap
        // call as scarce as the expensive one.
        limiter.LastOperation.Should().Be("getOrder");
    }

    [Fact]
    public async Task A_provider_429_empties_the_local_bucket()
    {
        var limiter = new StubRateLimiter();
        var (client, _, _) = Build(HttpStatusCode.TooManyRequests, "{}", rateLimiter: limiter);

        await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        // Amazon is the authority on its own budget; when its answer and our arithmetic
        // disagree, the provider wins.
        limiter.Penalized.Should().BeTrue();
        limiter.PenaltyWindow.Should().Be(TimeSpan.FromSeconds(60));
    }

    // ---- payload archiving ----

    [Fact]
    public async Task Each_order_on_a_page_is_archived_under_its_own_id()
    {
        var archive = new StubArchive();
        var (client, _, _) = Build(body: """
        { "orders": [ { "orderId": "111-0000001-0000001" }, { "orderId": "111-0000002-0000002" } ],
          "pagination": {} }
        """, archive: archive);

        await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        // Per order, not per page: the question is always about one order, and a page-level
        // capture would mean grepping through a hundred of them.
        archive.Captures.Should().HaveCount(2);
        archive.Captures.Select(c => c.Reference)
            .Should().BeEquivalentTo(new[] { "111-0000001-0000001", "111-0000002-0000002" });
        archive.Captures.Should().OnlyContain(c => c.Kind == IntegrationPayloadKind.OrderSnapshot);
    }

    [Fact]
    public async Task A_provider_error_body_is_archived()
    {
        var archive = new StubArchive();
        var (client, _, _) = Build(HttpStatusCode.BadRequest,
            """{"errors":[{"code":"InvalidInput","message":"bad"}]}""", archive: archive);

        await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        // A provider error is exactly what somebody will want to read verbatim later.
        archive.Captures.Should().ContainSingle();
        archive.Captures[0].Kind.Should().Be(IntegrationPayloadKind.ProviderError);
        archive.Captures[0].Payload.Should().Contain("InvalidInput");
    }

    [Fact]
    public async Task Nothing_is_archived_when_the_call_never_reaches_Amazon()
    {
        var archive = new StubArchive();
        var (client, _, _) = Build(
            capabilities: IntegrationCapabilities.ListingSync, archive: archive);

        await client.SearchOrdersAsync(TenantId, ConnectionId, Incremental);

        archive.Captures.Should().BeEmpty();
    }

    // ---- stubs ----

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public int Calls { get; private set; }
        public string? LastUrl { get; private set; }
        public Dictionary<string, string> LastHeaders { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUrl = request.RequestUri?.ToString();
            LastHeaders.Clear();
            foreach (var header in request.Headers)
            {
                LastHeaders[header.Key] = string.Join(',', header.Value);
            }

            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };

            if (_status == HttpStatusCode.TooManyRequests)
            {
                response.Headers.Add("Retry-After", "60");
            }

            return Task.FromResult(response);
        }
    }

    private sealed class StubArchive : IIntegrationPayloadArchiveService
    {
        public List<(IntegrationPayloadKind Kind, string? Reference, string Payload)> Captures { get; } = new();

        public Task CaptureAsync(
            IntegrationConnection connection, IntegrationPayloadKind kind,
            string? externalReference, string rawPayload, string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            Captures.Add((kind, externalReference, rawPayload));
            return Task.CompletedTask;
        }
    }

    private sealed class StubRateLimiter : IProviderRateLimiter
    {
        public bool Allow { get; set; } = true;
        public TimeSpan RetryAfter { get; set; } = TimeSpan.Zero;

        public string? LastKey { get; private set; }
        public string? LastOperation { get; private set; }
        public bool Penalized { get; private set; }
        public TimeSpan? PenaltyWindow { get; private set; }

        public Task<RateLimitLease> TryAcquireAsync(
            string bucketKey, string operation, CancellationToken cancellationToken = default)
        {
            LastKey = bucketKey;
            LastOperation = operation;
            return Task.FromResult(Allow ? RateLimitLease.Granted() : RateLimitLease.Denied(RetryAfter));
        }

        public Task PenalizeAsync(
            string bucketKey, string operation, TimeSpan retryAfter,
            CancellationToken cancellationToken = default)
        {
            Penalized = true;
            PenaltyWindow = retryAfter;
            return Task.CompletedTask;
        }
    }

    private sealed class StubTokenProvider : IAmazonAccessTokenProvider
    {
        private readonly string? _token;
        public StubTokenProvider(string? token) => _token = token;

        public Task<string?> GetAccessTokenAsync(
            int tenantId, Guid connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_token);
    }

    private sealed class StubConnections : IIntegrationConnectionService
    {
        private readonly IntegrationConnection _connection;

        public StubConnections(IntegrationCapabilities capabilities, string? region)
            => _connection = new IntegrationConnection
            {
                Id = ConnectionId,
                TenantId = TenantId,
                ExternalAccountId = "A3EXAMPLE1SELLER",
                Region = region,
                Status = IntegrationConnectionStatus.Connected,
                GrantedCapabilities = capabilities
            };

        public bool CredentialRejected { get; private set; }
        public bool TransientRecorded { get; private set; }

        public Task<IntegrationConnection?> GetAsync(
            int tenantId, Guid connectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(tenantId == TenantId ? _connection : null);

        public Task RecordCredentialRejectedAsync(
            int tenantId, Guid connectionId, string error, CancellationToken cancellationToken = default)
        {
            CredentialRejected = true;
            return Task.CompletedTask;
        }

        public Task RecordTransientErrorAsync(
            int tenantId, Guid connectionId, string error, CancellationToken cancellationToken = default)
        {
            TransientRecorded = true;
            return Task.CompletedTask;
        }

        // Not exercised here.
        public Task<IReadOnlyList<IntegrationConnection>> ListAsync(int t, CancellationToken c = default)
            => throw new NotSupportedException();
        public Task<IntegrationConnectionResult> BeginAsync(
            int o, int t, string p, string? d, CancellationToken c = default)
            => throw new NotSupportedException();
        public Task<IntegrationConnectionResult> CompleteAuthorizationAsync(
            int t, Guid p, string e, string r, IntegrationCapabilities g, string? region = null,
            CancellationToken c = default)
            => throw new NotSupportedException();
        public Task<IntegrationConnectionResult> DisconnectAsync(int t, Guid c2, CancellationToken c = default)
            => throw new NotSupportedException();
        public Task RecordCapabilityDeniedAsync(
            int t, Guid c2, IntegrationCapabilities cap, string e, CancellationToken c = default)
            => throw new NotSupportedException();
    }
}
