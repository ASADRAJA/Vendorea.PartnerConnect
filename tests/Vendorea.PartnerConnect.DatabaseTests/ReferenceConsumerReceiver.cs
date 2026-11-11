using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// A reference implementation of the Merchant360 receiver, written independently of
/// PartnerConnect's own signing code.
/// </summary>
/// <remarks>
/// Two jobs.
///
/// First, it is the counterparty in the end-to-end loop test. Everything in Phase 4 was otherwise
/// tested against stubs that accepted whatever they were given; this verifies properly, so the
/// loop proves PartnerConnect emits something a strict consumer accepts rather than something a
/// permissive mock tolerated.
///
/// Second, it is a worked example for whoever builds the real receiver (backlog 4.6). The
/// verification ladder below is the contract's, in the contract's order, with the reasons for the
/// order attached.
///
/// **The HMAC is computed here from first principles — <c>HMACSHA256</c> over
/// <c>timestamp + "." + body</c> — and deliberately does not call PartnerConnect's
/// <c>IntegrationSignature</c>.** Reusing it would make the loop test prove only that PC agrees
/// with itself. Two independent implementations agreeing is the thing worth knowing, and it is
/// what the published test vector exists to pin on both sides.
/// </remarks>
public sealed class ReferenceConsumerReceiver : HttpMessageHandler
{
    private const string TimestampHeader = "X-PC-Signature-Timestamp";
    private const string KeyIdHeader = "X-PC-Signature-KeyId";
    private const string SignatureHeader = "X-PC-Signature";

    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    private readonly Dictionary<string, string> _secretsByKeyId;

    /// <summary>
    /// The durable inbox, keyed by event id.
    /// </summary>
    /// <remarks>
    /// A dictionary here; a table in the real thing. Delivery is at-least-once, so the inbox is
    /// what makes business effects effective-once — the consumer's obligation, not the
    /// publisher's.
    /// </remarks>
    private readonly Dictionary<Guid, ReceivedEvent> _inbox = new();

    public ReferenceConsumerReceiver(params (string KeyId, string Secret)[] keys)
        => _secretsByKeyId = keys.ToDictionary(k => k.KeyId, k => k.Secret, StringComparer.Ordinal);

    /// <summary>Accepted events, in arrival order.</summary>
    public IReadOnlyList<ReceivedEvent> Received => _inbox.Values.OrderBy(e => e.ReceivedAt).ToList();

    /// <summary>Rejections, with the reason — what a consumer's 401 log would show.</summary>
    public List<string> Rejections { get; } = new();

    /// <summary>Deliveries recognised as repeats of an event already in the inbox.</summary>
    public int DuplicatesSuppressed { get; private set; }

    /// <summary>Forces the next delivery to fail, to exercise the publisher's retry path.</summary>
    public HttpStatusCode? NextResponseOverride { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(Handle(request, cancellationToken));

    private HttpResponseMessage Handle(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (NextResponseOverride is { } forced)
        {
            NextResponseOverride = null;
            return new HttpResponseMessage(forced);
        }

        // Step 1. All three headers, or 401. A missing header is not a malformed payload — it is
        // an unauthenticated request, and it must be refused before anything is parsed.
        if (!TryHeader(request, TimestampHeader, out var timestamp)
            || !TryHeader(request, KeyIdHeader, out var keyId)
            || !TryHeader(request, SignatureHeader, out var signature))
        {
            return Reject("A required signature header is missing.");
        }

        // Step 2. Within ±5 minutes. Reject, never clamp: clamping would accept a replay of a
        // capture from last week, which is exactly what binding the timestamp into the signature
        // was introduced to prevent.
        if (!DateTime.TryParse(
                timestamp, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal
                    | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var sentAt)
            || (DateTime.UtcNow - sentAt).Duration() > Tolerance)
        {
            return Reject($"The timestamp '{timestamp}' is missing, malformed, or outside the window.");
        }

        // Step 3. Resolve the secret by key id. An unknown id is a 401 rather than a fallback to
        // some default key — a fallback is how a retired secret keeps working for ever.
        if (!_secretsByKeyId.TryGetValue(keyId, out var secret))
        {
            return Reject($"Unknown signing key id '{keyId}'.");
        }

        // Step 4. Recompute over the RAW bytes as received and compare in constant time.
        // Re-serialising the parsed object first would change whitespace and key order and break
        // the comparison for a request that was perfectly valid.
        var body = request.Content is null
            ? string.Empty
            : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

        var expected = ComputeSignature(secret, timestamp, body);

        if (!FixedTimeEquals(expected, signature))
        {
            return Reject("The signature does not match the body.");
        }

        // Step 5. Only now deserialise.
        ReceivedEvent received;
        try
        {
            received = Parse(body);
        }
        catch (JsonException ex)
        {
            // Authenticated but unintelligible. A 400 rather than a 401: the caller is who they
            // say they are, and retrying will not make the payload parse.
            Rejections.Add($"Authenticated but unparseable: {ex.Message}");
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        // The inbox. An event already seen is acknowledged without being processed again —
        // delivery is at-least-once and the publisher is entitled to retry.
        if (_inbox.ContainsKey(received.EventId))
        {
            DuplicatesSuppressed++;
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        _inbox[received.EventId] = received;

        // 202: the bytes arrived and the event is in the inbox. Deliberately not a statement
        // about business processing — that is reported separately, which is the single most
        // important thing for an implementer of the real receiver to get right.
        return new HttpResponseMessage(HttpStatusCode.Accepted);
    }

    private HttpResponseMessage Reject(string reason)
    {
        Rejections.Add(reason);
        return new HttpResponseMessage(HttpStatusCode.Unauthorized);
    }

    private static bool TryHeader(HttpRequestMessage request, string name, out string value)
    {
        if (request.Headers.TryGetValues(name, out var values))
        {
            value = values.FirstOrDefault() ?? string.Empty;
            return !string.IsNullOrEmpty(value);
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// <c>"v1=" + lowercase_hex(HMAC_SHA256(secret, timestamp + "." + body))</c>.
    /// </summary>
    /// <remarks>
    /// Written out rather than delegated on purpose — see the class remarks. This is the function
    /// the real receiver must implement, and the published test vector is how it checks that it
    /// has.
    /// </remarks>
    private static string ComputeSignature(string secret, string timestamp, string body)
    {
        var signingString = timestamp + "." + body;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingString));

        return "v1=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Constant-time comparison.
    /// </summary>
    /// <remarks>
    /// A plain string comparison returns early on the first differing byte, which leaks how much
    /// of a guessed signature was correct. Over enough attempts that is a signature forgery.
    /// </remarks>
    private static bool FixedTimeEquals(string expected, string actual)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    private static ReceivedEvent Parse(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        // Required by the schema. A consumer that tolerated their absence would accept events it
        // could not route.
        foreach (var required in new[]
                 {
                     "eventId", "eventType", "eventVersion", "occurredAt", "publishedAt",
                     "organizationId", "tenantId", "integrationConnectionId", "provider",
                     "deduplicationKey", "correlationId", "payload"
                 })
        {
            if (!root.TryGetProperty(required, out _))
            {
                throw new JsonException($"The envelope is missing the required field '{required}'.");
            }
        }

        var payload = root.GetProperty("payload");
        var identity = payload.GetProperty("identity");

        return new ReceivedEvent
        {
            EventId = root.GetProperty("eventId").GetGuid(),
            EventType = root.GetProperty("eventType").GetString()!,
            DeduplicationKey = root.GetProperty("deduplicationKey").GetString()!,
            TenantId = root.GetProperty("tenantId").GetInt32(),
            OrganizationId = root.GetProperty("organizationId").GetInt32(),
            PublicationReason = root.TryGetProperty("publicationReason", out var reason)
                ? reason.GetString()
                : "ProviderChange",

            // The canonical key. Exactly one consumer sales order per tuple, for the life of the
            // order — the event id never governs that, and a republication proves why.
            Provider = identity.GetProperty("provider").GetString()!,
            IntegrationConnectionId = identity.GetProperty("integrationConnectionId").GetGuid(),
            ExternalOrderId = identity.GetProperty("externalOrderId").GetString()!,

            PcSalesOrderId = payload.GetProperty("pcSalesOrderId").GetGuid(),
            Status = payload.GetProperty("status").GetString()!,
            RawBody = body,
            ReceivedAt = DateTime.UtcNow
        };
    }

    /// <summary>What the receiver kept from one accepted delivery.</summary>
    public sealed class ReceivedEvent
    {
        public Guid EventId { get; init; }
        public string EventType { get; init; } = string.Empty;
        public string DeduplicationKey { get; init; } = string.Empty;
        public int TenantId { get; init; }
        public int OrganizationId { get; init; }
        public string? PublicationReason { get; init; }

        public string Provider { get; init; } = string.Empty;
        public Guid IntegrationConnectionId { get; init; }
        public string ExternalOrderId { get; init; } = string.Empty;

        public Guid PcSalesOrderId { get; init; }
        public string Status { get; init; } = string.Empty;
        public string RawBody { get; init; } = string.Empty;
        public DateTime ReceivedAt { get; init; }

        /// <summary>The identity tuple a consumer keys its own order on.</summary>
        public string OrderIdentityKey => $"{Provider}|{IntegrationConnectionId:N}|{ExternalOrderId}";
    }
}
