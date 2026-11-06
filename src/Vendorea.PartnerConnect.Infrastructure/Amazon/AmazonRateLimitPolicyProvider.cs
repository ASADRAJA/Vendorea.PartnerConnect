using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Infrastructure.Amazon;

/// <inheritdoc />
public class AmazonRateLimitPolicyProvider : IProviderRateLimitPolicyProvider
{
    /// <summary>
    /// Used when an operation has no configured policy.
    /// </summary>
    /// <remarks>
    /// Deliberately the most restrictive published Orders limit rather than "unlimited". An
    /// operation nobody remembered to configure should cost us a slow caller, not a throttled
    /// application — Amazon's 429s apply to the whole application, so one unmetered caller
    /// degrades every connection.
    /// </remarks>
    private static readonly RateLimitPolicy Conservative = new(0.0056, 1);

    private readonly AmazonSpApiOptions _options;
    private readonly ILogger<AmazonRateLimitPolicyProvider> _logger;

    public AmazonRateLimitPolicyProvider(
        IOptions<AmazonSpApiOptions> options,
        ILogger<AmazonRateLimitPolicyProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public RateLimitPolicy GetPolicy(string operation)
    {
        if (_options.RateLimits.TryGetValue(operation, out var setting) && setting.RatePerSecond > 0)
        {
            return new RateLimitPolicy(setting.RatePerSecond, Math.Max(1, setting.Burst));
        }

        _logger.LogWarning(
            "No rate-limit policy configured for {Operation}; falling back to the conservative default.",
            operation);

        return Conservative;
    }
}
