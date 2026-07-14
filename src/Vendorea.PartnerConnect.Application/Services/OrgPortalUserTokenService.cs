using System.Security.Cryptography;
using System.Text;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Default <see cref="IOrgPortalUserTokenService"/>: generates 256-bit random tokens (base64url) and
/// persists only their SHA-256 hash. Validation re-hashes the incoming raw token and checks purpose,
/// single-use, and expiry. Mirrors the SHA-256-lookup approach used for API keys.
/// </summary>
public class OrgPortalUserTokenService : IOrgPortalUserTokenService
{
    private const int TokenBytes = 32; // 256 bits of entropy

    private readonly IOrgPortalUserTokenRepository _tokens;

    public OrgPortalUserTokenService(IOrgPortalUserTokenRepository tokens)
    {
        _tokens = tokens;
    }

    public async Task<string> IssueAsync(Guid orgPortalUserId, OrgPortalUserTokenPurpose purpose, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        var rawToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

        var token = new OrgPortalUserToken
        {
            OrgPortalUserId = orgPortalUserId,
            TokenHash = HashToken(rawToken),
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            CreatedAt = DateTime.UtcNow
        };

        await _tokens.AddAsync(token, cancellationToken);
        return rawToken;
    }

    public async Task<OrgPortalUserToken?> ValidateAsync(string rawToken, OrgPortalUserTokenPurpose purpose, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var token = await _tokens.GetByHashAsync(HashToken(rawToken), cancellationToken);
        if (token is null)
            return null;

        if (token.Purpose != purpose || token.UsedAt.HasValue || token.ExpiresAt <= DateTime.UtcNow)
            return null;

        return token;
    }

    public Task ConsumeAsync(OrgPortalUserToken token, CancellationToken cancellationToken = default)
    {
        token.UsedAt = DateTime.UtcNow;
        return _tokens.MarkUsedAsync(token, cancellationToken);
    }

    /// <summary>Lowercase-hex SHA-256 of the raw token — the only form ever persisted.</summary>
    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>URL-safe, unpadded base64 so the token drops straight into a query string.</summary>
    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
