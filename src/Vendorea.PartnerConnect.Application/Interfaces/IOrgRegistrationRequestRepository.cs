using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Persistence for self-service <see cref="OrgRegistrationRequest"/> rows (the public-registration
/// review queue).
/// </summary>
public interface IOrgRegistrationRequestRepository
{
    Task<OrgRegistrationRequest> AddAsync(OrgRegistrationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Finds a request by id, including its pending organization.</summary>
    Task<OrgRegistrationRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lists requests, newest first; optionally filtered to a single status.</summary>
    Task<IReadOnlyList<OrgRegistrationRequest>> GetAllAsync(OrgRegistrationStatus? status, CancellationToken cancellationToken = default);

    Task UpdateAsync(OrgRegistrationRequest request, CancellationToken cancellationToken = default);
}
