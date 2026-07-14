using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository for SupplierInventoryItem entities.
/// </summary>
public class SupplierInventoryItemRepository : ISupplierInventoryItemRepository
{
    private readonly PartnerConnectDbContext _context;

    public SupplierInventoryItemRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<SupplierInventoryItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.SupplierInventoryItems
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierInventoryItem>> GetBySnapshotIdAsync(
        int snapshotId,
        int skip = 0,
        int take = 1000,
        CancellationToken cancellationToken = default)
    {
        return await _context.SupplierInventoryItems
            .Where(i => i.SupplierInventorySnapshotId == snapshotId)
            .OrderBy(i => i.SupplierSku)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierInventoryItem?> GetCurrentBySkuAsync(
        int tradingPartnerId,
        string sku,
        CancellationToken cancellationToken = default)
    {
        // Get the latest applied snapshot for this partner
        var latestSnapshot = await _context.SupplierInventorySnapshots
            .Where(s => s.TradingPartnerId == tradingPartnerId && s.Status == InventorySnapshotStatus.Applied)
            .OrderByDescending(s => s.ProcessingCompletedAt ?? s.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSnapshot == null)
            return null;

        return await _context.SupplierInventoryItems
            .FirstOrDefaultAsync(i => i.SupplierInventorySnapshotId == latestSnapshot.Id &&
                                      i.SupplierSku == sku, cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierInventoryItem>> GetCurrentBySkusAsync(
        int tradingPartnerId,
        IEnumerable<string> skus,
        CancellationToken cancellationToken = default)
    {
        var skuList = skus.ToList();
        if (skuList.Count == 0)
            return Array.Empty<SupplierInventoryItem>();

        // Get the latest applied snapshot for this partner
        var latestSnapshot = await _context.SupplierInventorySnapshots
            .Where(s => s.TradingPartnerId == tradingPartnerId && s.Status == InventorySnapshotStatus.Applied)
            .OrderByDescending(s => s.ProcessingCompletedAt ?? s.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSnapshot == null)
            return Array.Empty<SupplierInventoryItem>();

        return await _context.SupplierInventoryItems
            .Where(i => i.SupplierInventorySnapshotId == latestSnapshot.Id &&
                        skuList.Contains(i.SupplierSku))
            .ToListAsync(cancellationToken);
    }

    public async Task<InventoryPage> SearchCurrentInventoryAsync(
        int tradingPartnerId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Latest applied snapshot for this partner is the current inventory (partner-level).
        var latestSnapshot = await _context.SupplierInventorySnapshots
            .Where(s => s.TradingPartnerId == tradingPartnerId && s.Status == InventorySnapshotStatus.Applied)
            .OrderByDescending(s => s.ProcessingCompletedAt ?? s.ReceivedAt)
            .Select(s => new { s.Id, s.InventoryDate, s.ProcessingCompletedAt, s.ReceivedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSnapshot == null)
            return new InventoryPage(Array.Empty<SupplierInventoryItem>(), 0, null);

        var asOf = latestSnapshot.ProcessingCompletedAt
                   ?? (latestSnapshot.InventoryDate == default ? latestSnapshot.ReceivedAt : latestSnapshot.InventoryDate);

        var query = _context.SupplierInventoryItems
            .Where(i => i.SupplierInventorySnapshotId == latestSnapshot.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i => i.SupplierSku.Contains(term) ||
                                     (i.Description != null && i.Description.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(i => i.SupplierSku)
            .Skip(skip)
            .Take(take)
            .Include(i => i.LocationQuantities)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new InventoryPage(items, total, asOf);
    }

    public async Task<SupplierInventoryItem> AddAsync(SupplierInventoryItem item, CancellationToken cancellationToken = default)
    {
        _context.SupplierInventoryItems.Add(item);
        await _context.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task AddRangeAsync(IEnumerable<SupplierInventoryItem> items, CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
            return;

        _context.SupplierInventoryItems.AddRange(itemList);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountBySnapshotIdAsync(int snapshotId, CancellationToken cancellationToken = default)
    {
        return await _context.SupplierInventoryItems
            .CountAsync(i => i.SupplierInventorySnapshotId == snapshotId, cancellationToken);
    }

    public async Task DeleteBySnapshotIdAsync(int snapshotId, CancellationToken cancellationToken = default)
    {
        await _context.SupplierInventoryItems
            .Where(i => i.SupplierInventorySnapshotId == snapshotId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
