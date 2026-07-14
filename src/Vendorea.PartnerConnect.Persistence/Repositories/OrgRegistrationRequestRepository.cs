using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class OrgRegistrationRequestRepository : IOrgRegistrationRequestRepository
{
    private readonly PartnerConnectDbContext _context;

    public OrgRegistrationRequestRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<OrgRegistrationRequest> AddAsync(OrgRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        _context.OrgRegistrationRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<OrgRegistrationRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.OrgRegistrationRequests
            .Include(r => r.Organization)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<OrgRegistrationRequest>> GetAllAsync(OrgRegistrationStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _context.OrgRegistrationRequests.AsNoTracking().AsQueryable();
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(OrgRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        _context.OrgRegistrationRequests.Update(request);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
