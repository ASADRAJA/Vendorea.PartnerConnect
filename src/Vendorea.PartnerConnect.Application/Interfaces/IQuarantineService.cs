using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for managing quarantined documents.
/// </summary>
public interface IQuarantineService
{
    /// <summary>
    /// Quarantines a document.
    /// </summary>
    Task<QuarantinedDocument> QuarantineDocumentAsync(
        PartnerDocument document,
        QuarantineReason reason,
        string? errorCode = null,
        string? errorMessage = null,
        string? errorDetails = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a quarantine entry by document ID.
    /// </summary>
    Task<QuarantinedDocument?> GetByDocumentIdAsync(
        int documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all quarantined documents for a dealer connection.
    /// </summary>
    Task<IReadOnlyList<QuarantinedDocument>> GetByConnectionIdAsync(
        int connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unresolved quarantined documents.
    /// </summary>
    Task<IReadOnlyList<QuarantinedDocument>> GetUnresolvedAsync(
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a quarantine entry as reviewed.
    /// </summary>
    Task MarkReviewedAsync(
        int quarantineId,
        string reviewedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a quarantine entry.
    /// </summary>
    Task ResolveAsync(
        int quarantineId,
        QuarantineResolution resolution,
        string resolvedBy,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to reprocess a quarantined document.
    /// </summary>
    Task<bool> ReprocessAsync(
        int quarantineId,
        string performedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards a quarantined document.
    /// </summary>
    Task DiscardAsync(
        int quarantineId,
        string performedBy,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets quarantine statistics for a connection.
    /// </summary>
    Task<QuarantineStatistics> GetStatisticsAsync(
        int? connectionId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about quarantined documents.
/// </summary>
public class QuarantineStatistics
{
    public int TotalQuarantined { get; init; }
    public int UnresolvedCount { get; init; }
    public int ResolvedCount { get; init; }
    public int ReprocessedCount { get; init; }
    public int DiscardedCount { get; init; }
    public Dictionary<QuarantineReason, int> ByReason { get; init; } = new();
    public DateTime? OldestUnresolved { get; init; }
}
