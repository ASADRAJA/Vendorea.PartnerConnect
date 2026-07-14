using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class OrgPortalUserRepository : IOrgPortalUserRepository
{
    private readonly PartnerConnectDbContext _context;

    public OrgPortalUserRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<OrgPortalUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim();
        return await _context.OrgPortalUsers
            .Include(u => u.Tenants)
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task<OrgPortalUser?> GetByOrgAndEmailAsync(int organizationId, string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim();
        return await _context.OrgPortalUsers
            .Include(u => u.Tenants)
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.OrganizationId == organizationId && u.Email == normalized, cancellationToken);
    }

    public async Task<OrgPortalUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.OrgPortalUsers
            .Include(u => u.Tenants)
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<OrgPortalUser>> GetByOrganizationIdAsync(int organizationId, CancellationToken cancellationToken = default)
    {
        return await _context.OrgPortalUsers
            .AsNoTracking()
            .Include(u => u.Tenants)
            .Where(u => u.OrganizationId == organizationId)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(int organizationId, string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim();
        return await _context.OrgPortalUsers
            .AnyAsync(u => u.OrganizationId == organizationId && u.Email == normalized, cancellationToken);
    }

    public async Task<OrgPortalUser> AddAsync(OrgPortalUser user, CancellationToken cancellationToken = default)
    {
        _context.OrgPortalUsers.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpdateAsync(OrgPortalUser user, CancellationToken cancellationToken = default)
    {
        user.UpdatedAt = DateTime.UtcNow;
        _context.OrgPortalUsers.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWithTenantScopeAsync(
        OrgPortalUser user, bool allTenants, IReadOnlyCollection<int>? tenantIds, CancellationToken cancellationToken = default)
    {
        // `user` is tracked (loaded via GetByIdAsync in the same scoped context). Reconcile the tenant
        // rows through the change tracker (add/remove) — don't call Update(), which would mis-mark newly
        // added child rows (their composite key is set) as Modified and fail with a phantom UPDATE.
        var desired = allTenants || tenantIds is null
            ? new HashSet<int>()
            : tenantIds.ToHashSet();

        foreach (var existing in user.Tenants.Where(t => !desired.Contains(t.TenantId)).ToList())
            user.Tenants.Remove(existing);

        var present = user.Tenants.Select(t => t.TenantId).ToHashSet();
        foreach (var tenantId in desired.Where(id => !present.Contains(id)))
            user.Tenants.Add(new OrgPortalUserTenant { OrgPortalUserId = user.Id, TenantId = tenantId });

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
