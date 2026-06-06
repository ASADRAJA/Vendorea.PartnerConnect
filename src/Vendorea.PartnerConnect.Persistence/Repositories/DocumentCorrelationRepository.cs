using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository for document correlation (linking related documents).
/// </summary>
public class DocumentCorrelationRepository : IDocumentCorrelationRepository
{
    private readonly PartnerConnectDbContext _context;

    public DocumentCorrelationRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<DocumentCorrelation?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentCorrelations
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<DocumentCorrelation?> GetByBusinessReferenceAsync(
        string businessReference,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentCorrelations
            .Include(c => c.SourceDocument)
            .Include(c => c.TargetDocument)
            .FirstOrDefaultAsync(c => c.BusinessReference == businessReference, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentCorrelation>> GetCorrelationChainAsync(
        string businessReference,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentCorrelations
            .Include(c => c.SourceDocument)
            .Include(c => c.TargetDocument)
            .Where(c => c.BusinessReference == businessReference)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task LinkDocumentAsync(
        int documentId,
        DocumentType documentType,
        string businessReference,
        CancellationToken cancellationToken = default)
    {
        // Find existing correlation chain by business reference
        var existingCorrelation = await _context.DocumentCorrelations
            .Where(c => c.BusinessReference == businessReference)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Use the most recent document in the chain as source, or use this document as both source and target
        var sourceDocumentId = existingCorrelation?.TargetDocumentId ?? documentId;

        var newCorrelation = new DocumentCorrelation
        {
            BusinessReference = businessReference,
            SourceDocumentId = sourceDocumentId,
            TargetDocumentId = documentId,
            CorrelationType = DetermineCorrelationType(documentType),
            Method = CorrelationMethod.AutomaticPoNumber,
            CreatedAt = DateTime.UtcNow
        };

        _context.DocumentCorrelations.Add(newCorrelation);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerDocument>> GetCorrelatedDocumentsAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        // Get business references this document is part of
        var businessReferences = await _context.DocumentCorrelations
            .Where(c => c.SourceDocumentId == documentId || c.TargetDocumentId == documentId)
            .Select(c => c.BusinessReference)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (businessReferences.Count == 0)
            return Array.Empty<PartnerDocument>();

        // Get all document IDs in those correlation chains (excluding the current document)
        var correlatedDocumentIds = await _context.DocumentCorrelations
            .Where(c => businessReferences.Contains(c.BusinessReference))
            .SelectMany(c => new[] { c.SourceDocumentId, c.TargetDocumentId })
            .Where(id => id != documentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await _context.PartnerDocuments
            .Where(d => correlatedDocumentIds.Contains(d.Id))
            .OrderBy(d => d.ReceivedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentCorrelation> AddAsync(
        DocumentCorrelation correlation,
        CancellationToken cancellationToken = default)
    {
        correlation.CreatedAt = DateTime.UtcNow;
        _context.DocumentCorrelations.Add(correlation);
        await _context.SaveChangesAsync(cancellationToken);
        return correlation;
    }

    public async Task UpdateAsync(
        DocumentCorrelation correlation,
        CancellationToken cancellationToken = default)
    {
        _context.DocumentCorrelations.Update(correlation);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static CorrelationType DetermineCorrelationType(DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.PurchaseOrder => CorrelationType.DocumentToResponse,
            DocumentType.PurchaseOrderAcknowledgment => CorrelationType.OrderToAcknowledgment,
            DocumentType.AdvanceShipNotice => CorrelationType.OrderToShipment,
            DocumentType.Invoice => CorrelationType.OrderToInvoice,
            DocumentType.CreditMemo => CorrelationType.InvoiceToCreditMemo,
            _ => CorrelationType.DocumentToResponse
        };
    }
}
