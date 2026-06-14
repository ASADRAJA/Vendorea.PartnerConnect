using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository interface for EDI document operations.
/// </summary>
public interface IEdiDocumentRepository
{
    /// <summary>
    /// Gets an EDI document by ID.
    /// </summary>
    Task<EdiDocument?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an EDI document by ID with related entities.
    /// </summary>
    Task<EdiDocument?> GetByIdWithRelationsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets EDI documents for a partner document.
    /// </summary>
    Task<IReadOnlyList<EdiDocument>> GetByPartnerDocumentIdAsync(
        int partnerDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets EDI documents by connection with optional filtering.
    /// </summary>
    Task<IReadOnlyList<EdiDocument>> GetByTradingPartnerAsync(
        int tradingPartnerId,
        string? transactionSetCode = null,
        EdiDirection? direction = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending outbound documents that haven't been sent.
    /// </summary>
    Task<IReadOnlyList<EdiDocument>> GetPendingOutboundAsync(
        int? tradingPartnerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents by interchange control number.
    /// </summary>
    Task<IReadOnlyList<EdiDocument>> GetByInterchangeControlNumberAsync(
        string interchangeControlNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document with the given control numbers already exists.
    /// </summary>
    Task<bool> ExistsAsync(
        string interchangeControlNumber,
        string groupControlNumber,
        string transactionControlNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new EDI document.
    /// </summary>
    Task<EdiDocument> AddAsync(EdiDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing EDI document.
    /// </summary>
    Task UpdateAsync(EdiDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an EDI document.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of documents by status for a trading partner.
    /// </summary>
    Task<Dictionary<string, int>> GetCountsByTransactionSetAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next control number for a given type.
    /// </summary>
    Task<int> GetNextControlNumberAsync(
        int tradingPartnerId,
        string controlNumberType,
        CancellationToken cancellationToken = default);
}
