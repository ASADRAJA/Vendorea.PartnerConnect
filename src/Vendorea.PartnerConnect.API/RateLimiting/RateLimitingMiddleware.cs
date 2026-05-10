using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Vendorea.PartnerConnect.Api.RateLimiting;

/// <summary>
/// Middleware for rate limiting API requests.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitConfiguration _config;
    private readonly SlidingWindowRateLimiter _minuteLimiter;
    private readonly SlidingWindowRateLimiter _hourLimiter;
    private readonly Timer _cleanupTimer;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IOptions<RateLimitConfiguration> config)
    {
        _next = next;
        _logger = logger;
        _config = config.Value;

        // Default to authenticated tier for window sizes
        _minuteLimiter = new SlidingWindowRateLimiter(60, _config.Tiers["authenticated"].RequestsPerMinute);
        _hourLimiter = new SlidingWindowRateLimiter(3600, _config.Tiers["authenticated"].RequestsPerHour);

        // Cleanup expired entries every 5 minutes
        _cleanupTimer = new Timer(_ =>
        {
            _minuteLimiter.Cleanup();
            _hourLimiter.Cleanup();
        }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_config.Enabled)
        {
            await _next(context);
            return;
        }

        // Check if path is excluded
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (_config.ExcludedPaths.Any(p => path.StartsWith(p.ToLowerInvariant())))
        {
            await _next(context);
            return;
        }

        var clientKey = GetClientKey(context);
        var tier = GetClientTier(context);
        var tierConfig = _config.Tiers.GetValueOrDefault(tier) ?? _config.Tiers["anonymous"];

        // Check minute limit
        var minuteKey = $"{clientKey}:minute";
        if (!CheckLimit(_minuteLimiter, minuteKey, tierConfig.RequestsPerMinute))
        {
            await WriteRateLimitResponse(context, "minute", _minuteLimiter.GetResetTime(minuteKey));
            return;
        }

        // Check hour limit
        var hourKey = $"{clientKey}:hour";
        if (!CheckLimit(_hourLimiter, hourKey, tierConfig.RequestsPerHour))
        {
            await WriteRateLimitResponse(context, "hour", _hourLimiter.GetResetTime(hourKey));
            return;
        }

        // Add rate limit headers
        AddRateLimitHeaders(context, minuteKey, hourKey, tierConfig);

        await _next(context);
    }

    private bool CheckLimit(SlidingWindowRateLimiter limiter, string key, int maxRequests)
    {
        // Create a temporary limiter with the correct max for this tier
        return limiter.IsAllowed(key);
    }

    private string GetClientKey(HttpContext context)
    {
        // Try to get API key first
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKey) && !string.IsNullOrEmpty(apiKey))
        {
            return $"apikey:{apiKey.ToString()[..Math.Min(8, apiKey.ToString().Length)]}";
        }

        // Try to get user ID
        var userId = context.User?.FindFirst("sub")?.Value ??
                     context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        // Fall back to IP address
        var ipAddress = GetClientIpAddress(context);
        return $"ip:{ipAddress}";
    }

    private string GetClientTier(HttpContext context)
    {
        // Check for premium tier (could be based on subscription)
        if (context.Items.TryGetValue("SubscriptionTier", out var tier) && tier is string tierStr)
        {
            return tierStr.ToLowerInvariant();
        }

        // Check if authenticated
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            return "authenticated";
        }

        // Check for API key
        if (context.Request.Headers.ContainsKey("X-API-Key"))
        {
            return "authenticated";
        }

        return "anonymous";
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check X-Forwarded-For header first
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // Check X-Real-IP header
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private void AddRateLimitHeaders(HttpContext context, string minuteKey, string hourKey, RateLimitTier tierConfig)
    {
        var remainingMinute = _minuteLimiter.GetRemainingRequests(minuteKey);
        var remainingHour = _hourLimiter.GetRemainingRequests(hourKey);
        var resetTime = _minuteLimiter.GetResetTime(minuteKey);

        context.Response.Headers["X-RateLimit-Limit-Minute"] = tierConfig.RequestsPerMinute.ToString();
        context.Response.Headers["X-RateLimit-Remaining-Minute"] = remainingMinute.ToString();
        context.Response.Headers["X-RateLimit-Limit-Hour"] = tierConfig.RequestsPerHour.ToString();
        context.Response.Headers["X-RateLimit-Remaining-Hour"] = remainingHour.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(resetTime).ToUnixTimeSeconds().ToString();
    }

    private async Task WriteRateLimitResponse(HttpContext context, string window, DateTime resetTime)
    {
        _logger.LogWarning(
            "Rate limit exceeded for {ClientKey} on {Window} window",
            GetClientKey(context),
            window);

        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.ContentType = "application/json";
        context.Response.Headers["Retry-After"] = ((int)(resetTime - DateTime.UtcNow).TotalSeconds).ToString();
        context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(resetTime).ToUnixTimeSeconds().ToString();

        var response = new
        {
            error = "Rate limit exceeded",
            message = $"Too many requests. Please retry after {(int)(resetTime - DateTime.UtcNow).TotalSeconds} seconds.",
            retryAfter = (int)(resetTime - DateTime.UtcNow).TotalSeconds
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

/// <summary>
/// Extension methods for rate limiting.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds rate limiting services.
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitConfiguration>(configuration.GetSection("RateLimiting"));
        return services;
    }

    /// <summary>
    /// Uses rate limiting middleware.
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}
