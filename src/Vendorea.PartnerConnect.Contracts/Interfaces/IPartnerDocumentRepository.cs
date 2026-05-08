using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Contracts.Interfaces;

/// <summary>
/// Repository interface for partner document operations.
/// </summary>
public interface IPartnerDocumentRepository
{
    Task<PartnerDocument?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartnerDocument>> GetByConnectionIdAsync(int connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartnerDocument>> GetPendingDocumentsAsync(CancellationToken cancellationToken = default);
    Task<PartnerDocument> AddAsync(PartnerDocument document, CancellationToken cancellationToken = default);
    Task UpdateAsync(PartnerDocument document, CancellationToken cancellationToken = default);
}
