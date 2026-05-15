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
            .Include(d => d.DealerPartnerConnection)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerDocument>> GetByConnectionIdAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        return await _context.PartnerDocuments
            .Where(d => d.DealerPartnerConnectionId == connectionId)
            .OrderByDescending(d => d.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerDocument>> GetPendingDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PartnerDocuments
            .Include(d => d.DealerPartnerConnection)
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
}
