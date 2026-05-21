namespace Vendorea.PartnerConnect.WorkerProcesses.Configuration;

/// <summary>
/// Configuration options for SPR content ingestion from Etilize FTP.
/// </summary>
public class SprContentIngestionOptions
{
    public const string SectionName = "SprContentIngestion";

    /// <summary>
    /// FTP server hostname.
    /// </summary>
    public string FtpHost { get; set; } = "ftp.etilize.com";

    /// <summary>
    /// FTP username.
    /// </summary>
    public string FtpUsername { get; set; } = string.Empty;

    /// <summary>
    /// FTP password.
    /// </summary>
    public string FtpPassword { get; set; } = string.Empty;

    /// <summary>
    /// Base path on the FTP server.
    /// </summary>
    public string BasePath { get; set; } = "/";

    /// <summary>
    /// Local directory to download files to (used when UseAzureBlobStorage is false).
    /// Defaults to system temp directory.
    /// </summary>
    public string LocalDownloadPath { get; set; } = Path.Combine(Path.GetTempPath(), "spr-inquire");

    /// <summary>
    /// Whether to use Azure Blob Storage for downloaded files.
    /// When true, files are stored in Azure Blob Storage instead of local file system.
    /// Recommended for production Azure deployments.
    /// </summary>
    public bool UseAzureBlobStorage { get; set; } = false;

    /// <summary>
    /// Azure Blob Storage connection string.
    /// Required when UseAzureBlobStorage is true.
    /// </summary>
    public string? AzureBlobConnectionString { get; set; }

    /// <summary>
    /// Azure Blob Storage container name for ingestion files.
    /// Required when UseAzureBlobStorage is true.
    /// </summary>
    public string AzureBlobContainerName { get; set; } = "spr-content-ingestion";

    /// <summary>
    /// Whether to download accessory relationships.
    /// </summary>
    public bool DownloadAccessories { get; set; } = true;

    /// <summary>
    /// Whether to download upsell relationships.
    /// </summary>
    public bool DownloadUpsell { get; set; } = true;

    /// <summary>
    /// Whether to download similar/cross-sell relationships.
    /// </summary>
    public bool DownloadSimilar { get; set; } = true;

    /// <summary>
    /// Whether to download detailed attributes.
    /// </summary>
    public bool DownloadDetailedAttributes { get; set; } = true;

    /// <summary>
    /// Whether to download feature bullets.
    /// </summary>
    public bool DownloadFeatureBullets { get; set; } = true;

    /// <summary>
    /// Whether to download product resources (MSDS, Rebates).
    /// </summary>
    public bool DownloadProductResources { get; set; } = true;

    /// <summary>
    /// Whether to download features and benefits extras file.
    /// Note: This file may have data quality issues with missing Type values.
    /// </summary>
    public bool DownloadFeaturesAndBenefits { get; set; } = false;

    /// <summary>
    /// Whether to download also-bought extras file.
    /// Note: This file may have data quality issues with missing values.
    /// </summary>
    public bool DownloadAlsoBought { get; set; } = false;

    /// <summary>
    /// Locale to download (e.g., "EN_US").
    /// </summary>
    public string Locale { get; set; } = "EN_US";

    /// <summary>
    /// Database type suffix (mysql, mssql, oracle).
    /// </summary>
    public string DatabaseType { get; set; } = "mssql";

    /// <summary>
    /// Whether the worker is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Cron expression for scheduling (default: 2 AM daily).
    /// </summary>
    public string CronSchedule { get; set; } = "0 2 * * *";

    /// <summary>
    /// Batch size for bulk insert operations.
    /// </summary>
    public int BulkInsertBatchSize { get; set; } = 10000;

    /// <summary>
    /// Initial delay in seconds before first run.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Interval in minutes between schedule checks.
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to cleanup downloaded files after import.
    /// </summary>
    public bool CleanupAfterImport { get; set; } = true;

    /// <summary>
    /// Whether to transform specifications (generates HTML spec tables).
    /// This is the slowest transformation step and can be skipped for faster imports.
    /// </summary>
    public bool TransformSpecifications { get; set; } = true;

    /// <summary>
    /// Whether scheduled run is enabled.
    /// </summary>
    public bool EnableScheduledRun { get; set; } = true;

    /// <summary>
    /// Hour of day (UTC) to run the scheduled import.
    /// </summary>
    public int ScheduledRunHourUtc { get; set; } = 2;
}
