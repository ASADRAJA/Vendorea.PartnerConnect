using System.Security.Cryptography;
using System.Text;

namespace Vendorea.PartnerConnect.Application.Security;

/// <summary>
/// Computes a stable, indexable hash of an API key so an organization can be resolved from an
/// inbound X-Api-Key without decrypting every org's stored key. This is a lookup hash, not a
/// password hash — the key itself is high-entropy and also stored encrypted for outbound use.
/// </summary>
public static class ApiKeyHasher
{
    /// <summary>Returns the lowercase hex SHA-256 of the key, or null for a null/blank key.</summary>
    public static string? Hash(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
