using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for document correlation (linking related documents).
/// </summary>
public interface IDocumentCorrelationRepository
{
    /// <summary>
    /// Gets correlation by ID.
    /// </summary>
    Task<DocumentCorrelation?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets correlation by business reference (e.g., PO number).
    /// </summary>
    Task<DocumentCorrelation?> GetByBusinessReferenceAsync(
        string businessReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all documents in a correlation chain.
    /// </summary>
    Task<IReadOnlyList<DocumentCorrelation>> GetCorrelationChainAsync(
        string businessReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Links a document to a correlation chain by business reference.
    /// Creates the chain if it doesn't exist.
    /// </summary>
    Task LinkDocumentAsync(
        int documentId,
        DocumentType documentType,
        string businessReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets correlated documents for a given document.
    /// </summary>
    Task<IReadOnlyList<PartnerDocument>> GetCorrelatedDocumentsAsync(
        int documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new correlation.
    /// </summary>
    Task<DocumentCorrelation> AddAsync(
        DocumentCorrelation correlation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a correlation.
    /// </summary>
    Task UpdateAsync(
        DocumentCorrelation correlation,
        CancellationToken cancellationToken = default);
}
