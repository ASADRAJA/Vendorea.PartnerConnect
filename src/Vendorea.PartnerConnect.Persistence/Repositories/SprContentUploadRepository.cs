using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class SprContentUploadRepository : ISprContentUploadRepository
{
    private readonly PartnerConnectDbContext _context;

    public SprContentUploadRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<SprContentUpload?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.SprContentUploads
            .Include(u => u.TradingPartner)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SprContentUpload>> GetAllAsync(
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprContentUploads
            .Include(u => u.TradingPartner)
            .OrderByDescending(u => u.UploadedAt);

        if (offset.HasValue)
        {
            query = (IOrderedQueryable<SprContentUpload>)query.Skip(offset.Value);
        }

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<SprContentUpload>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<SprContentUpload?> GetLatestCompletedAsync(
        string localeId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprContentUploads
            .Include(u => u.TradingPartner)
            .Where(u => u.LocaleId == localeId &&
                        (u.Status == ContentUploadStatus.Completed ||
                         u.Status == ContentUploadStatus.PartiallyCompleted))
            .OrderByDescending(u => u.ProcessingCompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprContentUpload>> GetByStatusAsync(
        ContentUploadStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprContentUploads
            .Include(u => u.TradingPartner)
            .Where(u => u.Status == status)
            .OrderBy(u => u.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprContentUpload>> GetPendingUploadsAsync(
        CancellationToken cancellationToken = default)
    {
        var pendingStatuses = new[]
        {
            ContentUploadStatus.Pending,
            ContentUploadStatus.Extracting,
            ContentUploadStatus.Parsing,
            ContentUploadStatus.Importing
        };

        return await _context.SprContentUploads
            .Include(u => u.TradingPartner)
            .Where(u => pendingStatuses.Contains(u.Status))
            .OrderBy(u => u.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByVersionAsync(
        string contentVersion,
        string localeId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprContentUploads
            .AnyAsync(u => u.ContentVersion == contentVersion &&
                          u.LocaleId == localeId,
                     cancellationToken);
    }

    public async Task<SprContentUpload?> GetByFileHashAsync(
        string zipFileHash,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprContentUploads
            .Include(u => u.TradingPartner)
            .Where(u => u.ZipFileHash == zipFileHash)
            .OrderByDescending(u => u.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SprContentUpload> CreateAsync(
        SprContentUpload upload,
        CancellationToken cancellationToken = default)
    {
        _context.SprContentUploads.Add(upload);
        await _context.SaveChangesAsync(cancellationToken);
        return upload;
    }

    public async Task UpdateAsync(
        SprContentUpload upload,
        CancellationToken cancellationToken = default)
    {
        _context.SprContentUploads.Update(upload);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateProgressAsync(
        int uploadId,
        int processedProducts,
        int? errorProducts = null,
        CancellationToken cancellationToken = default)
    {
        var upload = await _context.SprContentUploads.FindAsync(
            new object[] { uploadId }, cancellationToken);

        if (upload != null)
        {
            upload.ProcessedProducts = processedProducts;
            if (errorProducts.HasValue)
            {
                upload.ErrorProducts = errorProducts.Value;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkCompletedAsync(
        int uploadId,
        int totalProducts,
        int processedProducts,
        int errorProducts,
        CancellationToken cancellationToken = default)
    {
        var upload = await _context.SprContentUploads.FindAsync(
            new object[] { uploadId }, cancellationToken);

        if (upload != null)
        {
            upload.Status = errorProducts > 0
                ? ContentUploadStatus.PartiallyCompleted
                : ContentUploadStatus.Completed;
            upload.TotalProducts = totalProducts;
            upload.ProcessedProducts = processedProducts;
            upload.ErrorProducts = errorProducts;
            upload.ProcessingCompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkFailedAsync(
        int uploadId,
        string errorDetails,
        CancellationToken cancellationToken = default)
    {
        var upload = await _context.SprContentUploads.FindAsync(
            new object[] { uploadId }, cancellationToken);

        if (upload != null)
        {
            upload.Status = ContentUploadStatus.Failed;
            upload.ErrorDetails = errorDetails;
            upload.ProcessingCompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        var upload = await _context.SprContentUploads.FindAsync(
            new object[] { uploadId }, cancellationToken);

        if (upload != null)
        {
            _context.SprContentUploads.Remove(upload);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SprContentUpload>> GetUploadHistoryAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprContentUploads
            .Include(u => u.TradingPartner)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(u => u.UploadedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(u => u.UploadedAt <= toDate.Value);
        }

        return await query
            .OrderByDescending(u => u.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprContentUpload>> GetByLocaleAsync(
        string localeId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprContentUploads
            .Include(u => u.TradingPartner)
            .Where(u => u.LocaleId == localeId)
            .OrderByDescending(u => u.UploadedAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<SprContentUpload>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<bool> HasDataForPartnerAsync(int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        return await _context.SprContentUploads
            .AnyAsync(u => u.TradingPartnerId == tradingPartnerId &&
                          (u.Status == ContentUploadStatus.Completed || u.Status == ContentUploadStatus.PartiallyCompleted),
                cancellationToken);
    }

    public async Task MarkPushedToM360Async(int uploadId, CancellationToken cancellationToken = default)
    {
        var upload = await _context.SprContentUploads.FindAsync(
            new object[] { uploadId }, cancellationToken);

        if (upload != null)
        {
            upload.PushedToM360At = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SprContentUpload>> GetByM360PushStatusAsync(
        string status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprContentUploads
            .Where(u => u.M360PushStatus == status)
            .OrderBy(u => u.M360PushClaimedAt)
            .ThenBy(u => u.UploadedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryClaimM360PushAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        // Atomic Queued -> Pushing. ExecuteUpdate issues a single UPDATE ... WHERE M360PushStatus =
        // 'Queued', so only one worker can win even if several poll the same row. Stamp the claim time
        // so a crashed push can later be detected and reclaimed.
        var now = DateTime.UtcNow;
        var rowsAffected = await _context.SprContentUploads
            .Where(u => u.Id == uploadId && u.M360PushStatus == "Queued")
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.M360PushStatus, "Pushing")
                    .SetProperty(u => u.M360PushClaimedAt, now)
                    .SetProperty(u => u.M360PushError, (string?)null),
                cancellationToken);

        return rowsAffected == 1;
    }

    public async Task<bool> TryEnqueueM360PushAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        // Guard against double-queue: only enqueue when the push is not already Queued or Pushing.
        // Zero the counters and clear any prior error so the modal starts from a clean slate.
        var rowsAffected = await _context.SprContentUploads
            .Where(u => u.Id == uploadId
                        && u.M360PushStatus != "Queued"
                        && u.M360PushStatus != "Pushing")
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.M360PushStatus, "Queued")
                    .SetProperty(u => u.M360PushClaimedAt, (DateTime?)null)
                    .SetProperty(u => u.M360PushTotalProducts, 0)
                    .SetProperty(u => u.M360PushProductsPushed, 0)
                    .SetProperty(u => u.M360PushCurrentBatch, 0)
                    .SetProperty(u => u.M360PushTotalBatches, 0)
                    .SetProperty(u => u.M360PushError, (string?)null),
                cancellationToken);

        return rowsAffected == 1;
    }

    public async Task UpdateM360PushProgressAsync(
        int uploadId,
        int productsPushed,
        int currentBatch,
        int totalBatches,
        int totalProducts,
        CancellationToken cancellationToken = default)
    {
        // Lightweight per-page update of just the counter columns — no entity tracking churn.
        await _context.SprContentUploads
            .Where(u => u.Id == uploadId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.M360PushProductsPushed, productsPushed)
                    .SetProperty(u => u.M360PushCurrentBatch, currentBatch)
                    .SetProperty(u => u.M360PushTotalBatches, totalBatches)
                    .SetProperty(u => u.M360PushTotalProducts, totalProducts),
                cancellationToken);
    }

    public async Task MarkM360PushCompletedAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await _context.SprContentUploads
            .Where(u => u.Id == uploadId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.M360PushStatus, "Pushed")
                    .SetProperty(u => u.PushedToM360At, now)
                    .SetProperty(u => u.M360PushError, (string?)null),
                cancellationToken);
    }

    public async Task MarkM360PushFailedAsync(int uploadId, string error, CancellationToken cancellationToken = default)
    {
        // M360PushError is capped at 1024 chars in the schema.
        var trimmed = error.Length > 1024 ? error.Substring(0, 1024) : error;
        await _context.SprContentUploads
            .Where(u => u.Id == uploadId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.M360PushStatus, "Failed")
                    .SetProperty(u => u.M360PushError, trimmed),
                cancellationToken);
    }

    public async Task<int> ReclaimStaleM360PushAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        // A worker that crashed after claiming leaves a push stuck in Pushing. Move any Pushing row
        // claimed before the cutoff to a terminal Failed state (no auto-requeue — an operator re-clicks
        // Push, which is an idempotent upsert).
        return await _context.SprContentUploads
            .Where(u => u.M360PushStatus == "Pushing"
                        && u.M360PushClaimedAt != null
                        && u.M360PushClaimedAt < olderThanUtc)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.M360PushStatus, "Failed")
                    .SetProperty(u => u.M360PushError, "Reclaimed: push process interrupted"),
                cancellationToken);
    }
}
