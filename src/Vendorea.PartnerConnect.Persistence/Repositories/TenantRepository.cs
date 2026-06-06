using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly PartnerConnectDbContext _context;

    public TenantRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<Tenant?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Tenant?> GetByIdWithOrganizationAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .Include(t => t.Organization)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Tenant?> GetByCodeAsync(int organizationId, string code, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.OrganizationId == organizationId && t.Code == code, cancellationToken);
    }

    public async Task<Tenant?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .Include(t => t.Organization)
            .FirstOrDefaultAsync(t => t.ExternalId == externalId, cancellationToken);
    }

    public async Task<Tenant?> GetDefaultTenantAsync(int organizationId, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.OrganizationId == organizationId && t.IsDefault, cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .Include(t => t.Organization)
            .OrderBy(t => t.Organization!.Name)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetByOrganizationIdAsync(int organizationId, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .Where(t => t.OrganizationId == organizationId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetByStatusAsync(int organizationId, TenantStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .Where(t => t.OrganizationId == organizationId && t.Status == status)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tenant> AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        tenant.UpdatedAt = DateTime.UtcNow;
        _context.Tenants.Update(tenant);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .AnyAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<bool> BelongsToOrganizationAsync(int tenantId, int organizationId, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .AnyAsync(t => t.Id == tenantId && t.OrganizationId == organizationId, cancellationToken);
    }
}
