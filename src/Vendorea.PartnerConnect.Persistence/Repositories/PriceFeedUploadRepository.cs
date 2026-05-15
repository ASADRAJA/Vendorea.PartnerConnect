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

    public async Task<bool> ExistsByHashAsync(
        int dealerId,
        int tradingPartnerId,
        string fileHash,
        CancellationToken cancellationToken = default)
    {
        return await _context.PriceFeedUploads
            .AnyAsync(u => u.DealerId == dealerId &&
                          u.TradingPartnerId == tradingPartnerId &&
                          u.FileHash == fileHash,
                cancellationToken);
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
}
