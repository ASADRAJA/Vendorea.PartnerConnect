using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Vendorea.PartnerConnect.Api.RateLimiting;

/// <summary>
/// Named rate-limit policies for the public/anonymous auth surface. These are opt-in — only endpoints
/// decorated with <c>[EnableRateLimiting(...)]</c> are throttled, so the authenticated app/API surface
/// is never rate limited. Both policies are fixed-window limiters partitioned by client IP.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Tight limit for the most abuse-prone anonymous endpoints: org registration, access requests,
    /// and forgot-password (spam orgs / enumeration / mail-bombing deterrent).
    /// </summary>
    public const string PublicAuth = "public-auth";

    /// <summary>Slightly higher limit for the login endpoint (legitimate retries are common).</summary>
    public const string PublicLogin = "public-login";

    /// <summary>Requests allowed per window for <see cref="PublicAuth"/>.</summary>
    private const int PublicAuthPermitLimit = 5;

    /// <summary>Requests allowed per window for <see cref="PublicLogin"/>.</summary>
    private const int PublicLoginPermitLimit = 10;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>Registers the public-auth rate-limit policies and a 429 rejection status.</summary>
    public static IServiceCollection AddPublicRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PublicAuth, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = PublicAuthPermitLimit,
                        Window = Window,
                        QueueLimit = 0
                    }));

            options.AddPolicy(PublicLogin, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = PublicLoginPermitLimit,
                        Window = Window,
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    /// <summary>Partition by client IP (honoring X-Forwarded-For when behind a proxy).</summary>
    private static string ClientKey(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
