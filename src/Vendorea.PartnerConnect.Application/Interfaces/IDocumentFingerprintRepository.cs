using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository interface for document fingerprint operations.
/// </summary>
public interface IDocumentFingerprintRepository
{
    /// <summary>
    /// Gets a fingerprint by ID.
    /// </summary>
    Task<DocumentFingerprint?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a fingerprint by content hash for a specific connection and document type.
    /// </summary>
    Task<DocumentFingerprint?> FindByHashAsync(
        int dealerPartnerConnectionId,
        DocumentType documentType,
        string contentHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all fingerprints matching a content hash across all connections.
    /// </summary>
    Task<IReadOnlyList<DocumentFingerprint>> FindAllByHashAsync(
        string contentHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document with the given hash already exists.
    /// </summary>
    Task<bool> ExistsAsync(
        int dealerPartnerConnectionId,
        DocumentType documentType,
        string contentHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new fingerprint.
    /// </summary>
    Task<DocumentFingerprint> AddAsync(DocumentFingerprint fingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets expired fingerprints for cleanup.
    /// </summary>
    Task<IReadOnlyList<DocumentFingerprint>> GetExpiredAsync(
        DateTime cutoffDate,
        int maxRecords = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes expired fingerprints.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets fingerprints for a specific connection.
    /// </summary>
    Task<IReadOnlyList<DocumentFingerprint>> GetByConnectionAsync(
        int dealerPartnerConnectionId,
        int maxRecords = 100,
        CancellationToken cancellationToken = default);
}
