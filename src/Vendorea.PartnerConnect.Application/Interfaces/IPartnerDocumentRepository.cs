using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository interface for partner document operations.
/// </summary>
public interface IPartnerDocumentRepository
{
    Task<PartnerDocument?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartnerDocument>> GetByTradingPartnerAsync(int tradingPartnerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartnerDocument>> GetByTenantAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartnerDocument>> GetPendingDocumentsAsync(CancellationToken cancellationToken = default);
    Task<PartnerDocument> AddAsync(PartnerDocument document, CancellationToken cancellationToken = default);
    Task UpdateAsync(PartnerDocument document, CancellationToken cancellationToken = default);
    Task<DocumentStats> GetDocumentStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents by status and direction.
    /// </summary>
    Task<IReadOnlyList<PartnerDocument>> GetByStatusAndDirectionAsync(
        DocumentStatus status,
        DocumentDirection direction,
        int? tradingPartnerId = null,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed documents eligible for retry.
    /// </summary>
    Task<IReadOnlyList<PartnerDocument>> GetFailedDocumentsForRetryAsync(
        int maxAttempts,
        int take = 25,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Document statistics for dashboard.
/// </summary>
public class DocumentStats
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Failed { get; set; }
    public int Quarantined { get; set; }
}
