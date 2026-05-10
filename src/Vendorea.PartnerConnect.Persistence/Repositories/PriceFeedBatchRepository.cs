using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class PriceFeedBatchRepository : IPriceFeedBatchRepository
{
    private readonly PartnerConnectDbContext _context;

    public PriceFeedBatchRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<PriceFeedBatch?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.PriceFeedBatches
            .Include(b => b.PartnerDocument)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PriceFeedBatch>> GetByDealerIdAsync(
        int dealerId,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PriceFeedBatches
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

    public async Task<IReadOnlyList<PriceFeedBatch>> GetByConnectionIdAsync(
        int connectionId,
        CancellationToken cancellationToken = default)
    {
        // Get connection to get dealerId and tradingPartnerId
        var connection = await _context.DealerPartnerConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);

        if (connection == null)
        {
            return Array.Empty<PriceFeedBatch>();
        }

        return await _context.PriceFeedBatches
            .Where(b => b.DealerId == connection.DealerId &&
                        b.TradingPartnerId == connection.TradingPartnerId)
            .OrderByDescending(b => b.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PriceFeedBatch>> GetByStatusAsync(
        FeedBatchStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _context.PriceFeedBatches
            .Where(b => b.Status == status)
            .OrderBy(b => b.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PriceFeedBatch?> GetLatestByConnectionIdAsync(
        int connectionId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _context.DealerPartnerConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);

        if (connection == null)
        {
            return null;
        }

        return await _context.PriceFeedBatches
            .Where(b => b.DealerId == connection.DealerId &&
                        b.TradingPartnerId == connection.TradingPartnerId)
            .OrderByDescending(b => b.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PriceFeedBatch>> GetByDateRangeAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.PriceFeedBatches
            .Where(b => b.DealerId == dealerId &&
                        b.ReceivedAt >= startDate &&
                        b.ReceivedAt <= endDate)
            .OrderByDescending(b => b.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PriceFeedBatch> AddAsync(PriceFeedBatch batch, CancellationToken cancellationToken = default)
    {
        _context.PriceFeedBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task UpdateAsync(PriceFeedBatch batch, CancellationToken cancellationToken = default)
    {
        _context.PriceFeedBatches.Update(batch);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PriceFeedStatistics> GetStatisticsAsync(
        int dealerId,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PriceFeedBatches
            .Where(b => b.DealerId == dealerId);

        if (since.HasValue)
        {
            query = query.Where(b => b.ReceivedAt >= since.Value);
        }

        var batches = await query.ToListAsync(cancellationToken);

        return new PriceFeedStatistics(
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
