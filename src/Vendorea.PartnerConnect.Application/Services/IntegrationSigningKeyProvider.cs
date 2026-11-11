using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class IntegrationSigningKeyProvider : IIntegrationSigningKeyProvider
{
    private readonly (string KeyId, string Secret)? _primary;
    private readonly Dictionary<string, string> _accepted;

    public IntegrationSigningKeyProvider(
        IOptions<IntegrationDeliveryOptions> options,
        ILogger<IntegrationSigningKeyProvider> logger)
    {
        var keys = options.Value.SigningKeys
            .Where(k => !string.IsNullOrWhiteSpace(k.KeyId) && !string.IsNullOrWhiteSpace(k.Secret))
            .ToList();

        _accepted = keys.ToDictionary(k => k.KeyId, k => k.Secret, StringComparer.Ordinal);

        var primaries = keys.Where(k => k.IsPrimary).ToList();

        if (primaries.Count > 1)
        {
            // Ambiguous rather than harmless: whichever key happened to be listed first would
            // sign, and a consumer debugging a signature failure would be told the wrong key id.
            logger.LogError(
                "More than one signing key is marked primary ({KeyIds}). Deliveries are not signed until exactly one is.",
                string.Join(", ", primaries.Select(p => p.KeyId)));
            _primary = null;
            return;
        }

        if (primaries.Count == 0)
        {
            if (keys.Count > 0)
            {
                logger.LogError(
                    "Signing keys are configured but none is primary. Deliveries are not signed until one is.");
            }
            _primary = null;
            return;
        }

        _primary = (primaries[0].KeyId, primaries[0].Secret);
    }

    public (string KeyId, string Secret)? Primary => _primary;

    public IReadOnlyDictionary<string, string> AcceptedKeys => _accepted;

    public string? Resolve(string? keyId)
        => keyId is not null && _accepted.TryGetValue(keyId, out var secret) ? secret : null;
}
