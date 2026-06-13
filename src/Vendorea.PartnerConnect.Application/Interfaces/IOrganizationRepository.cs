using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository interface for organization operations.
/// </summary>
public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Organization?> GetByIdWithPartnersAsync(int id, CancellationToken cancellationToken = default);
    Task<Organization?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the organization's selected trading partners with the given set.
    /// </summary>
    Task ReplacePartnersAsync(int organizationId, IReadOnlyCollection<int> tradingPartnerIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the next sequential organization code (e.g., "ORG-00001").
    /// </summary>
    Task<string> GenerateNextCodeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetByStatusAsync(OrganizationStatus status, CancellationToken cancellationToken = default);
    Task<Organization> AddAsync(Organization organization, CancellationToken cancellationToken = default);
    Task UpdateAsync(Organization organization, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);
}
