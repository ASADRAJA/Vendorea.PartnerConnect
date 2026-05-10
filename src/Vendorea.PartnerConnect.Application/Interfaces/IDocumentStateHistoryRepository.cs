using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for document state history records.
/// </summary>
public interface IDocumentStateHistoryRepository
{
    /// <summary>
    /// Adds a new history entry.
    /// </summary>
    Task AddAsync(DocumentStateHistory history, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all history entries for a document.
    /// </summary>
    Task<IReadOnlyList<DocumentStateHistory>> GetByDocumentIdAsync(
        int documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent state transitions across all documents.
    /// </summary>
    Task<IReadOnlyList<DocumentStateHistory>> GetRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets state transitions by trigger type.
    /// </summary>
    Task<IReadOnlyList<DocumentStateHistory>> GetByTriggerAsync(
        DocumentTrigger trigger,
        DateTime? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets state transitions that resulted in a specific state.
    /// </summary>
    Task<IReadOnlyList<DocumentStateHistory>> GetByToStateAsync(
        DocumentState toState,
        DateTime? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
}
