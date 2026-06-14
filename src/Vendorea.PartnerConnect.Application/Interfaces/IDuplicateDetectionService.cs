using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for detecting duplicate documents.
/// </summary>
public interface IDuplicateDetectionService
{
    /// <summary>
    /// Checks if a document with the given content hash is a duplicate.
    /// </summary>
    /// <param name="tradingPartnerId">The trading partner ID.</param>
    /// <param name="documentType">The document type.</param>
    /// <param name="contentHash">SHA-256 hash of the content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if duplicate, false otherwise.</returns>
    Task<bool> IsDuplicateAsync(
        int tradingPartnerId,
        DocumentType documentType,
        string contentHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document is a duplicate and returns the original if found.
    /// </summary>
    /// <param name="tradingPartnerId">The trading partner ID.</param>
    /// <param name="documentType">The document type.</param>
    /// <param name="contentHash">SHA-256 hash of the content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The duplicate check result.</returns>
    Task<DuplicateCheckResult> CheckDuplicateAsync(
        int tradingPartnerId,
        DocumentType documentType,
        string contentHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a document fingerprint after successful processing.
    /// </summary>
    /// <param name="tradingPartnerId">The trading partner ID.</param>
    /// <param name="documentType">The document type.</param>
    /// <param name="contentHash">SHA-256 hash of the content.</param>
    /// <param name="originalDocumentId">The ID of the processed document.</param>
    /// <param name="fileName">Original filename.</param>
    /// <param name="fileSizeBytes">File size in bytes.</param>
    /// <param name="retentionDays">Days to retain the fingerprint (null = forever).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RegisterFingerprintAsync(
        int tradingPartnerId,
        DocumentType documentType,
        string contentHash,
        int originalDocumentId,
        string? fileName = null,
        long? fileSizeBytes = null,
        int? retentionDays = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the SHA-256 hash of a stream.
    /// </summary>
    Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the SHA-256 hash of a byte array.
    /// </summary>
    string ComputeHash(byte[] content);

    /// <summary>
    /// Cleans up expired fingerprints.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of fingerprints deleted.</returns>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a duplicate check operation.
/// </summary>
public record DuplicateCheckResult
{
    /// <summary>
    /// Whether the document is a duplicate.
    /// </summary>
    public bool IsDuplicate { get; init; }

    /// <summary>
    /// The original document ID if duplicate.
    /// </summary>
    public int? OriginalDocumentId { get; init; }

    /// <summary>
    /// Original filename if available.
    /// </summary>
    public string? OriginalFileName { get; init; }

    /// <summary>
    /// When the original was processed.
    /// </summary>
    public DateTime? OriginalProcessedAt { get; init; }

    /// <summary>
    /// Creates a result indicating no duplicate found.
    /// </summary>
    public static DuplicateCheckResult NotDuplicate => new() { IsDuplicate = false };

    /// <summary>
    /// Creates a result indicating a duplicate was found.
    /// </summary>
    public static DuplicateCheckResult Duplicate(DocumentFingerprint fingerprint) => new()
    {
        IsDuplicate = true,
        OriginalDocumentId = fingerprint.OriginalDocumentId,
        OriginalFileName = fingerprint.OriginalFileName,
        OriginalProcessedAt = fingerprint.CreatedAt
    };
}
