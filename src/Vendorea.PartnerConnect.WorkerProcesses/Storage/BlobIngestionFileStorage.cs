using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;

namespace Vendorea.PartnerConnect.WorkerProcesses.Storage;

/// <summary>
/// Azure Blob Storage implementation of ingestion file storage.
/// </summary>
public class BlobIngestionFileStorage : IIngestionFileStorage
{
    private readonly ILogger<BlobIngestionFileStorage> _logger;
    private readonly BlobContainerClient _containerClient;
    private readonly string _tempPath;

    public BlobIngestionFileStorage(
        ILogger<BlobIngestionFileStorage> logger,
        IOptions<SprContentIngestionOptions> options)
    {
        _logger = logger;

        var connectionString = options.Value.AzureBlobConnectionString;
        var containerName = options.Value.AzureBlobContainerName;

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("AzureBlobConnectionString is required when using Azure Blob Storage");
        }

        if (string.IsNullOrEmpty(containerName))
        {
            throw new InvalidOperationException("AzureBlobContainerName is required when using Azure Blob Storage");
        }

        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        // Ensure container exists
        _containerClient.CreateIfNotExists();

        // Use system temp path for local caching
        _tempPath = Path.Combine(Path.GetTempPath(), "spr-ingestion-cache");
        Directory.CreateDirectory(_tempPath);

        _logger.LogInformation("Initialized Azure Blob Storage: {Container}", containerName);
    }

    public StorageType StorageType => StorageType.AzureBlob;

    public async Task<string> SaveFileAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var blobName = NormalizeBlobName(fileName);
        var blobClient = _containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

        _logger.LogDebug("Uploaded file to blob storage: {BlobName}", blobName);
        return blobClient.Uri.ToString();
    }

    public async Task<Stream> OpenReadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var blobName = NormalizeBlobName(fileName);
        var blobClient = _containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            throw new FileNotFoundException($"Blob not found: {blobName}");
        }

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public async Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var blobName = NormalizeBlobName(fileName);
        var blobClient = _containerClient.GetBlobClient(blobName);

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        _logger.LogDebug("Deleted blob: {BlobName}", blobName);

        // Also delete local cached copy if exists
        var localPath = GetLocalCachePath(fileName);
        if (File.Exists(localPath))
        {
            File.Delete(localPath);
        }
    }

    public async Task<bool> FileExistsAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var blobName = NormalizeBlobName(fileName);
        var blobClient = _containerClient.GetBlobClient(blobName);

        return await blobClient.ExistsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var normalizedPrefix = prefix != null ? NormalizeBlobName(prefix) : null;
        var files = new List<string>();

        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: normalizedPrefix, cancellationToken: cancellationToken))
        {
            files.Add(blobItem.Name);
        }

        return files;
    }

    public async Task DeleteAllAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var files = await ListFilesAsync(prefix, cancellationToken);
        var deleteCount = 0;

        foreach (var file in files)
        {
            var blobClient = _containerClient.GetBlobClient(file);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            deleteCount++;
        }

        // Clean up local cache
        if (Directory.Exists(_tempPath))
        {
            foreach (var file in Directory.GetFiles(_tempPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        _logger.LogInformation("Deleted {Count} blobs from storage", deleteCount);
    }

    public async Task<string> GetLocalPathAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var localPath = GetLocalCachePath(fileName);

        // Check if already cached locally
        if (File.Exists(localPath))
        {
            return localPath;
        }

        // Download from blob to local cache
        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var blobName = NormalizeBlobName(fileName);
        var blobClient = _containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            throw new FileNotFoundException($"Blob not found: {blobName}");
        }

        await blobClient.DownloadToAsync(localPath, cancellationToken);
        _logger.LogDebug("Downloaded blob to local cache: {BlobName} -> {LocalPath}", blobName, localPath);

        return localPath;
    }

    private string NormalizeBlobName(string fileName)
    {
        // Azure Blob Storage uses forward slashes
        return fileName.Replace('\\', '/').TrimStart('/');
    }

    private string GetLocalCachePath(string fileName)
    {
        var normalizedFileName = fileName.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_tempPath, normalizedFileName);
    }
}
