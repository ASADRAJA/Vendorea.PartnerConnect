using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository implementation for document state history.
/// </summary>
public class DocumentStateHistoryRepository : IDocumentStateHistoryRepository
{
    private readonly PartnerConnectDbContext _context;

    public DocumentStateHistoryRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(DocumentStateHistory history, CancellationToken cancellationToken = default)
    {
        _context.DocumentStateHistory.Add(history);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentStateHistory>> GetByDocumentIdAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentStateHistory
            .Where(h => h.PartnerDocumentId == documentId)
            .OrderBy(h => h.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentStateHistory>> GetRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentStateHistory
            .OrderByDescending(h => h.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentStateHistory>> GetByTriggerAsync(
        DocumentTrigger trigger,
        DateTime? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DocumentStateHistory
            .Where(h => h.Trigger == trigger);

        if (since.HasValue)
        {
            query = query.Where(h => h.OccurredAt >= since.Value);
        }

        query = query.OrderByDescending(h => h.OccurredAt);

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentStateHistory>> GetByToStateAsync(
        DocumentState toState,
        DateTime? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DocumentStateHistory
            .Where(h => h.ToState == toState);

        if (since.HasValue)
        {
            query = query.Where(h => h.OccurredAt >= since.Value);
        }

        query = query.OrderByDescending(h => h.OccurredAt);

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
