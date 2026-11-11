using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Infrastructure.Amazon;

/// <summary>
/// The Selling Partner API Orders client (v2026-01-01).
/// </summary>
/// <remarks>
/// No AWS SigV4 and no IAM: since October 2023 SP-API authenticates with a Login with Amazon
/// access token in <c>x-amz-access-token</c> and nothing else. There is no AWS credential here
/// to store or rotate.
///
/// Failures are classified rather than thrown, because the caller's recovery differs sharply by
/// kind: a 429 is a wait, an expired pagination token is a restart from the watermark, a 403 is
/// a missing role that no retry will supply.
/// </remarks>
public class AmazonOrdersClient : IMarketplaceOrdersClient
{
    private const string OrdersPath = "/orders/2026-01-01/orders";
    private const string SearchOrdersOperation = "searchOrders";
    private const string GetOrderOperation = "getOrder";
    private const string ProviderCode = "AMAZON_SP";

    private readonly HttpClient _http;
    private readonly IAmazonAccessTokenProvider _tokens;
    private readonly IIntegrationConnectionService _connections;
    private readonly IProviderRateLimiter _rateLimiter;
    private readonly IIntegrationPayloadArchiveService _archive;
    private readonly AmazonSpApiOptions _options;
    private readonly ILogger<AmazonOrdersClient> _logger;

    public AmazonOrdersClient(
        HttpClient http,
        IAmazonAccessTokenProvider tokens,
        IIntegrationConnectionService connections,
        IProviderRateLimiter rateLimiter,
        IIntegrationPayloadArchiveService archive,
        IOptions<AmazonSpApiOptions> options,
        ILogger<AmazonOrdersClient> logger)
    {
        _http = http;
        _tokens = tokens;
        _connections = connections;
        _rateLimiter = rateLimiter;
        _archive = archive;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MarketplaceOrderPage> SearchOrdersAsync(
        int tenantId, Guid connectionId, MarketplaceOrderQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!query.IsValid)
        {
            // Amazon requires exactly one of createdAfter / lastUpdatedAfter. Catching it here
            // spends no rate-limit budget discovering it.
            return MarketplaceOrderPage.Failed(
                MarketplaceCallOutcome.Permanent,
                "Exactly one of lastUpdatedAfter or createdAfter is required.");
        }

        var (connection, token, failure) = await PrepareAsync(tenantId, connectionId, cancellationToken);
        if (failure is not null)
        {
            return MarketplaceOrderPage.Failed(failure.Value.Outcome, failure.Value.Error);
        }

        var bucketKey = BucketKey(connection!);
        var lease = await _rateLimiter.TryAcquireAsync(bucketKey, SearchOrdersOperation, cancellationToken);
        if (!lease.Acquired)
        {
            // Reported as rate limiting without a request being made. The caller reschedules;
            // sending anyway would earn a 429 that costs more than the call it replaced.
            return MarketplaceOrderPage.Failed(
                MarketplaceCallOutcome.RateLimited,
                "The local rate-limit budget for this seller is exhausted.",
                lease.RetryAfter);
        }

        var url = _options.ResolveEndpoint(connection.Region) + OrdersPath + BuildQueryString(query);

        var (document, outcome, error, retryAfter) = await SendAsync(
            url, token!, connection, bucketKey, SearchOrdersOperation, cancellationToken);

        if (document is null)
        {
            return MarketplaceOrderPage.Failed(outcome, error ?? "Unknown error", retryAfter);
        }

        using (document)
        {
            var root = document.RootElement;

            var orders = new List<MarketplaceOrderSnapshot>();
            if (root.TryGetProperty("orders", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var order in array.EnumerateArray())
                {
                    var snapshot = AmazonOrderMapper.Map(order);
                    orders.Add(snapshot);

                    // Captured per order rather than per page, because the question is always
                    // about one order. The archive redacts before storing and expires on its own.
                    await _archive.CaptureAsync(
                        connection, IntegrationPayloadKind.OrderSnapshot,
                        snapshot.ExternalOrderId, order.GetRawText(),
                        cancellationToken: cancellationToken);
                }
            }

            string? nextToken = null;
            if (root.TryGetProperty("pagination", out var pagination) &&
                pagination.ValueKind == JsonValueKind.Object &&
                pagination.TryGetProperty("nextToken", out var next) &&
                next.ValueKind == JsonValueKind.String)
            {
                nextToken = next.GetString();
            }

            // The boundary Amazon declares for this page set, not the newest row in it. An order
            // modified during the walk may appear on no page at all; advancing to the maximum
            // timestamp seen would skip it permanently.
            var boundary = ReadDateTime(root, query.LastUpdatedAfter.HasValue
                ? "lastUpdatedBefore"
                : "createdBefore");

            return MarketplaceOrderPage.Ok(orders, nextToken, boundary);
        }
    }

    public async Task<MarketplaceOrderResult> GetOrderAsync(
        int tenantId, Guid connectionId, string externalOrderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalOrderId))
        {
            return MarketplaceOrderResult.Failed(
                MarketplaceCallOutcome.Permanent, "An order id is required.");
        }

        var (connection, token, failure) = await PrepareAsync(tenantId, connectionId, cancellationToken);
        if (failure is not null)
        {
            return MarketplaceOrderResult.Failed(failure.Value.Outcome, failure.Value.Error);
        }

        var bucketKey = BucketKey(connection!);
        var lease = await _rateLimiter.TryAcquireAsync(bucketKey, GetOrderOperation, cancellationToken);
        if (!lease.Acquired)
        {
            return MarketplaceOrderResult.Failed(
                MarketplaceCallOutcome.RateLimited,
                "The local rate-limit budget for this seller is exhausted.",
                lease.RetryAfter);
        }

        var url = _options.ResolveEndpoint(connection.Region)
            + $"{OrdersPath}/{Uri.EscapeDataString(externalOrderId)}"
            + $"?includedData={string.Join(',', _options.IncludedData)}";

        var (document, outcome, error, retryAfter) = await SendAsync(
            url, token!, connection, bucketKey, GetOrderOperation, cancellationToken);

        if (document is null)
        {
            return MarketplaceOrderResult.Failed(outcome, error ?? "Unknown error", retryAfter);
        }

        using (document)
        {
            // getOrder returns the order either at the root or wrapped; both shapes are accepted
            // so a wrapper change does not become a silent empty ingest.
            var root = document.RootElement;
            var order = root.TryGetProperty("order", out var wrapped) ? wrapped : root;
            var snapshot = AmazonOrderMapper.Map(order);

            await _archive.CaptureAsync(
                connection, IntegrationPayloadKind.OrderSnapshot,
                snapshot.ExternalOrderId, order.GetRawText(),
                cancellationToken: cancellationToken);

            return MarketplaceOrderResult.Ok(snapshot);
        }
    }

    // ---- shared plumbing ----

    private async Task<(IntegrationConnection? Connection, string? Token,
        (MarketplaceCallOutcome Outcome, string Error)? Failure)> PrepareAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken)
    {
        var connection = await _connections.GetAsync(tenantId, connectionId, cancellationToken);
        if (connection is null)
        {
            return (null, null, (MarketplaceCallOutcome.Permanent, "Connection not found."));
        }

        if (!connection.GrantedCapabilities.HasFlag(IntegrationCapabilities.SalesOrderIngest))
        {
            // Checked before the call rather than after a 403. Spending a three-minute
            // rate-limit slot to be told something we already knew is the expensive way to fail.
            return (null, null, (MarketplaceCallOutcome.Forbidden,
                "This connection does not have the sales-order ingest capability."));
        }

        var token = await _tokens.GetAccessTokenAsync(tenantId, connectionId, cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            // The provider already recorded why against the connection — expired credentials,
            // a revoked connection, or a transient refresh failure.
            return (null, null, (MarketplaceCallOutcome.Unauthorized,
                "No usable access token for this connection."));
        }

        return (connection, token, null);
    }

    private async Task<(JsonDocument? Document, MarketplaceCallOutcome Outcome,
        string? Error, TimeSpan? RetryAfter)> SendAsync(
        string url, string accessToken, IntegrationConnection connection,
        string bucketKey, string operation, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-amz-access-token", accessToken);

            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return (JsonDocument.Parse(body), MarketplaceCallOutcome.Ok, null, null);
            }

            var outcome = Classify(response.StatusCode, body);
            var error = Summarise(body, response.StatusCode);

            if (outcome == MarketplaceCallOutcome.RateLimited)
            {
                // Amazon is the authority on its own budget. Our arithmetic can disagree with it
                // — clock skew, or another integration calling under the same application — and
                // when it does, the provider wins.
                await _rateLimiter.PenalizeAsync(
                    bucketKey, operation,
                    RetryAfter(response) ?? TimeSpan.FromSeconds(60), cancellationToken);
            }
            else if (outcome == MarketplaceCallOutcome.Unauthorized)
            {
                // The token was accepted by LWA and rejected here, so the grant itself is gone.
                await _connections.RecordCredentialRejectedAsync(
                    connection.TenantId, connection.Id, error, cancellationToken);
            }
            else if (outcome is MarketplaceCallOutcome.Forbidden or MarketplaceCallOutcome.Permanent)
            {
                await _connections.RecordTransientErrorAsync(
                    connection.TenantId, connection.Id, error, cancellationToken);
            }

            // The error body too — a provider error is exactly the thing somebody will want to
            // see verbatim later, and it goes through the same redaction and the same expiry.
            await _archive.CaptureAsync(
                connection, IntegrationPayloadKind.ProviderError,
                externalReference: null, body, cancellationToken: cancellationToken);

            // The URL is not logged: it carries the query window and the pagination token.
            _logger.LogWarning(
                "Amazon Orders call for connection {ConnectionId} returned {StatusCode} ({Outcome}): {Error}",
                connection.Id, (int)response.StatusCode, outcome, error);

            return (null, outcome, error, RetryAfter(response));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Amazon Orders returned a body that is not JSON: {Message}", ex.Message);
            return (null, MarketplaceCallOutcome.Transient, "The provider returned an unreadable response.", null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Amazon Orders could not be reached: {Message}", ex.Message);
            return (null, MarketplaceCallOutcome.Transient, "Amazon could not be reached.", null);
        }
    }

    private static MarketplaceCallOutcome Classify(HttpStatusCode status, string body)
    {
        if (status == HttpStatusCode.TooManyRequests) return MarketplaceCallOutcome.RateLimited;
        if (status == HttpStatusCode.Unauthorized) return MarketplaceCallOutcome.Unauthorized;
        if (status == HttpStatusCode.Forbidden) return MarketplaceCallOutcome.Forbidden;
        if ((int)status >= 500) return MarketplaceCallOutcome.Transient;

        // An expired pagination token arrives as an ordinary 400. There is no dedicated code for
        // it, so it is identified from the message — a heuristic, and deliberately a narrow one:
        // misreading some other 400 as "expired" would restart a walk that should have stopped.
        if (status == HttpStatusCode.BadRequest && LooksLikeExpiredToken(body))
        {
            return MarketplaceCallOutcome.PaginationTokenExpired;
        }

        return MarketplaceCallOutcome.Permanent;
    }

    private static bool LooksLikeExpiredToken(string body) =>
        body.Contains("token", StringComparison.OrdinalIgnoreCase)
        && (body.Contains("expired", StringComparison.OrdinalIgnoreCase)
            || body.Contains("no longer valid", StringComparison.OrdinalIgnoreCase));

    /// <summary>Amazon's own error text, which is safe to log and useful to an operator.</summary>
    private static string Summarise(string body, HttpStatusCode status)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array &&
                errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                var code = first.TryGetProperty("code", out var c) ? c.GetString() : null;
                var message = first.TryGetProperty("message", out var m) ? m.GetString() : null;
                if (code is not null || message is not null)
                {
                    return $"{code}: {message}".Trim(':', ' ');
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON; fall through to the status code.
        }

        return $"HTTP {(int)status}";
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var delta = response.Headers.RetryAfter?.Delta;
        if (delta.HasValue) return delta;

        var date = response.Headers.RetryAfter?.Date;
        return date.HasValue ? date.Value - DateTimeOffset.UtcNow : null;
    }

    /// <summary>
    /// The selling partner, not the connection.
    /// </summary>
    /// <remarks>
    /// Amazon meters per application per selling partner. Two tenants may legitimately connect
    /// the same seller account, and keyed by connection they would each receive a full budget
    /// and jointly overspend the single bucket Amazon actually keeps. The connection id is used
    /// only before authorization completes, when there is no account id yet and no calls to make.
    /// </remarks>
    private static string BucketKey(IntegrationConnection connection) =>
        $"{ProviderCode}:{connection.ExternalAccountId ?? connection.Id.ToString()}";

    private static DateTime? ReadDateTime(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && DateTime.TryParse(
            value.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    private string BuildQueryString(MarketplaceOrderQuery query)
    {
        var parts = new List<string>();

        // A continuation must repeat every value of the request that produced it, except
        // maxResultsPerPage and includedData. The caller carries the original query forward for
        // exactly this reason.
        if (query.PaginationToken is not null)
        {
            parts.Add("paginationToken=" + Uri.EscapeDataString(query.PaginationToken));
        }

        if (query.LastUpdatedAfter.HasValue) parts.Add("lastUpdatedAfter=" + Iso(query.LastUpdatedAfter.Value));
        if (query.LastUpdatedBefore.HasValue) parts.Add("lastUpdatedBefore=" + Iso(query.LastUpdatedBefore.Value));
        if (query.CreatedAfter.HasValue) parts.Add("createdAfter=" + Iso(query.CreatedAfter.Value));
        if (query.CreatedBefore.HasValue) parts.Add("createdBefore=" + Iso(query.CreatedBefore.Value));

        if (query.MarketplaceIds is { Count: > 0 })
        {
            parts.Add("marketplaceIds=" + Uri.EscapeDataString(string.Join(',', query.MarketplaceIds)));
        }

        parts.Add("maxResultsPerPage=" +
            (query.MaxResultsPerPage ?? _options.MaxResultsPerPage).ToString(CultureInfo.InvariantCulture));

        if (_options.IncludedData.Count > 0)
        {
            parts.Add("includedData=" + Uri.EscapeDataString(string.Join(',', _options.IncludedData)));
        }

        return "?" + string.Join('&', parts);
    }

    /// <summary>UTC, ISO-8601, second precision — the format Amazon's date filters accept.</summary>
    private static string Iso(DateTime value) =>
        Uri.EscapeDataString(
            value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
}
