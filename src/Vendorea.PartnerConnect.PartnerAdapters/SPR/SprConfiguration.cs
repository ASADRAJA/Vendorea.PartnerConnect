using System.Text.Json;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR;

/// <summary>
/// Configuration for SPR partner connections.
/// Stored as JSON in DealerPartnerConnection.ConfigurationJson.
/// </summary>
public class SprConfiguration
{
    /// <summary>
    /// SFTP host address.
    /// </summary>
    public string SftpHost { get; set; } = string.Empty;

    /// <summary>
    /// SFTP port (default 22).
    /// </summary>
    public int SftpPort { get; set; } = 22;

    /// <summary>
    /// SFTP username.
    /// </summary>
    public string SftpUsername { get; set; } = string.Empty;

    /// <summary>
    /// Path to the price feed directory on the SFTP server.
    /// </summary>
    public string PriceFeedPath { get; set; } = "/outbound/prices";

    /// <summary>
    /// Path to the inventory feed directory on the SFTP server.
    /// </summary>
    public string InventoryFeedPath { get; set; } = "/outbound/inventory";

    /// <summary>
    /// Path to archive processed files (null = don't archive).
    /// </summary>
    public string? ArchivePath { get; set; } = "/archive";

    /// <summary>
    /// File pattern for price feed files.
    /// </summary>
    public string PriceFeedFilePattern { get; set; } = "*.csv";

    /// <summary>
    /// File pattern for inventory feed files.
    /// </summary>
    public string InventoryFeedFilePattern { get; set; } = "*.csv";

    /// <summary>
    /// Whether to delete files after processing.
    /// </summary>
    public bool DeleteAfterProcessing { get; set; } = false;

    /// <summary>
    /// Whether to move files to archive after processing.
    /// </summary>
    public bool ArchiveAfterProcessing { get; set; } = true;

    /// <summary>
    /// CSV delimiter character.
    /// </summary>
    public char CsvDelimiter { get; set; } = ',';

    /// <summary>
    /// Whether CSV files have a header row.
    /// </summary>
    public bool CsvHasHeader { get; set; } = true;

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// SPR-specific customer number for this dealer.
    /// </summary>
    public string? SprCustomerNumber { get; set; }

    /// <summary>
    /// Pricing tier for this dealer (determines which cost column to use).
    /// </summary>
    public SprPricingTier PricingTier { get; set; } = SprPricingTier.Standard;

    /// <summary>
    /// Warehouse code mappings (SPR code -> internal code).
    /// </summary>
    public Dictionary<string, string>? WarehouseCodeMappings { get; set; }

    /// <summary>
    /// Category code mappings (SPR code -> internal code).
    /// </summary>
    public Dictionary<string, string>? CategoryCodeMappings { get; set; }

    // EDI Configuration

    /// <summary>
    /// Path to inbound EDI documents on the SFTP server.
    /// </summary>
    public string EdiInboundPath { get; set; } = "/edi/inbound";

    /// <summary>
    /// Path to outbound EDI documents on the SFTP server.
    /// </summary>
    public string EdiOutboundPath { get; set; } = "/edi/outbound";

    /// <summary>
    /// Path to archive processed EDI documents.
    /// </summary>
    public string EdiArchivePath { get; set; } = "/edi/archive";

    /// <summary>
    /// File patterns for EDI files (semicolon-separated).
    /// </summary>
    public string EdiFilePattern { get; set; } = "*.edi;*.x12;*.txt";

    /// <summary>
    /// Whether to automatically send 997 Functional Acknowledgments.
    /// </summary>
    public bool AutoSend997 { get; set; } = true;

    /// <summary>
    /// Whether to automatically send 855 PO Acknowledgments.
    /// </summary>
    public bool AutoSend855 { get; set; } = true;

    /// <summary>
    /// Interval in minutes for EDI document sync worker.
    /// </summary>
    public int EdiSyncIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// ISA sender ID qualifier (ISA05) for outbound documents.
    /// </summary>
    public string IsaSenderQualifier { get; set; } = "ZZ";

    /// <summary>
    /// ISA sender ID (ISA06) for outbound documents.
    /// </summary>
    public string IsaSenderId { get; set; } = string.Empty;

    /// <summary>
    /// ISA receiver ID qualifier (ISA07) for outbound documents.
    /// </summary>
    public string IsaReceiverQualifier { get; set; } = "ZZ";

    /// <summary>
    /// ISA receiver ID (ISA08) for outbound documents.
    /// </summary>
    public string IsaReceiverId { get; set; } = string.Empty;

    /// <summary>
    /// GS application sender code for outbound documents.
    /// </summary>
    public string GsApplicationSenderCode { get; set; } = string.Empty;

    /// <summary>
    /// GS application receiver code for outbound documents.
    /// </summary>
    public string GsApplicationReceiverCode { get; set; } = string.Empty;

    /// <summary>
    /// Deserializes configuration from JSON.
    /// </summary>
    public static SprConfiguration FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SprConfiguration();
        }

        try
        {
            return JsonSerializer.Deserialize<SprConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new SprConfiguration();
        }
        catch
        {
            return new SprConfiguration();
        }
    }

    /// <summary>
    /// Serializes configuration to JSON.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}

/// <summary>
/// Credentials for SPR connections.
/// Stored as JSON in DealerPartnerConnection.CredentialsJson.
/// </summary>
public class SprCredentials
{
    /// <summary>
    /// SFTP password (if using password auth).
    /// </summary>
    public string? SftpPassword { get; set; }

    /// <summary>
    /// Path to private key file (if using key auth).
    /// </summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    /// Passphrase for private key.
    /// </summary>
    public string? PrivateKeyPassphrase { get; set; }

    /// <summary>
    /// Deserializes credentials from JSON.
    /// </summary>
    public static SprCredentials FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SprCredentials();
        }

        try
        {
            return JsonSerializer.Deserialize<SprCredentials>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new SprCredentials();
        }
        catch
        {
            return new SprCredentials();
        }
    }
}

/// <summary>
/// SPR pricing tier determines which dealer cost column to use from the price file.
/// </summary>
public enum SprPricingTier
{
    /// <summary>
    /// Standard pricing (Net Cost for non-CCP Dealers - Column 78)
    /// </summary>
    Standard,

    /// <summary>
    /// CCP-3 program pricing (Column 79)
    /// </summary>
    Ccp3,

    /// <summary>
    /// CCP-4 program pricing (Column 80)
    /// </summary>
    Ccp4
}
