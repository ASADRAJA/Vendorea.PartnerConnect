using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Storage.Interfaces;
using Vendorea.PartnerConnect.Storage.Models;

namespace Vendorea.PartnerConnect.Storage.AzureBlob;

/// <summary>
/// Configuration options for Azure Blob storage.
/// </summary>
public class AzureBlobStorageOptions
{
    public const string SectionName = "Storage:AzureBlob";

    /// <summary>
    /// Azure Storage connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Container name for document storage.
    /// </summary>
    public string ContainerName { get; set; } = "partner-documents";

    /// <summary>
    /// Whether to create the container if it doesn't exist.
    /// </summary>
    public bool CreateContainerIfNotExists { get; set; } = true;
}

/// <summary>
/// Azure Blob Storage implementation of document storage. Metadata is persisted as a sibling
/// "{path}.metadata.json" blob, mirroring the local file provider so List/GetMetadata behave the same.
/// </summary>
public class AzureBlobDocumentStorage : IDocumentStorage
{
    private const string MetadataSuffix = ".metadata.json";

    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobDocumentStorage> _logger;

    public AzureBlobDocumentStorage(
        IOptions<AzureBlobStorageOptions> options,
        ILogger<AzureBlobDocumentStorage> logger)
    {
        var opts = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            throw new InvalidOperationException(
                "AzureBlobStorageOptions.ConnectionString is required (Storage:AzureBlob:ConnectionString).");
        }

        _container = new BlobContainerClient(opts.ConnectionString, opts.ContainerName);
        if (opts.CreateContainerIfNotExists)
        {
            _container.CreateIfNotExists(PublicAccessType.None);
        }
    }

    public async Task<string> StoreAsync(
        Stream content,
        string path,
        StorageMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        // Buffer so we can hash and upload from the same content (blobs need a seekable/forward stream).
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var sizeBytes = buffer.Length;
        buffer.Position = 0;

        var hash = Convert.ToHexString(await SHA256.HashDataAsync(buffer, cancellationToken)).ToLowerInvariant();
        buffer.Position = 0;

        var blob = _container.GetBlobClient(GetBlobName(path));
        var headers = new BlobHttpHeaders { ContentType = metadata.ContentType ?? "application/octet-stream" };
        await blob.UploadAsync(buffer, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);

        var metadataWithHash = metadata with { ContentHash = hash, SizeBytes = sizeBytes };
        await StoreMetadataAsync(path, metadataWithHash, cancellationToken);

        _logger.LogInformation(
            "Stored document at {Path}, Size: {Size} bytes, Hash: {Hash}", path, sizeBytes, hash);

        return path;
    }

    public async Task<string> StoreAsync(
        byte[] content,
        string path,
        StorageMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        return await StoreAsync(stream, path, metadata, cancellationToken);
    }

    public async Task<Stream> RetrieveAsync(string path, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(GetBlobName(path));
        try
        {
            var memoryStream = new MemoryStream();
            await blob.DownloadToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"Document not found at path: {path}", path);
        }
    }

    public async Task<byte[]> RetrieveBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = await RetrieveAsync(path, cancellationToken);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    public async Task<StorageMetadata?> GetMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(GetMetadataBlobName(path));
        try
        {
            var response = await blob.DownloadContentAsync(cancellationToken);
            return JsonSerializer.Deserialize<StorageMetadata>(response.Value.Content.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(GetBlobName(path));
        return await blob.ExistsAsync(cancellationToken);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        await _container.GetBlobClient(GetBlobName(path)).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        await _container.GetBlobClient(GetMetadataBlobName(path)).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Deleted document at {Path}", path);
    }

    public async Task<IReadOnlyList<StoredDocument>> ListAsync(
        string prefix,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<StoredDocument>();
        var blobPrefix = GetBlobName(prefix);

        await foreach (var item in _container.GetBlobsAsync(prefix: blobPrefix, cancellationToken: cancellationToken))
        {
            if (item.Name.EndsWith(MetadataSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var metadata = await GetMetadataAsync(item.Name, cancellationToken);
            results.Add(new StoredDocument
            {
                Path = item.Name,
                Metadata = metadata ?? new StorageMetadata(),
                Exists = true
            });

            if (results.Count >= maxResults)
            {
                break;
            }
        }

        return results;
    }

    public async Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        content.Position = 0; // Reset stream position
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task StoreMetadataAsync(string path, StorageMetadata metadata, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        var blob = _container.GetBlobClient(GetMetadataBlobName(path));
        var headers = new BlobHttpHeaders { ContentType = "application/json" };
        await blob.UploadAsync(BinaryData.FromString(json), new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);
    }

    private static string GetBlobName(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string GetMetadataBlobName(string path) => GetBlobName(path) + MetadataSuffix;
}

/// <summary>
/// Factory for creating Azure Blob document storage instances.
/// </summary>
public class AzureBlobDocumentStorageFactory : IDocumentStorageFactory
{
    private readonly IOptions<AzureBlobStorageOptions> _options;
    private readonly ILogger<AzureBlobDocumentStorage> _logger;

    public AzureBlobDocumentStorageFactory(
        IOptions<AzureBlobStorageOptions> options,
        ILogger<AzureBlobDocumentStorage> logger)
    {
        _options = options;
        _logger = logger;
    }

    public IDocumentStorage Create() => new AzureBlobDocumentStorage(_options, _logger);
}
