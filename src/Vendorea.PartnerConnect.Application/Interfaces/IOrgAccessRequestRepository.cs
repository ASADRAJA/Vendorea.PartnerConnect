using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>Persistence for public "request to join" submissions (<see cref="OrgAccessRequest"/>).</summary>
public interface IOrgAccessRequestRepository
{
    Task<OrgAccessRequest> AddAsync(OrgAccessRequest request, CancellationToken cancellationToken = default);

    /// <summary>Finds a request by id (org loaded); null if not found.</summary>
    Task<OrgAccessRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The org's access requests, newest first, optionally filtered by status.</summary>
    Task<IReadOnlyList<OrgAccessRequest>> GetByOrganizationIdAsync(
        int organizationId, OrgAccessRequestStatus? status, CancellationToken cancellationToken = default);

    /// <summary>True if the org already has a pending request for this email (dedupe/guard).</summary>
    Task<bool> HasPendingAsync(int organizationId, string email, CancellationToken cancellationToken = default);

    Task UpdateAsync(OrgAccessRequest request, CancellationToken cancellationToken = default);
}
