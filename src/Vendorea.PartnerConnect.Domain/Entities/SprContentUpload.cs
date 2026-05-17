namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Tracks an SPR enhanced content import operation.
/// Content is SHARED MASTER DATA - uploaded once and available to all dealers.
/// </summary>
public class SprContentUpload
{
    public int Id { get; set; }

    /// <summary>
    /// Trading partner providing the content (SPR).
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// Navigation property to trading partner.
    /// </summary>
    public TradingPartner? TradingPartner { get; set; }

    /// <summary>
    /// Content version identifier (e.g., "5242", "current").
    /// </summary>
    public string ContentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Locale of the content (EN_US, EN_CA, ES_US, FR_CA).
    /// </summary>
    public string LocaleId { get; set; } = "EN_US";

    /// <summary>
    /// Original ZIP file name.
    /// </summary>
    public string ZipFileName { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the ZIP file for duplicate detection.
    /// </summary>
    public string? ZipFileHash { get; set; }

    /// <summary>
    /// Size of the ZIP file in bytes.
    /// </summary>
    public long ZipFileSizeBytes { get; set; }

    /// <summary>
    /// Path where the ZIP file is stored (if retained).
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// Current processing status.
    /// </summary>
    public ContentUploadStatus Status { get; set; } = ContentUploadStatus.Pending;

    /// <summary>
    /// Total number of products in the content package.
    /// </summary>
    public int TotalProducts { get; set; }

    /// <summary>
    /// Number of products successfully processed.
    /// </summary>
    public int ProcessedProducts { get; set; }

    /// <summary>
    /// Number of new products added.
    /// </summary>
    public int NewProducts { get; set; }

    /// <summary>
    /// Number of existing products updated.
    /// </summary>
    public int UpdatedProducts { get; set; }

    /// <summary>
    /// Number of products skipped (unchanged or excluded).
    /// </summary>
    public int SkippedProducts { get; set; }

    /// <summary>
    /// Number of products with errors.
    /// </summary>
    public int ErrorProducts { get; set; }

    /// <summary>
    /// Error details if processing failed.
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Correlation ID for tracking through the system.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// User who initiated the upload.
    /// </summary>
    public string? UploadedByUserId { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }

    /// <summary>
    /// When content was pushed to Merchant360.
    /// </summary>
    public DateTime? PushedToM360At { get; set; }
}

/// <summary>
/// Status of a content upload operation.
/// </summary>
public enum ContentUploadStatus
{
    /// <summary>Upload received, waiting to process.</summary>
    Pending,

    /// <summary>Extracting ZIP contents.</summary>
    Extracting,

    /// <summary>Parsing content files.</summary>
    Parsing,

    /// <summary>Importing records to database.</summary>
    Importing,

    /// <summary>Successfully completed.</summary>
    Completed,

    /// <summary>Completed with some errors.</summary>
    PartiallyCompleted,

    /// <summary>Processing failed.</summary>
    Failed,

    /// <summary>Upload was cancelled.</summary>
    Cancelled
}
