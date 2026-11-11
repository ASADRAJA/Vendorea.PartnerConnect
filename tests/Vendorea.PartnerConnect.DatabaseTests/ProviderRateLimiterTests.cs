using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// The cross-process rate limiter — test matrix 3.4.
/// </summary>
/// <remarks>
/// Against real PostgreSQL because the whole point is the row lock. The acquire is one UPDATE
/// whose WHERE clause is the admission test; proving that two concurrent callers cannot both
/// take the last token requires a database that actually locks rows.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class ProviderRateLimiterTests : IAsyncLifetime
{
    private const string Key = "AMAZON_SP:A3EXAMPLE1SELLER";

    private readonly PostgresFixture _pg;

    public ProviderRateLimiterTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync() => await _pg.TruncateAsync("ProviderRateLimitBuckets");

    public Task DisposeAsync() => Task.CompletedTask;

    private ProviderRateLimiter Build(double rate, double burst, Persistence.PartnerConnectDbContext? db = null)
        => new(db ?? _pg.CreateContext(),
            new FixedPolicy(new RateLimitPolicy(rate, burst)),
            NullLogger<ProviderRateLimiter>.Instance);

    private sealed class FixedPolicy : IProviderRateLimitPolicyProvider
    {
        private readonly RateLimitPolicy _policy;
        public FixedPolicy(RateLimitPolicy policy) => _policy = policy;
        public RateLimitPolicy GetPolicy(string operation) => _policy;
    }

    [Fact]
    public async Task A_new_bucket_starts_full()
    {
        var limiter = Build(rate: 0.0056, burst: 3);

        (await limiter.TryAcquireAsync(Key, "searchOrders")).Acquired.Should().BeTrue();
        (await limiter.TryAcquireAsync(Key, "searchOrders")).Acquired.Should().BeTrue();
        (await limiter.TryAcquireAsync(Key, "searchOrders")).Acquired.Should().BeTrue();

        // A first connection gets its whole burst immediately, which is what makes the opening
        // pages of a backfill fast before the slow refill takes over.
        var denied = await limiter.TryAcquireAsync(Key, "searchOrders");
        denied.Acquired.Should().BeFalse();
    }

    [Fact]
    public async Task A_denied_caller_is_told_how_long_to_wait()
    {
        var limiter = Build(rate: 0.0056, burst: 1);

        await limiter.TryAcquireAsync(Key, "searchOrders");
        var denied = await limiter.TryAcquireAsync(Key, "searchOrders");

        denied.Acquired.Should().BeFalse();

        // One token at 0.0056/s is about 179 seconds. The caller schedules rather than blocks —
        // holding a worker thread for three minutes would stall every other connection.
        denied.RetryAfter.Should().BeCloseTo(TimeSpan.FromSeconds(179), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Tokens_refill_with_elapsed_time()
    {
        // 20/second: a tenth of a second is worth two tokens.
        var limiter = Build(rate: 20, burst: 5);

        for (var i = 0; i < 5; i++)
        {
            (await limiter.TryAcquireAsync(Key, "getOrder")).Acquired.Should().BeTrue();
        }
        (await limiter.TryAcquireAsync(Key, "getOrder")).Acquired.Should().BeFalse();

        await Task.Delay(250);

        // Refill is computed from the clock, so nothing has to keep buckets warm and an idle
        // connection costs nothing.
        (await limiter.TryAcquireAsync(Key, "getOrder")).Acquired.Should().BeTrue();
    }

    [Fact]
    public async Task Refill_never_exceeds_the_burst()
    {
        var limiter = Build(rate: 1000, burst: 2);

        await Task.Delay(50);   // worth 50 tokens at this rate

        (await limiter.TryAcquireAsync(Key, "getOrder")).Acquired.Should().BeTrue();
        (await limiter.TryAcquireAsync(Key, "getOrder")).Acquired.Should().BeTrue();

        await using var db = _pg.CreateContext();
        var bucket = await db.ProviderRateLimitBuckets.SingleAsync();

        // A bucket that accumulated unboundedly while idle would let a connection burst far past
        // what Amazon permits the moment it woke up.
        bucket.Tokens.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Separate_operations_have_separate_budgets()
    {
        var limiter = Build(rate: 0.0056, burst: 1);

        (await limiter.TryAcquireAsync(Key, "searchOrders")).Acquired.Should().BeTrue();

        // getOrder is roughly ninety times cheaper than searchOrders; one budget for both would
        // make the cheap call as scarce as the expensive one.
        (await limiter.TryAcquireAsync(Key, "getOrder")).Acquired.Should().BeTrue();
    }

    [Fact]
    public async Task Separate_selling_partners_have_separate_budgets()
    {
        var limiter = Build(rate: 0.0056, burst: 1);

        (await limiter.TryAcquireAsync("AMAZON_SP:SELLER-A", "searchOrders")).Acquired.Should().BeTrue();
        (await limiter.TryAcquireAsync("AMAZON_SP:SELLER-B", "searchOrders")).Acquired.Should().BeTrue();

        // Amazon meters per application per selling partner, so connections do not contend.
        (await limiter.TryAcquireAsync("AMAZON_SP:SELLER-A", "searchOrders")).Acquired.Should().BeFalse();
    }

    [Fact]
    public async Task Two_tenants_sharing_one_seller_account_share_its_budget()
    {
        var limiter = Build(rate: 0.0056, burst: 1);

        // A reseller may manage a client's Amazon account alongside the client's own connection.
        // Keyed by connection they would each get a full budget and jointly overspend the one
        // bucket Amazon actually keeps — so the key is the selling partner, not the connection.
        (await limiter.TryAcquireAsync(Key, "searchOrders")).Acquired.Should().BeTrue();
        (await limiter.TryAcquireAsync(Key, "searchOrders")).Acquired.Should().BeFalse();
    }

    [Fact]
    public async Task Concurrent_callers_cannot_both_take_the_last_token()
    {
        // Each caller gets its own DbContext, as separate processes would.
        var limiters = Enumerable.Range(0, 12).Select(_ => Build(rate: 0.0001, burst: 4)).ToList();

        var results = await Task.WhenAll(
            limiters.Select(l => l.TryAcquireAsync(Key, "searchOrders")));

        // Exactly four, not five and not three. The admission test is the UPDATE's WHERE clause,
        // so the row lock settles it; a read-then-write in application code could not.
        results.Count(r => r.Acquired).Should().Be(4);
    }

    [Fact]
    public async Task A_provider_throttle_empties_the_bucket_for_the_stated_window()
    {
        var limiter = Build(rate: 1000, burst: 100);

        (await limiter.TryAcquireAsync(Key, "searchOrders")).Acquired.Should().BeTrue();

        await limiter.PenalizeAsync(Key, "searchOrders", TimeSpan.FromSeconds(30));

        // Our arithmetic and Amazon's can disagree. A 429 is the authoritative answer, and the
        // local bucket believes it rather than keep issuing tokens the provider will refuse —
        // even though the configured rate would otherwise refill it instantly.
        var denied = await limiter.TryAcquireAsync(Key, "searchOrders");
        denied.Acquired.Should().BeFalse();
    }

    [Fact]
    public async Task An_unmetered_operation_is_allowed_rather_than_silently_blocked()
    {
        var limiter = Build(rate: 0, burst: 0);

        // A missing policy must not stop work without saying why.
        (await limiter.TryAcquireAsync(Key, "somethingUnmetered")).Acquired.Should().BeTrue();
    }

    [Fact]
    public async Task A_bucket_is_created_once_however_many_callers_arrive_together()
    {
        var limiters = Enumerable.Range(0, 8).Select(_ => Build(rate: 20, burst: 20)).ToList();

        await Task.WhenAll(limiters.Select(l => l.TryAcquireAsync(Key, "getOrder")));

        await using var db = _pg.CreateContext();

        // Two processes reaching a new connection at once is the normal case, not a rare one.
        (await db.ProviderRateLimitBuckets.CountAsync(b => b.BucketKey == Key)).Should().Be(1);
    }
}
