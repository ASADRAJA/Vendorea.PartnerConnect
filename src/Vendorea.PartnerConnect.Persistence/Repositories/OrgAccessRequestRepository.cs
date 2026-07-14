using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class OrgAccessRequestRepository : IOrgAccessRequestRepository
{
    private readonly PartnerConnectDbContext _context;

    public OrgAccessRequestRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<OrgAccessRequest> AddAsync(OrgAccessRequest request, CancellationToken cancellationToken = default)
    {
        _context.OrgAccessRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<OrgAccessRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.OrgAccessRequests
            .Include(r => r.Organization)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<OrgAccessRequest>> GetByOrganizationIdAsync(
        int organizationId, OrgAccessRequestStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _context.OrgAccessRequests
            .AsNoTracking()
            .Where(r => r.OrganizationId == organizationId);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasPendingAsync(int organizationId, string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim();
        return await _context.OrgAccessRequests.AnyAsync(
            r => r.OrganizationId == organizationId
                 && r.Status == OrgAccessRequestStatus.Pending
                 && r.Email == normalized,
            cancellationToken);
    }

    public async Task UpdateAsync(OrgAccessRequest request, CancellationToken cancellationToken = default)
    {
        _context.OrgAccessRequests.Update(request);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
