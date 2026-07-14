using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class OrgPortalUserTokenRepository : IOrgPortalUserTokenRepository
{
    private readonly PartnerConnectDbContext _context;

    public OrgPortalUserTokenRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<OrgPortalUserToken> AddAsync(OrgPortalUserToken token, CancellationToken cancellationToken = default)
    {
        _context.OrgPortalUserTokens.Add(token);
        await _context.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<OrgPortalUserToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _context.OrgPortalUserTokens
            .Include(t => t.OrgPortalUser)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task MarkUsedAsync(OrgPortalUserToken token, CancellationToken cancellationToken = default)
    {
        _context.OrgPortalUserTokens.Update(token);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
