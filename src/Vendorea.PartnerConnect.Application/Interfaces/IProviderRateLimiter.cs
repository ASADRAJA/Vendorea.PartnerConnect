namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Spends a provider's rate-limit budget, atomically and across processes.
/// </summary>
/// <remarks>
/// A caller that cannot acquire is told how long to wait rather than being blocked. With
/// <c>searchOrders</c> metered at roughly one call every three minutes, blocking a worker thread
/// on a bucket would stall every other connection it was due to serve.
/// </remarks>
public interface IProviderRateLimiter
{
    /// <summary>Takes one token, or reports how long until one exists.</summary>
    Task<RateLimitLease> TryAcquireAsync(
        string bucketKey, string operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Empties a bucket after the provider says we have overspent.
    /// </summary>
    /// <remarks>
    /// Our arithmetic and Amazon's can disagree — clock skew, calls made by another integration
    /// under the same application. A 429 is the authoritative answer, and the local bucket
    /// should believe it rather than keep issuing tokens the provider will refuse.
    /// </remarks>
    Task PenalizeAsync(
        string bucketKey, string operation, TimeSpan retryAfter,
        CancellationToken cancellationToken = default);
}

/// <summary>The outcome of asking for a token.</summary>
public sealed record RateLimitLease(bool Acquired, TimeSpan RetryAfter)
{
    public static RateLimitLease Granted() => new(true, TimeSpan.Zero);

    public static RateLimitLease Denied(TimeSpan retryAfter) =>
        new(false, retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter);
}

/// <summary>Rate and burst for one operation.</summary>
public sealed record RateLimitPolicy(double RatePerSecond, double Burst)
{
    /// <summary>How long one token takes to accrue.</summary>
    public TimeSpan TokenInterval => RatePerSecond <= 0
        ? TimeSpan.MaxValue
        : TimeSpan.FromSeconds(1 / RatePerSecond);
}

/// <summary>Supplies the policy for a provider operation.</summary>
public interface IProviderRateLimitPolicyProvider
{
    RateLimitPolicy GetPolicy(string operation);
}
