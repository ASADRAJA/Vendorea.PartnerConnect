using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository for SupplierInventorySnapshot entities.
/// </summary>
public class SupplierInventorySnapshotRepository : ISupplierInventorySnapshotRepository
{
    private readonly PartnerConnectDbContext _context;

    public SupplierInventorySnapshotRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<SupplierInventorySnapshot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.SupplierInventorySnapshots
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<SupplierInventorySnapshot?> GetByIdWithItemsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.SupplierInventorySnapshots
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<SupplierInventorySnapshot?> GetLatestAppliedAsync(int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        return await _context.SupplierInventorySnapshots
            .Where(s => s.TradingPartnerId == tradingPartnerId && s.Status == InventorySnapshotStatus.Applied)
            .OrderByDescending(s => s.ProcessingCompletedAt ?? s.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierInventorySnapshot>> GetByTradingPartnerAsync(
        int tradingPartnerId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _context.SupplierInventorySnapshots
            .Where(s => s.TradingPartnerId == tradingPartnerId)
            .OrderByDescending(s => s.ReceivedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierInventorySnapshot>> GetByStatusAsync(
        InventorySnapshotStatus status,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        return await _context.SupplierInventorySnapshots
            .Where(s => s.Status == status)
            .OrderBy(s => s.ReceivedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierInventorySnapshot>> GetByStatusAsync(
        IEnumerable<InventorySnapshotStatus> statuses,
        int? tradingPartnerId = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var statusList = statuses.ToList();

        var query = _context.SupplierInventorySnapshots
            .Where(s => statusList.Contains(s.Status));

        if (tradingPartnerId.HasValue)
        {
            query = query.Where(s => s.TradingPartnerId == tradingPartnerId.Value);
        }

        return await query
            .OrderBy(s => s.ReceivedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierInventorySnapshot> AddAsync(SupplierInventorySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        snapshot.ReceivedAt = DateTime.UtcNow;
        _context.SupplierInventorySnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    public async Task UpdateAsync(SupplierInventorySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _context.SupplierInventorySnapshots.Update(snapshot);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SupersedeAllExceptAsync(int tradingPartnerId, int excludeSnapshotId, CancellationToken cancellationToken = default)
    {
        var snapshotsToSupersede = await _context.SupplierInventorySnapshots
            .Where(s => s.TradingPartnerId == tradingPartnerId &&
                        s.Id != excludeSnapshotId &&
                        s.Status == InventorySnapshotStatus.Applied)
            .ToListAsync(cancellationToken);

        foreach (var snapshot in snapshotsToSupersede)
        {
            snapshot.Status = InventorySnapshotStatus.Superseded;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteOldSnapshotsAsync(int tradingPartnerId, int retain, CancellationToken cancellationToken = default)
    {
        retain = Math.Max(1, retain);

        var keepIds = await _context.SupplierInventorySnapshots
            .Where(s => s.TradingPartnerId == tradingPartnerId)
            .OrderByDescending(s => s.ReceivedAt)
            .Select(s => s.Id)
            .Take(retain)
            .ToListAsync(cancellationToken);

        // Direct DELETE; the DB cascades to SupplierInventoryItems and their LocationQuantities.
        return await _context.SupplierInventorySnapshots
            .Where(s => s.TradingPartnerId == tradingPartnerId && !keepIds.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
