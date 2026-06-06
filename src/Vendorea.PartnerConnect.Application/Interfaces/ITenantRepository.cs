using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository interface for tenant operations.
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByIdWithOrganizationAsync(int id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByCodeAsync(int organizationId, string code, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<Tenant?> GetDefaultTenantAsync(int organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> GetByOrganizationIdAsync(int organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> GetByStatusAsync(int organizationId, TenantStatus status, CancellationToken cancellationToken = default);
    Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> BelongsToOrganizationAsync(int tenantId, int organizationId, CancellationToken cancellationToken = default);
}
