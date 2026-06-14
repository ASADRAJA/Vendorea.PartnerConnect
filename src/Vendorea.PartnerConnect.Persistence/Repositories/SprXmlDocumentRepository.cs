using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class SprXmlDocumentRepository : ISprXmlDocumentRepository
{
    private readonly PartnerConnectDbContext _context;

    public SprXmlDocumentRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<SprXmlDocument?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.SprXmlDocuments
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<SprXmlDocument?> GetByIdWithRelationsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.SprXmlDocuments
            .Include(x => x.PartnerDocument)
            .Include(x => x.OriginalDocument)
            .Include(x => x.ResponseDocument)
            .Include(x => x.Responses)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SprXmlDocument>> GetByPartnerDocumentIdAsync(
        int partnerDocumentId, CancellationToken cancellationToken = default)
    {
        return await _context.SprXmlDocuments
            .Where(x => x.PartnerDocumentId == partnerDocumentId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprXmlDocument>> GetByOrderNumberAsync(
        string orderNumber, CancellationToken cancellationToken = default)
    {
        return await _context.SprXmlDocuments
            .Where(x => x.OrderNumber == orderNumber)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprXmlDocument>> GetByManifestNumberAsync(
        string manifestNumber, CancellationToken cancellationToken = default)
    {
        return await _context.SprXmlDocuments
            .Where(x => x.ManifestNumber == manifestNumber)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprXmlDocument>> GetByTradingPartnerAsync(
        int tradingPartnerId,
        SprXmlDocumentType? documentType = null,
        EdiDirection? direction = null,
        SprXmlProcessingStatus? status = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprXmlDocuments
            .Include(x => x.PartnerDocument)
            .Where(x => x.PartnerDocument != null && x.PartnerDocument.TradingPartnerId == tradingPartnerId);

        if (documentType.HasValue)
            query = query.Where(x => x.DocumentType == documentType.Value);

        if (direction.HasValue)
            query = query.Where(x => x.Direction == direction.Value);

        if (status.HasValue)
            query = query.Where(x => x.ProcessingStatus == status.Value);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprXmlDocument>> GetPendingOutboundAsync(
        int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        return await _context.SprXmlDocuments
            .Include(x => x.PartnerDocument)
            .Where(x => x.PartnerDocument != null
                && x.PartnerDocument.TradingPartnerId == tradingPartnerId
                && x.Direction == EdiDirection.Outbound
                && x.ProcessingStatus == SprXmlProcessingStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByBusinessReferenceAsync(
        SprXmlDocumentType documentType,
        string businessReference,
        EdiDirection direction,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprXmlDocuments
            .AnyAsync(x => x.DocumentType == documentType
                && x.BusinessReference == businessReference
                && x.Direction == direction,
                cancellationToken);
    }

    public async Task<SprXmlDocument> AddAsync(SprXmlDocument document, CancellationToken cancellationToken = default)
    {
        document.CreatedAt = DateTime.UtcNow;
        _context.SprXmlDocuments.Add(document);
        await _context.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task UpdateAsync(SprXmlDocument document, CancellationToken cancellationToken = default)
    {
        document.UpdatedAt = DateTime.UtcNow;
        _context.SprXmlDocuments.Update(document);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprXmlDocument>> GetAwaitingAcknowledgmentAsync(
        int tradingPartnerId, TimeSpan? olderThan = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SprXmlDocuments
            .Include(x => x.PartnerDocument)
            .Where(x => x.PartnerDocument != null
                && x.PartnerDocument.TradingPartnerId == tradingPartnerId
                && x.Direction == EdiDirection.Outbound
                && x.DocumentType == SprXmlDocumentType.EZPO4
                && x.ProcessingStatus == SprXmlProcessingStatus.Sent
                && !x.AcknowledgmentReceived);

        if (olderThan.HasValue)
        {
            var cutoff = DateTime.UtcNow - olderThan.Value;
            query = query.Where(x => x.SentAt < cutoff);
        }

        return await query
            .OrderBy(x => x.SentAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprXmlDocument>> GetFailedDocumentsAsync(
        int tradingPartnerId, int maxRetries = 3, CancellationToken cancellationToken = default)
    {
        return await _context.SprXmlDocuments
            .Include(x => x.PartnerDocument)
            .Where(x => x.PartnerDocument != null
                && x.PartnerDocument.TradingPartnerId == tradingPartnerId
                && x.ProcessingStatus == SprXmlProcessingStatus.Failed)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
