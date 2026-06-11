using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vendorea.PartnerConnect.Merchant360Connector;

/// <summary>
/// Logs a non-fatal startup warning when the Merchant360 inbound API key is missing or still a
/// placeholder. PC -> M360 callbacks send <c>X-Api-Key</c> (== M360's PartnerConnect:InboundApiKey);
/// if it is unset/placeholder, M360 rejects callbacks with 401 at runtime. This is a heads-up only
/// and never blocks startup.
/// </summary>
internal sealed class Merchant360ConfigWarningService : IHostedService
{
    private static readonly string[] PlaceholderMarkers =
    {
        "not-for-production", "placeholder", "changeme", "replace", "example", "dev-secret", "12345"
    };

    private readonly Merchant360Options _options;
    private readonly ILogger<Merchant360ConfigWarningService> _logger;

    public Merchant360ConfigWarningService(
        IOptions<Merchant360Options> options,
        ILogger<Merchant360ConfigWarningService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var key = _options.ApiKey;

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning(
                "Merchant360:ApiKey is not configured. PC -> Merchant360 callbacks send the X-Api-Key " +
                "header, and Merchant360 will reject them with 401 until the shared key is set " +
                "(must equal Merchant360's PartnerConnect:InboundApiKey).");
        }
        else if (LooksLikePlaceholder(key))
        {
            _logger.LogWarning(
                "Merchant360:ApiKey appears to be a placeholder/dev value ({MaskedKey}). Set the real " +
                "shared key (== Merchant360's PartnerConnect:InboundApiKey) before live PC -> Merchant360 " +
                "callbacks, otherwise they will be rejected with 401.",
                Mask(key));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool LooksLikePlaceholder(string key)
    {
        var lower = key.ToLowerInvariant();
        return PlaceholderMarkers.Any(marker => lower.Contains(marker));
    }

    private static string Mask(string key) =>
        key.Length <= 4 ? "****" : $"{key[..2]}{new string('*', key.Length - 4)}{key[^2..]}";
}
