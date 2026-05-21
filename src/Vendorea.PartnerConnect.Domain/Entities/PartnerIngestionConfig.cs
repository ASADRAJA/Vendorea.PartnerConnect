namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Stores ingestion configuration for a trading partner.
/// </summary>
public class PartnerIngestionConfig
{
    public int Id { get; set; }

    /// <summary>
    /// Partner code (e.g., "SPR", "UNI").
    /// </summary>
    public string PartnerCode { get; set; } = string.Empty;

    /// <summary>
    /// FTP server hostname.
    /// </summary>
    public string FtpHost { get; set; } = string.Empty;

    /// <summary>
    /// FTP server port.
    /// </summary>
    public int FtpPort { get; set; } = 21;

    /// <summary>
    /// FTP username.
    /// </summary>
    public string FtpUsername { get; set; } = string.Empty;

    /// <summary>
    /// FTP password (encrypted in production).
    /// </summary>
    public string FtpPassword { get; set; } = string.Empty;

    /// <summary>
    /// Local download path (for local storage mode).
    /// </summary>
    public string LocalDownloadPath { get; set; } = string.Empty;

    /// <summary>
    /// Locale code (e.g., "EN_US").
    /// </summary>
    public string Locale { get; set; } = "EN_US";

    /// <summary>
    /// Database type (mssql, oracle).
    /// </summary>
    public string DatabaseType { get; set; } = "mssql";

    /// <summary>
    /// Whether ingestion is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Whether scheduled runs are enabled.
    /// </summary>
    public bool EnableScheduledRun { get; set; }

    /// <summary>
    /// Hour (UTC) for scheduled runs.
    /// </summary>
    public int ScheduledRunHourUtc { get; set; } = 2;

    /// <summary>
    /// Interval in minutes between schedule checks.
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Batch size for bulk insert operations.
    /// </summary>
    public int BulkInsertBatchSize { get; set; } = 10000;

    /// <summary>
    /// Whether to cleanup files after import.
    /// </summary>
    public bool CleanupAfterImport { get; set; } = true;

    /// <summary>
    /// Whether to use Azure Blob Storage.
    /// </summary>
    public bool UseAzureBlobStorage { get; set; }

    /// <summary>
    /// Azure Blob connection string.
    /// </summary>
    public string? AzureBlobConnectionString { get; set; }

    /// <summary>
    /// Azure Blob container name.
    /// </summary>
    public string? AzureBlobContainerName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
