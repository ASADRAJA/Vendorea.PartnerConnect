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
    /// Warehouse code mappings (SPR code -> internal code).
    /// </summary>
    public Dictionary<string, string>? WarehouseCodeMappings { get; set; }

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
