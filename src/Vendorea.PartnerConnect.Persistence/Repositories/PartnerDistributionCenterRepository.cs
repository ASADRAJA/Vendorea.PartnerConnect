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
}
