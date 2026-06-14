using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for SPR XML document persistence.
/// </summary>
public interface ISprXmlDocumentRepository
{
    /// <summary>
    /// Gets a document by ID.
    /// </summary>
    Task<SprXmlDocument?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a document by ID with related documents (original/response).
    /// </summary>
    Task<SprXmlDocument?> GetByIdWithRelationsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents by partner document ID.
    /// </summary>
    Task<IReadOnlyList<SprXmlDocument>> GetByPartnerDocumentIdAsync(
        int partnerDocumentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents by order number.
    /// </summary>
    Task<IReadOnlyList<SprXmlDocument>> GetByOrderNumberAsync(
        string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents by manifest number.
    /// </summary>
    Task<IReadOnlyList<SprXmlDocument>> GetByManifestNumberAsync(
        string manifestNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents for a connection with optional filtering.
    /// </summary>
    Task<IReadOnlyList<SprXmlDocument>> GetByTradingPartnerAsync(
        int tradingPartnerId,
        SprXmlDocumentType? documentType = null,
        EdiDirection? direction = null,
        SprXmlProcessingStatus? status = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending outbound documents that need to be sent.
    /// </summary>
    Task<IReadOnlyList<SprXmlDocument>> GetPendingOutboundAsync(
        int tradingPartnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document exists by business reference (order number, manifest, invoice).
    /// </summary>
    Task<bool> ExistsByBusinessReferenceAsync(
        SprXmlDocumentType documentType,
        string businessReference,
        EdiDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new document.
    /// </summary>
    Task<SprXmlDocument> AddAsync(SprXmlDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing document.
    /// </summary>
    Task UpdateAsync(SprXmlDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents awaiting acknowledgment.
    /// </summary>
    Task<IReadOnlyList<SprXmlDocument>> GetAwaitingAcknowledgmentAsync(
        int tradingPartnerId, TimeSpan? olderThan = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed documents for retry.
    /// </summary>
    Task<IReadOnlyList<SprXmlDocument>> GetFailedDocumentsAsync(
        int tradingPartnerId, int maxRetries = 3, CancellationToken cancellationToken = default);
}
