namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Stores fingerprints (hashes) of processed documents for duplicate detection.
/// </summary>
public class DocumentFingerprint
{
    public int Id { get; set; }

    /// <summary>
    /// Trading partner this fingerprint belongs to. Duplicate detection is per-partner
    /// (content is shared).
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// The type of document.
    /// </summary>
    public DocumentType DocumentType { get; set; }

    /// <summary>
    /// SHA-256 hash of the document content for exact duplicate detection.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Optional structural hash for near-duplicate detection (ignores whitespace, timestamps, etc.).
    /// </summary>
    public string? StructuralHash { get; set; }

    /// <summary>
    /// Reference to the original document that created this fingerprint.
    /// </summary>
    public int OriginalDocumentId { get; set; }

    /// <summary>
    /// Original filename if available.
    /// </summary>
    public string? OriginalFileName { get; set; }

    /// <summary>
    /// Size of the original document in bytes.
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// When this fingerprint was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this fingerprint expires (for cleanup). Null = never expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    // Navigation properties
    public PartnerDocument? OriginalDocument { get; set; }
}
