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
}
