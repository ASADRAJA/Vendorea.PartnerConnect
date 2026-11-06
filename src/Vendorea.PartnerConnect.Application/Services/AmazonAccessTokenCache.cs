using System.Collections.Concurrent;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class AmazonAccessTokenCache : IAmazonAccessTokenCache
{
    /// <summary>
    /// Refresh this long before expiry, so a token does not expire mid-request.
    /// </summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private sealed record Entry(string AccessToken, DateTime ExpiresAtUtc);

    // Keyed by connection id. Never by region, provider or seller id: those are shared across
    // tenants, and a hit on a shared key would return another tenant's token.
    private readonly ConcurrentDictionary<Guid, Entry> _cache = new();

    // One refresh at a time per connection. Without this, a burst of requests on a cold cache
    // each calls Amazon, which wastes the rate-limit budget the whole tenant shares.
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<string?> GetOrRefreshAsync(
        Guid connectionId, Func<CancellationToken, Task<LwaTokenResult>> refresh,
        CancellationToken cancellationToken = default)
    {
        if (TryGetFresh(connectionId, out var cached))
        {
            return cached;
        }

        var gate = _locks.GetOrAdd(connectionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have refreshed while we waited.
            if (TryGetFresh(connectionId, out cached))
            {
                return cached;
            }

            var result = await refresh(cancellationToken);
            if (!result.Success || string.IsNullOrEmpty(result.AccessToken))
            {
                return null;
            }

            _cache[connectionId] = new Entry(
                result.AccessToken,
                result.ExpiresAtUtc ?? DateTime.UtcNow.AddHours(1));

            return result.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Invalidate(Guid connectionId) => _cache.TryRemove(connectionId, out _);

    private bool TryGetFresh(Guid connectionId, out string? token)
    {
        token = null;
        if (!_cache.TryGetValue(connectionId, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAtUtc - RefreshMargin <= DateTime.UtcNow)
        {
            _cache.TryRemove(connectionId, out _);
            return false;
        }

        token = entry.AccessToken;
        return true;
    }
}
