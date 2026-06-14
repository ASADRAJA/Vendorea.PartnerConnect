using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository implementation for quarantined documents.
/// </summary>
public class QuarantinedDocumentRepository : IQuarantinedDocumentRepository
{
    private readonly PartnerConnectDbContext _context;

    public QuarantinedDocumentRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<QuarantinedDocument?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.QuarantinedDocuments
            .Include(q => q.PartnerDocument)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuarantinedDocument?> GetByDocumentIdAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.QuarantinedDocuments
            .Include(q => q.PartnerDocument)
            .FirstOrDefaultAsync(q => q.PartnerDocumentId == documentId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuarantinedDocument>> GetByConnectionIdAsync(
        int connectionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.QuarantinedDocuments
            .Include(q => q.PartnerDocument)
            .Where(q => q.DealerPartnerConnectionId == connectionId)
            .OrderByDescending(q => q.QuarantinedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuarantinedDocument>> GetUnresolvedAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.QuarantinedDocuments
            .Include(q => q.PartnerDocument)
            .Where(q => q.Resolution == null)
            .OrderBy(q => q.QuarantinedAt);

        if (limit.HasValue)
        {
            return await query.Take(limit.Value).ToListAsync(cancellationToken);
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuarantinedDocument>> GetByReasonAsync(
        QuarantineReason reason,
        bool unresolvedOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.QuarantinedDocuments
            .Include(q => q.PartnerDocument)
            .Where(q => q.Reason == reason);

        if (unresolvedOnly)
        {
            query = query.Where(q => q.Resolution == null);
        }

        return await query
            .OrderByDescending(q => q.QuarantinedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(QuarantinedDocument quarantine, CancellationToken cancellationToken = default)
    {
        if (quarantine.TradingPartnerId == 0 && quarantine.DealerPartnerConnectionId > 0)
        {
            quarantine.TradingPartnerId = await _context.DealerPartnerConnections
                .Where(c => c.Id == quarantine.DealerPartnerConnectionId)
                .Select(c => c.TradingPartnerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        _context.QuarantinedDocuments.Add(quarantine);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(QuarantinedDocument quarantine, CancellationToken cancellationToken = default)
    {
        _context.QuarantinedDocuments.Update(quarantine);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuarantineStatistics> GetStatisticsAsync(
        int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.QuarantinedDocuments.AsQueryable();

        if (connectionId.HasValue)
        {
            query = query.Where(q => q.DealerPartnerConnectionId == connectionId.Value);
        }

        var allQuarantined = await query.ToListAsync(cancellationToken);

        var byReason = allQuarantined
            .GroupBy(q => q.Reason)
            .ToDictionary(g => g.Key, g => g.Count());

        var unresolvedItems = allQuarantined.Where(q => q.Resolution == null).ToList();
        var oldestUnresolved = unresolvedItems.MinBy(q => q.QuarantinedAt)?.QuarantinedAt;

        return new QuarantineStatistics
        {
            TotalQuarantined = allQuarantined.Count,
            UnresolvedCount = unresolvedItems.Count,
            ResolvedCount = allQuarantined.Count(q => q.Resolution != null),
            ReprocessedCount = allQuarantined.Count(q => q.Resolution == QuarantineResolution.Reprocessed),
            DiscardedCount = allQuarantined.Count(q => q.Resolution == QuarantineResolution.Discarded),
            ByReason = byReason,
            OldestUnresolved = oldestUnresolved
        };
    }
}
