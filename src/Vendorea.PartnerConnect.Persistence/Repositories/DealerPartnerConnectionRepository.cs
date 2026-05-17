using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class DealerPartnerConnectionRepository : IDealerPartnerConnectionRepository
{
    private readonly PartnerConnectDbContext _context;

    public DealerPartnerConnectionRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<DealerPartnerConnection?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.DealerPartnerConnections
            .Include(c => c.TradingPartner)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<DealerPartnerConnection?> GetByDealerAndPartnerAsync(int dealerId, int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        return await _context.DealerPartnerConnections
            .Include(c => c.TradingPartner)
            .FirstOrDefaultAsync(c => c.DealerId == dealerId && c.TradingPartnerId == tradingPartnerId, cancellationToken);
    }

    public async Task<IReadOnlyList<DealerPartnerConnection>> GetByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default)
    {
        return await _context.DealerPartnerConnections
            .Include(c => c.TradingPartner)
            .Where(c => c.DealerId == dealerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DealerPartnerConnection>> GetByPartnerIdAsync(int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        return await _context.DealerPartnerConnections
            .Include(c => c.TradingPartner)
            .Where(c => c.TradingPartnerId == tradingPartnerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DealerPartnerConnection>> GetActiveConnectionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DealerPartnerConnections
            .Include(c => c.TradingPartner)
            .Where(c => c.Status == ConnectionStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DealerPartnerConnection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DealerPartnerConnections
            .Include(c => c.TradingPartner)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DealerPartnerConnection> AddAsync(DealerPartnerConnection connection, CancellationToken cancellationToken = default)
    {
        _context.DealerPartnerConnections.Add(connection);
        await _context.SaveChangesAsync(cancellationToken);
        return connection;
    }

    public async Task UpdateAsync(DealerPartnerConnection connection, CancellationToken cancellationToken = default)
    {
        _context.DealerPartnerConnections.Update(connection);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
