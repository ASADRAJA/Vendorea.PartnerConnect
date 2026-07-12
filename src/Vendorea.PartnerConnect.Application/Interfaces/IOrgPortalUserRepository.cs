using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Persistence for customer-portal (org) login users. Lookups load the user's explicit tenant scope
/// (<see cref="OrgPortalUser.Tenants"/>) so callers can resolve accessible tenants without a second
/// round trip.
/// </summary>
public interface IOrgPortalUserRepository
{
    /// <summary>Finds an active-or-inactive user by email across all orgs (email is treated as globally unique).</summary>
    Task<OrgPortalUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by org + email (case-insensitive), including its tenant scope.</summary>
    Task<OrgPortalUser?> GetByOrgAndEmailAsync(int organizationId, string email, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by id, including its tenant scope.</summary>
    Task<OrgPortalUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True if a user with this email already exists for the org.</summary>
    Task<bool> ExistsAsync(int organizationId, string email, CancellationToken cancellationToken = default);

    Task<OrgPortalUser> AddAsync(OrgPortalUser user, CancellationToken cancellationToken = default);

    Task UpdateAsync(OrgPortalUser user, CancellationToken cancellationToken = default);
}
