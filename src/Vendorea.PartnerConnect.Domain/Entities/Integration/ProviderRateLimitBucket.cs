namespace Vendorea.PartnerConnect.Domain.Entities.Integration;

/// <summary>
/// A token bucket for one provider operation, shared across every process.
/// </summary>
/// <remarks>
/// It lives in the database rather than in memory because the API and the workers both call
/// Amazon, and the workers may run more than once. An in-memory limiter would give each process
/// a full budget and together they would exceed the real one — which Amazon answers with 429s
/// that cost more than the calls they replaced.
///
/// <see cref="BucketKey"/> is the **selling partner**, not the connection. Amazon meters per
/// application per selling partner, and two tenants may legitimately connect the same seller
/// account (a reseller managing a client's account alongside the client). Keyed by connection,
/// those two would each receive a full budget and jointly overspend the one bucket Amazon
/// actually keeps.
/// </remarks>
public class ProviderRateLimitBucket
{
    public long Id { get; set; }

    /// <summary>Provider code and selling-partner id, e.g. <c>AMAZON_SP:A3EXAMPLE1SELLER</c>.</summary>
    public string BucketKey { get; set; } = string.Empty;

    /// <summary>The operation being metered, e.g. <c>searchOrders</c>. Each has its own limit.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Tokens available at <see cref="LastRefillAt"/>. Fractional: refill is continuous.</summary>
    public double Tokens { get; set; }

    public DateTime LastRefillAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
