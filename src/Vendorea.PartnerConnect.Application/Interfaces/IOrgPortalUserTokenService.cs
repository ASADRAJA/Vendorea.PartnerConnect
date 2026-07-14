using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Issues and validates secure, single-use, short-lived tokens for <see cref="OrgPortalUser"/> account
/// activation and (later) password reset. The raw token is returned only once — at issue time, for the
/// emailed link — and only its SHA-256 hash is persisted.
/// </summary>
public interface IOrgPortalUserTokenService
{
    /// <summary>
    /// Generates a cryptographically-random token, stores its hash for the given user/purpose/expiry,
    /// and returns the RAW token (to embed in the link). The raw token is never persisted.
    /// </summary>
    Task<string> IssueAsync(Guid orgPortalUserId, OrgPortalUserTokenPurpose purpose, TimeSpan lifetime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a raw token for the given purpose: hashes it, looks it up, and returns the token
    /// (with its owning user loaded) only when it exists, matches the purpose, is unused, and is
    /// unexpired. Returns null otherwise.
    /// </summary>
    Task<OrgPortalUserToken?> ValidateAsync(string rawToken, OrgPortalUserTokenPurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>Marks a validated token as used so it can't be redeemed again.</summary>
    Task ConsumeAsync(OrgPortalUserToken token, CancellationToken cancellationToken = default);
}
