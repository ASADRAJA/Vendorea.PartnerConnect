using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class DocumentFingerprintRepository : IDocumentFingerprintRepository
{
    private readonly PartnerConnectDbContext _context;

    public DocumentFingerprintRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<DocumentFingerprint?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentFingerprints
            .Include(f => f.DealerPartnerConnection)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<DocumentFingerprint?> FindByHashAsync(
        int dealerPartnerConnectionId,
        DocumentType documentType,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentFingerprints
            .FirstOrDefaultAsync(f =>
                f.DealerPartnerConnectionId == dealerPartnerConnectionId &&
                f.DocumentType == documentType &&
                f.ContentHash == contentHash &&
                (f.ExpiresAt == null || f.ExpiresAt > DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentFingerprint>> FindAllByHashAsync(
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentFingerprints
            .Where(f => f.ContentHash == contentHash &&
                        (f.ExpiresAt == null || f.ExpiresAt > DateTime.UtcNow))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        int dealerPartnerConnectionId,
        DocumentType documentType,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentFingerprints
            .AnyAsync(f =>
                f.DealerPartnerConnectionId == dealerPartnerConnectionId &&
                f.DocumentType == documentType &&
                f.ContentHash == contentHash &&
                (f.ExpiresAt == null || f.ExpiresAt > DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<DocumentFingerprint> AddAsync(
        DocumentFingerprint fingerprint,
        CancellationToken cancellationToken = default)
    {
        _context.DocumentFingerprints.Add(fingerprint);
        await _context.SaveChangesAsync(cancellationToken);
        return fingerprint;
    }

    public async Task<IReadOnlyList<DocumentFingerprint>> GetExpiredAsync(
        DateTime cutoffDate,
        int maxRecords = 1000,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentFingerprints
            .Where(f => f.ExpiresAt != null && f.ExpiresAt <= cutoffDate)
            .Take(maxRecords)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentFingerprints
            .Where(f => f.ExpiresAt != null && f.ExpiresAt <= cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentFingerprint>> GetByConnectionAsync(
        int dealerPartnerConnectionId,
        int maxRecords = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentFingerprints
            .Where(f => f.DealerPartnerConnectionId == dealerPartnerConnectionId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(maxRecords)
            .ToListAsync(cancellationToken);
    }
}
