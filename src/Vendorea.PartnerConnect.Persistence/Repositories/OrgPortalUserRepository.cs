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
}
