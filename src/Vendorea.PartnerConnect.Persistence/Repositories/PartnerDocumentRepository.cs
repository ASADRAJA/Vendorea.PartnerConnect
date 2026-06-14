using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class PartnerDocumentRepository : IPartnerDocumentRepository
{
    private readonly PartnerConnectDbContext _context;

    public PartnerDocumentRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<PartnerDocument?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.PartnerDocuments
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerDocument>> GetByTradingPartnerAsync(int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        return await _context.PartnerDocuments
            .Where(d => d.TradingPartnerId == tradingPartnerId)
            .OrderByDescending(d => d.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerDocument>> GetByTenantAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.PartnerDocuments
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerDocument>> GetPendingDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PartnerDocuments
            .Where(d => d.State == DocumentState.Received || d.State == DocumentState.Queued)
            .OrderBy(d => d.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PartnerDocument> AddAsync(PartnerDocument document, CancellationToken cancellationToken = default)
    {
        _context.PartnerDocuments.Add(document);
        await _context.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task UpdateAsync(PartnerDocument document, CancellationToken cancellationToken = default)
    {
        _context.PartnerDocuments.Update(document);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DocumentStats> GetDocumentStatsAsync(CancellationToken cancellationToken = default)
    {
        var pendingStates = new[]
        {
            DocumentState.Received,
            DocumentState.Validating,
            DocumentState.Validated,
            DocumentState.Mapping,
            DocumentState.Mapped,
            DocumentState.Queued,
            DocumentState.Sending,
            DocumentState.AwaitingAcknowledgment
        };

        var failedStates = new[]
        {
            DocumentState.ValidationFailed,
            DocumentState.MapError,
            DocumentState.SendError,
            DocumentState.Rejected,
            DocumentState.Cancelled
        };

        var total = await _context.PartnerDocuments.CountAsync(cancellationToken);
        var pending = await _context.PartnerDocuments.CountAsync(d => pendingStates.Contains(d.State), cancellationToken);
        var failed = await _context.PartnerDocuments.CountAsync(d => failedStates.Contains(d.State), cancellationToken);
        var quarantined = await _context.PartnerDocuments.CountAsync(d => d.State == DocumentState.Quarantined, cancellationToken);

        return new DocumentStats
        {
            Total = total,
            Pending = pending,
            Failed = failed,
            Quarantined = quarantined
        };
    }

    public async Task<IReadOnlyList<PartnerDocument>> GetByStatusAndDirectionAsync(
        DocumentStatus status,
        DocumentDirection direction,
        int? tradingPartnerId = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        // Map DocumentStatus to DocumentState for querying
        var targetStates = MapStatusToStates(status);

        var query = _context.PartnerDocuments
            .Where(d => targetStates.Contains(d.State) && d.Direction == direction);

        if (tradingPartnerId.HasValue)
        {
            query = query.Where(d => d.TradingPartnerId == tradingPartnerId.Value);
        }

        return await query
            .OrderBy(d => d.ReceivedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerDocument>> GetFailedDocumentsForRetryAsync(
        int maxAttempts,
        int take = 25,
        CancellationToken cancellationToken = default)
    {
        var failedStates = new[]
        {
            DocumentState.ValidationFailed,
            DocumentState.MapError,
            DocumentState.SendError,
            DocumentState.Rejected
        };

        return await _context.PartnerDocuments
            .Where(d => failedStates.Contains(d.State) && d.RetryCount < maxAttempts)
            .OrderBy(d => d.ProcessingCompletedAt ?? d.ReceivedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private static DocumentState[] MapStatusToStates(DocumentStatus status)
    {
        return status switch
        {
            DocumentStatus.Received => new[] { DocumentState.Received },
            DocumentStatus.Pending => new[] { DocumentState.Received },
            DocumentStatus.Queued => new[] { DocumentState.Queued },
            DocumentStatus.Processing => new[]
            {
                DocumentState.Validating,
                DocumentState.Validated,
                DocumentState.Mapping,
                DocumentState.Mapped,
                DocumentState.Sending,
                DocumentState.AwaitingAcknowledgment
            },
            DocumentStatus.Completed => new[] { DocumentState.Completed, DocumentState.Acknowledged },
            DocumentStatus.Failed => new[]
            {
                DocumentState.ValidationFailed,
                DocumentState.MapError,
                DocumentState.SendError,
                DocumentState.Quarantined,
                DocumentState.Rejected
            },
            DocumentStatus.Cancelled => new[] { DocumentState.Cancelled },
            _ => new[] { DocumentState.Received }
        };
    }
}
