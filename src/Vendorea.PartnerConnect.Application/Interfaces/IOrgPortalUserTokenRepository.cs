using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Persistence for <see cref="OrgPortalUserToken"/> — the single-use activation / password-reset
/// tokens. Lookups are by the token's SHA-256 hash (the raw token is never stored) and load the
/// owning user so the caller can act on it without a second round trip.
/// </summary>
public interface IOrgPortalUserTokenRepository
{
    Task<OrgPortalUserToken> AddAsync(OrgPortalUserToken token, CancellationToken cancellationToken = default);

    /// <summary>Finds a token by its SHA-256 hash, including the owning <see cref="OrgPortalUser"/>.</summary>
    Task<OrgPortalUserToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Stamps the token as used (single-use enforcement).</summary>
    Task MarkUsedAsync(OrgPortalUserToken token, CancellationToken cancellationToken = default);
}
