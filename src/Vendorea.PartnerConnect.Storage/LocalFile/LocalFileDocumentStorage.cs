using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Storage.Interfaces;
using Vendorea.PartnerConnect.Storage.Models;

namespace Vendorea.PartnerConnect.Storage.LocalFile;

/// <summary>
/// Configuration options for local file storage.
/// </summary>
public class LocalFileStorageOptions
{
    public const string SectionName = "Storage:LocalFile";

    /// <summary>
    /// Base path for document storage.
    /// </summary>
    public string BasePath { get; set; } = "./documents";

    /// <summary>
    /// Whether to create directories automatically.
    /// </summary>
    public bool CreateDirectories { get; set; } = true;
}

/// <summary>
/// Local file system implementation of document storage.
/// Suitable for development and single-server deployments.
/// </summary>
public class LocalFileDocumentStorage : IDocumentStorage
{
    private readonly LocalFileStorageOptions _options;
    private readonly ILogger<LocalFileDocumentStorage> _logger;

    public LocalFileDocumentStorage(
        IOptions<LocalFileStorageOptions> options,
        ILogger<LocalFileDocumentStorage> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.CreateDirectories && !Directory.Exists(_options.BasePath))
        {
            Directory.CreateDirectory(_options.BasePath);
        }
    }

    public async Task<string> StoreAsync(
        Stream content,
        string path,
        StorageMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Compute hash while writing
        using var hashAlgorithm = SHA256.Create();
        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var cryptoStream = new CryptoStream(fileStream, hashAlgorithm, CryptoStreamMode.Write);

        await content.CopyToAsync(cryptoStream, cancellationToken);
        await cryptoStream.FlushFinalBlockAsync(cancellationToken);

        var hash = Convert.ToHexString(hashAlgorithm.Hash!).ToLowerInvariant();

        // Store metadata alongside the file
        var metadataWithHash = metadata with
        {
            ContentHash = hash,
            SizeBytes = fileStream.Length
        };

        await StoreMetadataAsync(fullPath, metadataWithHash, cancellationToken);

        _logger.LogInformation(
            "Stored document at {Path}, Size: {Size} bytes, Hash: {Hash}",
            path, metadataWithHash.SizeBytes, hash);

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
        var fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Document not found at path: {path}", path);
        }

        var memoryStream = new MemoryStream();
        await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public async Task<byte[]> RetrieveBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Document not found at path: {path}", path);
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public async Task<StorageMetadata?> GetMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        var metadataPath = GetMetadataPath(fullPath);

        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        return JsonSerializer.Deserialize<StorageMetadata>(json);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        var metadataPath = GetMetadataPath(fullPath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }

        _logger.LogInformation("Deleted document at {Path}", path);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoredDocument>> ListAsync(
        string prefix,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var basePath = GetFullPath(prefix);
        var results = new List<StoredDocument>();

        if (!Directory.Exists(basePath))
        {
            return Task.FromResult<IReadOnlyList<StoredDocument>>(results);
        }

        var files = Directory.GetFiles(basePath, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".metadata.json"))
            .Take(maxResults);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(_options.BasePath, file);
            var metadataPath = GetMetadataPath(file);

            StorageMetadata? metadata = null;
            if (File.Exists(metadataPath))
            {
                var json = File.ReadAllText(metadataPath);
                metadata = JsonSerializer.Deserialize<StorageMetadata>(json);
            }

            results.Add(new StoredDocument
            {
                Path = relativePath,
                Metadata = metadata ?? new StorageMetadata(),
                Exists = true
            });
        }

        return Task.FromResult<IReadOnlyList<StoredDocument>>(results);
    }

    public async Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var hashAlgorithm = SHA256.Create();
        var hash = await hashAlgorithm.ComputeHashAsync(content, cancellationToken);
        content.Position = 0; // Reset stream position
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string GetFullPath(string path)
    {
        // Normalize path separators and combine with base path
        var normalizedPath = path.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_options.BasePath, normalizedPath);
    }

    private static string GetMetadataPath(string filePath)
    {
        return filePath + ".metadata.json";
    }

    private async Task StoreMetadataAsync(
        string filePath,
        StorageMetadata metadata,
        CancellationToken cancellationToken)
    {
        var metadataPath = GetMetadataPath(filePath);
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken);
    }
}

/// <summary>
/// Factory for creating local file document storage instances.
/// </summary>
public class LocalFileDocumentStorageFactory : IDocumentStorageFactory
{
    private readonly IOptions<LocalFileStorageOptions> _options;
    private readonly ILogger<LocalFileDocumentStorage> _logger;

    public LocalFileDocumentStorageFactory(
        IOptions<LocalFileStorageOptions> options,
        ILogger<LocalFileDocumentStorage> logger)
    {
        _options = options;
        _logger = logger;
    }

    public IDocumentStorage Create()
    {
        return new LocalFileDocumentStorage(_options, _logger);
    }
}
