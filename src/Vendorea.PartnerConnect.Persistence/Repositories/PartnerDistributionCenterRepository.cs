using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class PartnerDistributionCenterRepository : IPartnerDistributionCenterRepository
{
    private readonly PartnerConnectDbContext _context;

    public PartnerDistributionCenterRepository(PartnerConnectDbContext context) => _context = context;

    public async Task<IReadOnlyList<PartnerDistributionCenter>> GetByPartnerAsync(int tradingPartnerId, CancellationToken cancellationToken = default) =>
        await _context.PartnerDistributionCenters
            .AsNoTracking()
            .Where(dc => dc.TradingPartnerId == tradingPartnerId)
            .ToListAsync(cancellationToken);

    public async Task<PartnerDistributionCenter?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _context.PartnerDistributionCenters
            .FirstOrDefaultAsync(dc => dc.Id == id, cancellationToken);

    public async Task<bool> DcNumberExistsAsync(int tradingPartnerId, int dcNumber, int? excludeId = null, CancellationToken cancellationToken = default) =>
        await _context.PartnerDistributionCenters
            .AnyAsync(dc => dc.TradingPartnerId == tradingPartnerId
                            && dc.DcNumber == dcNumber
                            && (excludeId == null || dc.Id != excludeId.Value),
                cancellationToken);

    public async Task<PartnerDistributionCenter> AddAsync(PartnerDistributionCenter dc, CancellationToken cancellationToken = default)
    {
        _context.PartnerDistributionCenters.Add(dc);
        await _context.SaveChangesAsync(cancellationToken);
        return dc;
    }

    public async Task UpdateAsync(PartnerDistributionCenter dc, CancellationToken cancellationToken = default)
    {
        _context.PartnerDistributionCenters.Update(dc);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PartnerDistributionCenter dc, CancellationToken cancellationToken = default)
    {
        _context.PartnerDistributionCenters.Remove(dc);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
