namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Tracks price feed file uploads for all suppliers.
/// Each upload contains price records stored in supplier-specific tables.
/// </summary>
public class PriceFeedUpload
{
    public int Id { get; set; }

    /// <summary>
    /// The dealer/tenant this upload belongs to.
    /// </summary>
    public int DealerId { get; set; }

    /// <summary>
    /// The trading partner (supplier) this upload is from.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// Navigation property to the trading partner.
    /// </summary>
    public TradingPartner? TradingPartner { get; set; }

    /// <summary>
    /// Original file name uploaded by the user.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the file content for duplicate detection.
    /// </summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>
    /// Size of the uploaded file in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Path where the raw file is stored locally.
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// Current processing status of this upload.
    /// </summary>
    public PriceFeedUploadStatus Status { get; set; } = PriceFeedUploadStatus.Pending;

    /// <summary>
    /// Number of records successfully parsed.
    /// </summary>
    public int RecordCount { get; set; }

    /// <summary>
    /// Number of records that failed to parse.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Error message if the upload failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the file was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who uploaded the file (if applicable).
    /// </summary>
    public string? UploadedByUserId { get; set; }

    /// <summary>
    /// When a worker claimed this upload for processing (Pending → Processing). Used to detect and
    /// reclaim uploads left stuck in Processing by a crashed/restarted worker.
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }

    /// <summary>
    /// When parsing completed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// When the data was pushed to Merchant360.
    /// </summary>
    public DateTime? PushedToMerchant360At { get; set; }

    /// <summary>
    /// Correlation ID for tracking through the system.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Status of a price feed upload.
/// </summary>
public enum PriceFeedUploadStatus
{
    /// <summary>
    /// Upload received, waiting to be processed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Currently being parsed and stored.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Parsing completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Parsing failed with errors.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Data pushed to Merchant360 successfully.
    /// </summary>
    PushedToMerchant360 = 4,

    /// <summary>
    /// Push to Merchant360 failed.
    /// </summary>
    PushFailed = 5,

    /// <summary>
    /// Cancelled by an operator before processing completed.
    /// </summary>
    Cancelled = 6
}
