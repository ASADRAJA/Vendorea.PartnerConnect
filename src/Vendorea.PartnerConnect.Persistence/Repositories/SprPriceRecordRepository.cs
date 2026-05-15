using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class SprPriceRecordRepository : ISprPriceRecordRepository
{
    private readonly PartnerConnectDbContext _context;

    public SprPriceRecordRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<SprPriceRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> GetByUploadIdAsync(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == uploadId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> GetByStockNumberAsync(
        int dealerId,
        string stockNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .Where(r => r.DealerId == dealerId && r.StockNumber == stockNumber)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<SprPriceRecord?> GetLatestByStockNumberAsync(
        int dealerId,
        string stockNumber,
        CancellationToken cancellationToken = default)
    {
        // Get the latest completed upload for this dealer
        var latestUpload = await _context.PriceFeedUploads
            .Where(u => u.DealerId == dealerId &&
                        u.TradingPartner != null &&
                        u.TradingPartner.Code == "SPR" &&
                        u.Status == PriceFeedUploadStatus.Completed)
            .OrderByDescending(u => u.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestUpload == null)
            return null;

        return await _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == latestUpload.Id && r.StockNumber == stockNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> GetByUpcAsync(
        int dealerId,
        string upc,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .Where(r => r.DealerId == dealerId && r.Upc == upc)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> GetByCategoryAsync(
        int dealerId,
        string categoryCode,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprPriceRecords
            .Where(r => r.DealerId == dealerId && r.CategoryCode == categoryCode)
            .OrderBy(r => r.StockNumber);

        if (offset.HasValue)
        {
            query = (IOrderedQueryable<SprPriceRecord>)query.Skip(offset.Value);
        }

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<SprPriceRecord>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> GetCurrentPricesAsync(
        int dealerId,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        // Get the latest completed upload for this dealer
        var latestUpload = await _context.PriceFeedUploads
            .Where(u => u.DealerId == dealerId &&
                        u.TradingPartner != null &&
                        u.TradingPartner.Code == "SPR" &&
                        u.Status == PriceFeedUploadStatus.Completed)
            .OrderByDescending(u => u.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestUpload == null)
            return Array.Empty<SprPriceRecord>();

        var query = _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == latestUpload.Id)
            .OrderBy(r => r.StockNumber);

        if (offset.HasValue)
        {
            query = (IOrderedQueryable<SprPriceRecord>)query.Skip(offset.Value);
        }

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<SprPriceRecord>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task BulkInsertAsync(
        IEnumerable<SprPriceRecord> records,
        CancellationToken cancellationToken = default)
    {
        // For large datasets, batch the inserts
        const int batchSize = 1000;
        var recordList = records.ToList();

        for (int i = 0; i < recordList.Count; i += batchSize)
        {
            var batch = recordList.Skip(i).Take(batchSize);
            await _context.SprPriceRecords.AddRangeAsync(batch, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteByUploadIdAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        await _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == uploadId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> GetCountByUploadIdAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .CountAsync(r => r.PriceFeedUploadId == uploadId, cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> SearchByDescriptionAsync(
        int dealerId,
        string searchTerm,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprPriceRecords
            .Where(r => r.DealerId == dealerId &&
                        r.ProductDescription.Contains(searchTerm))
            .OrderBy(r => r.ProductDescription);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<SprPriceRecord>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
