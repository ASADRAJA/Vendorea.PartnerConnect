using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class PriceFeedUploadRepository : IPriceFeedUploadRepository
{
    private readonly PartnerConnectDbContext _context;

    public PriceFeedUploadRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<PriceFeedUpload?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.PriceFeedUploads
            .Include(u => u.TradingPartner)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PriceFeedUpload>> GetByDealerIdAsync(
        int dealerId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PriceFeedUploads
            .Include(u => u.TradingPartner)
            .Where(u => u.DealerId == dealerId)
            .OrderByDescending(u => u.UploadedAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<PriceFeedUpload>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PriceFeedUpload>> GetByDealerAndPartnerAsync(
        int dealerId,
        int tradingPartnerId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PriceFeedUploads
            .Include(u => u.TradingPartner)
            .Where(u => u.DealerId == dealerId && u.TradingPartnerId == tradingPartnerId)
            .OrderByDescending(u => u.UploadedAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<PriceFeedUpload>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PriceFeedUpload?> GetLatestAsync(
        int dealerId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PriceFeedUploads
            .Include(u => u.TradingPartner)
            .Where(u => u.DealerId == dealerId &&
                        u.TradingPartnerId == tradingPartnerId &&
                        u.Status == PriceFeedUploadStatus.Completed)
            .OrderByDescending(u => u.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PriceFeedUpload?> GetSuccessfulUploadByHashAsync(
        int dealerId,
        int tradingPartnerId,
        string fileHash,
        CancellationToken cancellationToken = default)
    {
        // Only a previously *successful* import blocks a re-upload of the same content.
        // Failed or zero-record attempts are intentionally ignored so the file can be retried.
        return await _context.PriceFeedUploads
            .Where(u => u.DealerId == dealerId &&
                        u.TradingPartnerId == tradingPartnerId &&
                        u.FileHash == fileHash &&
                        u.RecordCount > 0 &&
                        (u.Status == PriceFeedUploadStatus.Completed ||
                         u.Status == PriceFeedUploadStatus.PushedToMerchant360))
            .OrderByDescending(u => u.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryClaimForProcessingAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        // Atomic Pending -> Processing transition. ExecuteUpdate issues a single UPDATE ... WHERE
        // Status = 'Pending', so only one worker can win even if several poll the same row.
        // Stamp ProcessingStartedAt so a stuck row can later be detected and reclaimed.
        var now = DateTime.UtcNow;
        var rowsAffected = await _context.PriceFeedUploads
            .Where(u => u.Id == uploadId && u.Status == PriceFeedUploadStatus.Pending)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.Status, PriceFeedUploadStatus.Processing)
                    .SetProperty(u => u.ProcessingStartedAt, now),
                cancellationToken);

        return rowsAffected == 1;
    }

    public async Task<bool> TryCancelPendingAsync(int uploadId, string reason, CancellationToken cancellationToken = default)
    {
        // Only a Pending upload can be cancelled — a Processing one is mid-insert (let it finish or
        // be reclaimed); terminal ones are already done.
        var now = DateTime.UtcNow;
        var rowsAffected = await _context.PriceFeedUploads
            .Where(u => u.Id == uploadId && u.Status == PriceFeedUploadStatus.Pending)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.Status, PriceFeedUploadStatus.Cancelled)
                    .SetProperty(u => u.ErrorMessage, reason)
                    .SetProperty(u => u.ProcessedAt, now),
                cancellationToken);

        return rowsAffected == 1;
    }

    public async Task<int> ReclaimStaleProcessingAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        // A worker that crashed after claiming leaves an upload stuck in Processing. Return any
        // Processing row claimed before the cutoff to Pending so it gets retried.
        return await _context.PriceFeedUploads
            .Where(u => u.Status == PriceFeedUploadStatus.Processing
                        && u.ProcessingStartedAt != null
                        && u.ProcessingStartedAt < olderThanUtc)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.Status, PriceFeedUploadStatus.Pending)
                    .SetProperty(u => u.ProcessingStartedAt, (DateTime?)null),
                cancellationToken);
    }

    public async Task DeleteAsync(PriceFeedUpload upload, CancellationToken cancellationToken = default)
    {
        _context.PriceFeedUploads.Remove(upload);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PriceFeedUpload> AddAsync(PriceFeedUpload upload, CancellationToken cancellationToken = default)
    {
        _context.PriceFeedUploads.Add(upload);
        await _context.SaveChangesAsync(cancellationToken);
        return upload;
    }

    public async Task UpdateAsync(PriceFeedUpload upload, CancellationToken cancellationToken = default)
    {
        _context.PriceFeedUploads.Update(upload);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PriceFeedUpload>> GetByStatusAsync(
        PriceFeedUploadStatus status,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PriceFeedUploads
            .Include(u => u.TradingPartner)
            .Where(u => u.Status == status)
            .OrderBy(u => u.UploadedAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<PriceFeedUpload>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<bool> HasDataForPartnerAsync(int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        return await _context.PriceFeedUploads
            .AnyAsync(u => u.TradingPartnerId == tradingPartnerId &&
                          u.Status == PriceFeedUploadStatus.Completed,
                cancellationToken);
    }

    public async Task<IReadOnlyList<PriceFeedUpload>> GetAllAsync(
        int? dealerId = null,
        int? tradingPartnerId = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PriceFeedUploads
            .Include(u => u.TradingPartner)
            .AsQueryable();

        if (dealerId.HasValue)
        {
            query = query.Where(u => u.DealerId == dealerId.Value);
        }

        if (tradingPartnerId.HasValue)
        {
            query = query.Where(u => u.TradingPartnerId == tradingPartnerId.Value);
        }

        query = query.OrderByDescending(u => u.UploadedAt);

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
