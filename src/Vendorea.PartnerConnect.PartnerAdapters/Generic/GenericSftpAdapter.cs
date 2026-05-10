using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.PartnerAdapters.Common;
using Vendorea.PartnerConnect.Storage.Interfaces;
using Vendorea.PartnerConnect.Transport.Interfaces;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.PartnerAdapters.Generic;

/// <summary>
/// Configuration for the generic SFTP adapter.
/// </summary>
public class GenericSftpAdapterConfiguration
{
    /// <summary>
    /// The directory path on the SFTP server to monitor for incoming files.
    /// </summary>
    public string InboundPath { get; set; } = "/inbound";

    /// <summary>
    /// The directory path for processed files (archive).
    /// </summary>
    public string ArchivePath { get; set; } = "/archive";

    /// <summary>
    /// The directory path for outbound files.
    /// </summary>
    public string OutboundPath { get; set; } = "/outbound";

    /// <summary>
    /// File pattern to match (e.g., "*.csv", "*.edi").
    /// </summary>
    public string FilePattern { get; set; } = "*.*";

    /// <summary>
    /// Whether to move files to archive after processing.
    /// </summary>
    public bool ArchiveAfterProcessing { get; set; } = true;

    /// <summary>
    /// Whether to delete files after processing (use with caution).
    /// </summary>
    public bool DeleteAfterProcessing { get; set; } = false;

    /// <summary>
    /// Content type of the files (e.g., "text/csv", "application/edi-x12").
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// Document type for categorization.
    /// </summary>
    public DocumentType DocumentType { get; set; } = DocumentType.PriceList;

    /// <summary>
    /// File encoding (e.g., "UTF-8", "ASCII").
    /// </summary>
    public string FileEncoding { get; set; } = "UTF-8";
}

/// <summary>
/// Generic SFTP adapter that can be configured for different partners.
/// </summary>
public class GenericSftpAdapter : BasePartnerAdapter
{
    private readonly IFileTransportClientFactory _transportFactory;
    private readonly IDocumentStorage _documentStorage;
    private readonly IDuplicateDetectionService _duplicateDetection;
    private GenericSftpAdapterConfiguration _config = new();

    public GenericSftpAdapter(
        IFileTransportClientFactory transportFactory,
        IDocumentStorage documentStorage,
        IDuplicateDetectionService duplicateDetection,
        ILogger<GenericSftpAdapter> logger) : base(logger)
    {
        _transportFactory = transportFactory;
        _documentStorage = documentStorage;
        _duplicateDetection = duplicateDetection;
    }

    public override string PartnerCode => "GENERIC_SFTP";

    public override IReadOnlyList<PartnerCapability> SupportedCapabilities =>
        new List<PartnerCapability> { PartnerCapability.PriceFeed, PartnerCapability.InventoryFeed };

    /// <summary>
    /// Configures the adapter with specific settings.
    /// </summary>
    public void Configure(GenericSftpAdapterConfiguration configuration)
    {
        _config = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<IReadOnlyList<PartnerDocument>> FetchDocumentsAsync(
        DealerPartnerConnection connection,
        CancellationToken cancellationToken = default)
    {
        var documents = new List<PartnerDocument>();

        try
        {
            LogInfo("Fetching documents for connection {ConnectionId}", connection.Id);

            await using var client = _transportFactory.CreateSftpClient();
            var connectionInfo = ParseConnectionInfo(connection.CredentialsJson);
            await client.ConnectAsync(connectionInfo, cancellationToken);

            // List files matching the pattern
            var files = await client.ListFilesAsync(_config.InboundPath, cancellationToken);
            var matchingFiles = FilterFilesByPattern(files, _config.FilePattern);

            LogInfo("Found {FileCount} matching files", matchingFiles.Count);

            foreach (var file in matchingFiles)
            {
                try
                {
                    var document = await ProcessFileAsync(connection, client, file, cancellationToken);
                    if (document != null)
                    {
                        documents.Add(document);
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex, "Error processing file {FileName}", file.Name);
                }
            }

            return documents;
        }
        catch (Exception ex)
        {
            LogError(ex, "Error fetching documents for connection {ConnectionId}", connection.Id);
            throw;
        }
    }

    private async Task<PartnerDocument?> ProcessFileAsync(
        DealerPartnerConnection connection,
        IFileTransportClient client,
        RemoteFileInfo file,
        CancellationToken cancellationToken)
    {
        // Download file content
        var filePath = $"{_config.InboundPath}/{file.Name}";
        using var stream = await client.DownloadFileAsync(filePath, cancellationToken);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        var content = memoryStream.ToArray();

        // Check for duplicates
        var contentHash = _duplicateDetection.ComputeHash(content);
        if (await _duplicateDetection.IsDuplicateAsync(
            connection.Id,
            _config.DocumentType,
            contentHash,
            cancellationToken))
        {
            LogInfo("Skipping duplicate file {FileName}", file.Name);

            // Archive or delete the duplicate
            await HandleProcessedFileAsync(client, file, cancellationToken);
            return null;
        }

        // Store raw document
        var storagePath = $"raw/{connection.DealerId}/{_config.DocumentType}/{DateTime.UtcNow:yyyyMMdd}/{file.Name}";
        var storageKey = await _documentStorage.StoreAsync(
            new MemoryStream(content),
            storagePath,
            new Storage.Models.StorageMetadata
            {
                OriginalFileName = file.Name,
                ContentType = _config.ContentType,
                DealerId = connection.DealerId,
                TradingPartnerCode = connection.TradingPartner?.Code,
                DocumentType = _config.DocumentType.ToString(),
                SizeBytes = content.Length,
                ContentHash = contentHash
            },
            cancellationToken);

        // Create document record
        var document = new PartnerDocument
        {
            DealerPartnerConnectionId = connection.Id,
            DocumentType = _config.DocumentType,
            Direction = DocumentDirection.Inbound,
            FileName = file.Name,
            ContentHash = contentHash,
            FileSizeBytes = content.Length,
            StoragePath = storageKey,
            ReceivedAt = DateTime.UtcNow
        };

        // Register fingerprint
        await _duplicateDetection.RegisterFingerprintAsync(
            connection.Id,
            _config.DocumentType,
            contentHash,
            document.Id,
            file.Name,
            content.Length,
            null,
            cancellationToken);

        // Handle processed file
        await HandleProcessedFileAsync(client, file, cancellationToken);

        LogInfo("Successfully processed file {FileName}", file.Name);
        return document;
    }

    private async Task HandleProcessedFileAsync(
        IFileTransportClient client,
        RemoteFileInfo file,
        CancellationToken cancellationToken)
    {
        var sourcePath = $"{_config.InboundPath}/{file.Name}";

        if (_config.ArchiveAfterProcessing && !_config.DeleteAfterProcessing)
        {
            var archivePath = $"{_config.ArchivePath}/{DateTime.UtcNow:yyyyMMdd}/{file.Name}";
            try
            {
                await client.MoveFileAsync(sourcePath, archivePath, cancellationToken);
            }
            catch (Exception ex)
            {
                LogWarning("Failed to archive file {FileName}: {Error}", file.Name, ex.Message);
            }
        }
        else if (_config.DeleteAfterProcessing)
        {
            try
            {
                await client.DeleteFileAsync(sourcePath, cancellationToken);
            }
            catch (Exception ex)
            {
                LogWarning("Failed to delete file {FileName}: {Error}", file.Name, ex.Message);
            }
        }
    }

    public async Task<bool> SendDocumentAsync(
        DealerPartnerConnection connection,
        PartnerDocument document,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var client = _transportFactory.CreateSftpClient();
            var connectionInfo = ParseConnectionInfo(connection.CredentialsJson);
            await client.ConnectAsync(connectionInfo, cancellationToken);

            var fileName = document.FileName ?? $"{document.DocumentType}_{DateTime.UtcNow:yyyyMMddHHmmss}.dat";
            var remotePath = $"{_config.OutboundPath}/{fileName}";

            await client.UploadFileAsync(content, remotePath, cancellationToken);

            LogInfo("Successfully sent document {DocumentId} to {Path}", document.Id, remotePath);
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, "Error sending document {DocumentId}", document.Id);
            return false;
        }
    }

    public override async Task<bool> TestConnectionAsync(
        DealerPartnerConnection connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var client = _transportFactory.CreateSftpClient();
            var connectionInfo = ParseConnectionInfo(connection.CredentialsJson);
            await client.ConnectAsync(connectionInfo, cancellationToken);

            // Try to list the inbound directory
            await client.ListFilesAsync(_config.InboundPath, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, "Connection test failed for {ConnectionId}", connection.Id);
            return false;
        }
    }

    private static TransportConnectionInfo ParseConnectionInfo(string? credentialsJson)
    {
        if (string.IsNullOrEmpty(credentialsJson))
        {
            throw new InvalidOperationException("No credentials configured for connection");
        }

        var creds = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(credentialsJson);
        if (creds == null)
        {
            throw new InvalidOperationException("Invalid credentials JSON");
        }

        return new TransportConnectionInfo(
            Host: GetStringValue(creds, "host") ?? throw new InvalidOperationException("Host is required"),
            Port: GetIntValue(creds, "port") ?? 22,
            Username: GetStringValue(creds, "username") ?? throw new InvalidOperationException("Username is required"),
            Password: GetStringValue(creds, "password"),
            PrivateKeyPath: GetStringValue(creds, "privateKeyPath"),
            PrivateKeyPassphrase: GetStringValue(creds, "privateKeyPassphrase")
        );
    }

    private static string? GetStringValue(Dictionary<string, JsonElement> dict, string key)
    {
        return dict.TryGetValue(key, out var value) ? value.GetString() : null;
    }

    private static int? GetIntValue(Dictionary<string, JsonElement> dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetInt32();
            }
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var result))
            {
                return result;
            }
        }
        return null;
    }

    private static IReadOnlyList<RemoteFileInfo> FilterFilesByPattern(
        IEnumerable<RemoteFileInfo> files,
        string pattern)
    {
        if (pattern == "*.*" || pattern == "*")
        {
            return files.Where(f => !f.IsDirectory).ToList();
        }

        var extension = pattern.Replace("*", "").ToLowerInvariant();
        return files
            .Where(f => !f.IsDirectory && f.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
