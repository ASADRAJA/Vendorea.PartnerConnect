using System.Collections.Concurrent;

namespace Vendorea.PartnerConnect.Api.RateLimiting;

/// <summary>
/// Sliding window rate limiter implementation.
/// </summary>
public class SlidingWindowRateLimiter
{
    private readonly ConcurrentDictionary<string, SlidingWindow> _windows = new();
    private readonly int _windowSizeInSeconds;
    private readonly int _maxRequests;

    public SlidingWindowRateLimiter(int windowSizeInSeconds, int maxRequests)
    {
        _windowSizeInSeconds = windowSizeInSeconds;
        _maxRequests = maxRequests;
    }

    /// <summary>
    /// Checks if a request is allowed for the given key.
    /// </summary>
    public bool IsAllowed(string key)
    {
        var window = _windows.GetOrAdd(key, _ => new SlidingWindow(_windowSizeInSeconds));
        return window.TryAdd(_maxRequests);
    }

    /// <summary>
    /// Gets the remaining requests for a key.
    /// </summary>
    public int GetRemainingRequests(string key)
    {
        if (_windows.TryGetValue(key, out var window))
        {
            return Math.Max(0, _maxRequests - window.GetCurrentCount());
        }
        return _maxRequests;
    }

    /// <summary>
    /// Gets when the rate limit resets for a key.
    /// </summary>
    public DateTime GetResetTime(string key)
    {
        if (_windows.TryGetValue(key, out var window))
        {
            return window.GetResetTime();
        }
        return DateTime.UtcNow.AddSeconds(_windowSizeInSeconds);
    }

    /// <summary>
    /// Cleans up expired windows.
    /// </summary>
    public void Cleanup()
    {
        var expiredKeys = _windows
            .Where(kvp => kvp.Value.IsExpired())
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _windows.TryRemove(key, out _);
        }
    }

    private class SlidingWindow
    {
        private readonly int _windowSizeInSeconds;
        private readonly ConcurrentQueue<DateTime> _timestamps = new();
        private readonly object _lock = new();

        public SlidingWindow(int windowSizeInSeconds)
        {
            _windowSizeInSeconds = windowSizeInSeconds;
        }

        public bool TryAdd(int maxRequests)
        {
            lock (_lock)
            {
                CleanOldEntries();

                if (_timestamps.Count >= maxRequests)
                {
                    return false;
                }

                _timestamps.Enqueue(DateTime.UtcNow);
                return true;
            }
        }

        public int GetCurrentCount()
        {
            lock (_lock)
            {
                CleanOldEntries();
                return _timestamps.Count;
            }
        }

        public DateTime GetResetTime()
        {
            lock (_lock)
            {
                if (_timestamps.TryPeek(out var oldest))
                {
                    return oldest.AddSeconds(_windowSizeInSeconds);
                }
                return DateTime.UtcNow.AddSeconds(_windowSizeInSeconds);
            }
        }

        public bool IsExpired()
        {
            lock (_lock)
            {
                CleanOldEntries();
                return _timestamps.IsEmpty;
            }
        }

        private void CleanOldEntries()
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-_windowSizeInSeconds);
            while (_timestamps.TryPeek(out var timestamp) && timestamp < cutoff)
            {
                _timestamps.TryDequeue(out _);
            }
        }
    }
}

/// <summary>
/// Rate limit configuration for different tiers.
/// </summary>
public class RateLimitConfiguration
{
    /// <summary>
    /// Default rate limits per tier.
    /// </summary>
    public Dictionary<string, RateLimitTier> Tiers { get; set; } = new()
    {
        ["anonymous"] = new RateLimitTier { RequestsPerMinute = 30, RequestsPerHour = 500 },
        ["authenticated"] = new RateLimitTier { RequestsPerMinute = 100, RequestsPerHour = 2000 },
        ["premium"] = new RateLimitTier { RequestsPerMinute = 500, RequestsPerHour = 10000 }
    };

    /// <summary>
    /// Whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Endpoints to exclude from rate limiting.
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = new()
    {
        "/health",
        "/swagger"
    };
}

/// <summary>
/// Rate limit tier configuration.
/// </summary>
public class RateLimitTier
{
    public int RequestsPerMinute { get; set; }
    public int RequestsPerHour { get; set; }
}
