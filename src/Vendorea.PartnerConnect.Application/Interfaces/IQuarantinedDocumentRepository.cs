using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for quarantined documents.
/// </summary>
public interface IQuarantinedDocumentRepository
{
    /// <summary>
    /// Gets a quarantine entry by ID.
    /// </summary>
    Task<QuarantinedDocument?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a quarantine entry by document ID.
    /// </summary>
    Task<QuarantinedDocument?> GetByDocumentIdAsync(int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all quarantine entries for a trading partner.
    /// </summary>
    Task<IReadOnlyList<QuarantinedDocument>> GetByTradingPartnerAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unresolved quarantine entries.
    /// </summary>
    Task<IReadOnlyList<QuarantinedDocument>> GetUnresolvedAsync(
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets quarantine entries by reason.
    /// </summary>
    Task<IReadOnlyList<QuarantinedDocument>> GetByReasonAsync(
        QuarantineReason reason,
        bool unresolvedOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new quarantine entry.
    /// </summary>
    Task AddAsync(QuarantinedDocument quarantine, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a quarantine entry.
    /// </summary>
    Task UpdateAsync(QuarantinedDocument quarantine, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets quarantine statistics.
    /// </summary>
    Task<QuarantineStatistics> GetStatisticsAsync(
        int? tradingPartnerId = null,
        CancellationToken cancellationToken = default);
}
