using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class InventoryFeedBatchRepository : IInventoryFeedBatchRepository
{
    private readonly PartnerConnectDbContext _context;

    public InventoryFeedBatchRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryFeedBatch?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.InventoryFeedBatches
            .Include(b => b.PartnerDocument)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryFeedBatch>> GetByDealerIdAsync(
        int dealerId,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryFeedBatches
            .Where(b => b.DealerId == dealerId)
            .OrderByDescending(b => b.ReceivedAt)
            .AsQueryable();

        if (skip.HasValue)
        {
            query = query.Skip(skip.Value);
        }

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryFeedBatch>> GetByTradingPartnerAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.InventoryFeedBatches
            .Where(b => b.TradingPartnerId == tradingPartnerId)
            .OrderByDescending(b => b.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryFeedBatch>> GetByStatusAsync(
        FeedBatchStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _context.InventoryFeedBatches
            .Where(b => b.Status == status)
            .OrderBy(b => b.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<InventoryFeedBatch?> GetLatestByTradingPartnerAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.InventoryFeedBatches
            .Where(b => b.TradingPartnerId == tradingPartnerId)
            .OrderByDescending(b => b.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryFeedBatch>> GetByDateRangeAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.InventoryFeedBatches
            .Where(b => b.DealerId == dealerId &&
                        b.ReceivedAt >= startDate &&
                        b.ReceivedAt <= endDate)
            .OrderByDescending(b => b.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<InventoryFeedBatch> AddAsync(InventoryFeedBatch batch, CancellationToken cancellationToken = default)
    {
        _context.InventoryFeedBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task UpdateAsync(InventoryFeedBatch batch, CancellationToken cancellationToken = default)
    {
        _context.InventoryFeedBatches.Update(batch);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<InventoryFeedStatistics> GetStatisticsAsync(
        int dealerId,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryFeedBatches
            .Where(b => b.DealerId == dealerId);

        if (since.HasValue)
        {
            query = query.Where(b => b.ReceivedAt >= since.Value);
        }

        var batches = await query.ToListAsync(cancellationToken);

        return new InventoryFeedStatistics(
            TotalBatches: batches.Count,
            TotalItemsProcessed: batches.Sum(b => b.ProcessedItems),
            TotalItemsMatched: batches.Sum(b => b.MatchedItems),
            TotalItemsUpdated: batches.Sum(b => b.UpdatedItems),
            TotalErrors: batches.Sum(b => b.ErrorItems),
            CompletedBatches: batches.Count(b => b.Status == FeedBatchStatus.Completed),
            FailedBatches: batches.Count(b => b.Status == FeedBatchStatus.Failed),
            LastSyncAt: batches
                .Where(b => b.Status == FeedBatchStatus.Completed)
                .OrderByDescending(b => b.ProcessingCompletedAt)
                .Select(b => b.ProcessingCompletedAt)
                .FirstOrDefault()
        );
    }
}
