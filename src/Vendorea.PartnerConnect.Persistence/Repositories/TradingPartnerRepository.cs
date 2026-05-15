using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class TradingPartnerRepository : ITradingPartnerRepository
{
    private readonly PartnerConnectDbContext _context;

    public TradingPartnerRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<TradingPartner?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.TradingPartners
            .Include(p => p.Capabilities)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<TradingPartner?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.TradingPartners
            .Include(p => p.Capabilities)
            .FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<TradingPartner>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TradingPartners
            .Include(p => p.Capabilities)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TradingPartner>> GetByStatusAsync(TradingPartnerStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.TradingPartners
            .Include(p => p.Capabilities)
            .Where(p => p.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<TradingPartner> AddAsync(TradingPartner partner, CancellationToken cancellationToken = default)
    {
        _context.TradingPartners.Add(partner);
        await _context.SaveChangesAsync(cancellationToken);
        return partner;
    }

    public async Task UpdateAsync(TradingPartner partner, CancellationToken cancellationToken = default)
    {
        _context.TradingPartners.Update(partner);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddCapabilityAsync(PartnerCapabilityConfiguration capability, CancellationToken cancellationToken = default)
    {
        _context.PartnerCapabilities.Add(capability);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
