using FluentAssertions;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests;

/// <summary>
/// The return-URL allow-list. An OAuth callback that redirects wherever it is told is a standing
/// phishing primitive, so these cases are the specification, not illustration.
/// </summary>
public class IntegrationReturnUrlTests
{
    private static IntegrationAuthorizationOptions WithAllowed(params string[] urls)
        => new() { AllowedReturnUrls = urls.ToList() };

    [Fact]
    public void An_empty_allow_list_permits_nothing()
    {
        var options = new IntegrationAuthorizationOptions();

        options.IsReturnUrlAllowed("https://app.merchant360.com/integrations").Should().BeFalse();
    }

    [Theory]
    [InlineData("https://app.merchant360.com/integrations")]
    [InlineData("https://app.merchant360.com/integrations/amazon")]
    [InlineData("https://APP.merchant360.com/integrations")]   // host comparison is case-insensitive
    public void An_allowed_prefix_is_accepted(string url)
    {
        WithAllowed("https://app.merchant360.com/integrations")
            .IsReturnUrlAllowed(url).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://evil.com/integrations")]                       // different host
    [InlineData("https://app.merchant360.com.evil.com/integrations")]   // suffix trick
    [InlineData("http://app.merchant360.com/integrations")]             // scheme downgrade
    [InlineData("https://app.merchant360.com:8443/integrations")]       // different port
    [InlineData("https://app.merchant360.com/elsewhere")]               // outside the prefix
    [InlineData("/integrations")]                                       // relative, so no host to check
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_else_is_refused(string? url)
    {
        WithAllowed("https://app.merchant360.com/integrations")
            .IsReturnUrlAllowed(url).Should().BeFalse();
    }
}

/// <summary>
/// The access-token cache. Keyed by connection and nothing else — the assertion that matters is
/// that two connections never share an entry.
/// </summary>
public class AmazonAccessTokenCacheTests
{
    private static Task<LwaTokenResult> Token(string value, int minutes = 60)
        => Task.FromResult(LwaTokenResult.Ok(value, "refresh", DateTime.UtcNow.AddMinutes(minutes)));

    [Fact]
    public async Task A_cached_token_is_returned_without_refreshing_again()
    {
        var cache = new AmazonAccessTokenCache();
        var id = Guid.NewGuid();
        var calls = 0;

        Task<LwaTokenResult> Refresh(CancellationToken _)
        {
            calls++;
            return Token("token-1");
        }

        (await cache.GetOrRefreshAsync(id, Refresh)).Should().Be("token-1");
        (await cache.GetOrRefreshAsync(id, Refresh)).Should().Be("token-1");

        calls.Should().Be(1);
    }

    [Fact]
    public async Task Two_connections_never_share_a_token()
    {
        var cache = new AmazonAccessTokenCache();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await cache.GetOrRefreshAsync(a, _ => Token("token-a"));
        var forB = await cache.GetOrRefreshAsync(b, _ => Token("token-b"));

        forB.Should().Be("token-b");
    }

    [Fact]
    public async Task A_token_inside_the_refresh_margin_is_treated_as_expired()
    {
        var cache = new AmazonAccessTokenCache();
        var id = Guid.NewGuid();

        // Four minutes left against a five-minute margin: a request that starts now could
        // otherwise still be in flight when the token dies.
        await cache.GetOrRefreshAsync(id, _ => Token("nearly-expired", minutes: 4));
        var second = await cache.GetOrRefreshAsync(id, _ => Token("fresh"));

        second.Should().Be("fresh");
    }

    [Fact]
    public async Task Invalidate_forces_the_next_call_to_refresh()
    {
        var cache = new AmazonAccessTokenCache();
        var id = Guid.NewGuid();

        await cache.GetOrRefreshAsync(id, _ => Token("old"));
        cache.Invalidate(id);

        (await cache.GetOrRefreshAsync(id, _ => Token("new"))).Should().Be("new");
    }

    [Fact]
    public async Task A_failed_refresh_caches_nothing()
    {
        var cache = new AmazonAccessTokenCache();
        var id = Guid.NewGuid();

        var failed = await cache.GetOrRefreshAsync(
            id, _ => Task.FromResult(LwaTokenResult.Failed("invalid_grant")));
        failed.Should().BeNull();

        // A cached failure would keep a connection broken until the process restarted.
        (await cache.GetOrRefreshAsync(id, _ => Token("recovered"))).Should().Be("recovered");
    }

    [Fact]
    public async Task Concurrent_callers_on_a_cold_cache_refresh_once()
    {
        var cache = new AmazonAccessTokenCache();
        var id = Guid.NewGuid();
        var calls = 0;

        async Task<LwaTokenResult> Refresh(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(50);
            return await Token("token");
        }

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => cache.GetOrRefreshAsync(id, Refresh)));

        // Amazon's rate limit is shared across the whole connection, so a thundering herd on a
        // cold cache spends budget that the order poller needs.
        calls.Should().Be(1);
    }
}
