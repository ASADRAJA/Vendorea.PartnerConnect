namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Archives the original raw document content.
/// Maintains immutable record for audit, debugging, and reprocessing.
/// </summary>
public class RawDocumentArchive
{
    public int Id { get; set; }

    /// <summary>
    /// The partner document this archive belongs to.
    /// </summary>
    public int PartnerDocumentId { get; set; }

    /// <summary>
    /// Hash of the raw content for duplicate detection.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Algorithm used for hash (SHA256, MD5, etc.).
    /// </summary>
    public string HashAlgorithm { get; set; } = "SHA256";

    /// <summary>
    /// Original filename if available.
    /// </summary>
    public string? OriginalFileName { get; set; }

    /// <summary>
    /// Content type (application/xml, application/edi-x12, etc.).
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Size of the raw content in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Storage location type.
    /// </summary>
    public ArchiveStorageType StorageType { get; set; }

    /// <summary>
    /// Storage path/key (blob URL, file path, etc.).
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// For small documents, the content may be stored inline.
    /// </summary>
    public byte[]? InlineContent { get; set; }

    /// <summary>
    /// Whether the content is compressed.
    /// </summary>
    public bool IsCompressed { get; set; }

    /// <summary>
    /// Compression algorithm if compressed.
    /// </summary>
    public string? CompressionAlgorithm { get; set; }

    /// <summary>
    /// When this archive was created.
    /// </summary>
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Retention policy applied.
    /// </summary>
    public string? RetentionPolicy { get; set; }

    /// <summary>
    /// When this archive expires (if applicable).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public PartnerDocument? PartnerDocument { get; set; }
}

/// <summary>
/// Type of archive storage.
/// </summary>
public enum ArchiveStorageType
{
    /// <summary>Content stored inline in database.</summary>
    Inline = 0,

    /// <summary>Content stored in blob storage.</summary>
    BlobStorage = 10,

    /// <summary>Content stored in file system.</summary>
    FileSystem = 20,

    /// <summary>Content stored in S3-compatible storage.</summary>
    S3 = 30
}
