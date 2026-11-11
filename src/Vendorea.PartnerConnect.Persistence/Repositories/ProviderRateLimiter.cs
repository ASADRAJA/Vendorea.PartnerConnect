using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// A PostgreSQL-backed token bucket.
/// </summary>
/// <remarks>
/// The whole acquire is one UPDATE. PostgreSQL takes a row lock for the duration, so the refill
/// and the decrement cannot interleave with another process doing the same thing — which a
/// read-then-write in application code could not promise however carefully it was written.
///
/// Refill is computed from the clock rather than scheduled: tokens accrue continuously at the
/// policy rate between one acquire and the next, so no background job has to keep buckets warm
/// and an idle connection costs nothing.
/// </remarks>
public class ProviderRateLimiter : IProviderRateLimiter
{
    private readonly PartnerConnectDbContext _context;
    private readonly IProviderRateLimitPolicyProvider _policies;
    private readonly ILogger<ProviderRateLimiter> _logger;

    public ProviderRateLimiter(
        PartnerConnectDbContext context,
        IProviderRateLimitPolicyProvider policies,
        ILogger<ProviderRateLimiter> logger)
    {
        _context = context;
        _policies = policies;
        _logger = logger;
    }

    public async Task<RateLimitLease> TryAcquireAsync(
        string bucketKey, string operation, CancellationToken cancellationToken = default)
    {
        var policy = _policies.GetPolicy(operation);
        if (policy.RatePerSecond <= 0)
        {
            // An unmetered operation. Explicitly allowed rather than silently denied, so a
            // missing policy does not stop work without saying why.
            return RateLimitLease.Granted();
        }

        await EnsureBucketAsync(bucketKey, operation, policy, cancellationToken);

        // One statement: refill by elapsed time, cap at the burst, spend one token — and update
        // nothing at all unless a whole token is available. The WHERE clause is the admission
        // test, so a denied request leaves the row untouched.
        const string sql = """
            UPDATE "ProviderRateLimitBuckets"
            SET "Tokens" = LEAST(@burst, "Tokens" +
                    EXTRACT(EPOCH FROM (now() AT TIME ZONE 'utc' - "LastRefillAt")) * @rate) - 1,
                "LastRefillAt" = now() AT TIME ZONE 'utc'
            WHERE "BucketKey" = @key AND "Operation" = @op
              AND LEAST(@burst, "Tokens" +
                    EXTRACT(EPOCH FROM (now() AT TIME ZONE 'utc' - "LastRefillAt")) * @rate) >= 1
            """;

        var updated = await _context.Database.ExecuteSqlRawAsync(
            sql,
            new[]
            {
                new NpgsqlParameter("burst", policy.Burst),
                new NpgsqlParameter("rate", policy.RatePerSecond),
                new NpgsqlParameter("key", bucketKey),
                new NpgsqlParameter("op", operation)
            },
            cancellationToken);

        if (updated > 0)
        {
            return RateLimitLease.Granted();
        }

        return RateLimitLease.Denied(await TimeUntilNextTokenAsync(bucketKey, operation, policy, cancellationToken));
    }

    public async Task PenalizeAsync(
        string bucketKey, string operation, TimeSpan retryAfter,
        CancellationToken cancellationToken = default)
    {
        var policy = _policies.GetPolicy(operation);
        await EnsureBucketAsync(bucketKey, operation, policy, cancellationToken);

        // Backdating LastRefillAt is how a wait longer than one token's worth is expressed: the
        // bucket is empty AND will not refill until the provider's own retry window has passed.
        var seconds = Math.Max(0, retryAfter.TotalSeconds);

        const string sql = """
            UPDATE "ProviderRateLimitBuckets"
            SET "Tokens" = 0,
                "LastRefillAt" = (now() AT TIME ZONE 'utc') + (@seconds * INTERVAL '1 second')
            WHERE "BucketKey" = @key AND "Operation" = @op
            """;

        await _context.Database.ExecuteSqlRawAsync(
            sql,
            new[]
            {
                new NpgsqlParameter("seconds", seconds),
                new NpgsqlParameter("key", bucketKey),
                new NpgsqlParameter("op", operation)
            },
            cancellationToken);

        _logger.LogWarning(
            "Rate-limit bucket {BucketKey}/{Operation} emptied for {Seconds}s after the provider throttled us.",
            bucketKey, operation, seconds);
    }

    /// <summary>
    /// Creates the bucket if it is new, starting full.
    /// </summary>
    /// <remarks>
    /// <c>ON CONFLICT DO NOTHING</c> rather than a read-then-insert: two processes reaching a new
    /// connection at once is the normal case, not a rare one, and the unique index should settle
    /// it rather than a retry loop.
    /// </remarks>
    private async Task EnsureBucketAsync(
        string bucketKey, string operation, RateLimitPolicy policy, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO "ProviderRateLimitBuckets"
                ("BucketKey", "Operation", "Tokens", "LastRefillAt", "CreatedAt")
            VALUES (@key, @op, @burst, now() AT TIME ZONE 'utc', now() AT TIME ZONE 'utc')
            ON CONFLICT ("BucketKey", "Operation") DO NOTHING
            """;

        await _context.Database.ExecuteSqlRawAsync(
            sql,
            new[]
            {
                new NpgsqlParameter("key", bucketKey),
                new NpgsqlParameter("op", operation),
                new NpgsqlParameter("burst", policy.Burst)
            },
            cancellationToken);
    }

    private async Task<TimeSpan> TimeUntilNextTokenAsync(
        string bucketKey, string operation, RateLimitPolicy policy, CancellationToken cancellationToken)
    {
        var bucket = await _context.ProviderRateLimitBuckets
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BucketKey == bucketKey && b.Operation == operation, cancellationToken);

        if (bucket is null)
        {
            return policy.TokenInterval;
        }

        var elapsed = (DateTime.UtcNow - bucket.LastRefillAt).TotalSeconds;
        var available = Math.Min(policy.Burst, bucket.Tokens + elapsed * policy.RatePerSecond);
        var shortfall = 1 - available;

        if (shortfall <= 0)
        {
            // A token appeared between the UPDATE and this read. Report no wait rather than a
            // negative one; the caller will simply try again.
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(shortfall / policy.RatePerSecond);
    }
}
