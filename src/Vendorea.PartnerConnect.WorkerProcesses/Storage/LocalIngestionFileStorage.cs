using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;

namespace Vendorea.PartnerConnect.WorkerProcesses.Storage;

/// <summary>
/// Local file system implementation of ingestion file storage.
/// </summary>
public class LocalIngestionFileStorage : IIngestionFileStorage
{
    private readonly ILogger<LocalIngestionFileStorage> _logger;
    private readonly string _basePath;

    public LocalIngestionFileStorage(
        ILogger<LocalIngestionFileStorage> logger,
        IOptions<SprContentIngestionOptions> options)
    {
        _logger = logger;
        _basePath = options.Value.LocalDownloadPath;

        // Ensure base directory exists
        Directory.CreateDirectory(_basePath);
    }

    public StorageType StorageType => StorageType.Local;

    public async Task<string> SaveFileAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(fileName);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, cancellationToken);

        _logger.LogDebug("Saved file to local storage: {Path}", fullPath);
        return fullPath;
    }

    public Task<Stream> OpenReadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(fileName);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {fileName}", fullPath);
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream>(stream);
    }

    public Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(fileName);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogDebug("Deleted file from local storage: {Path}", fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(fileName);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<IReadOnlyList<string>> ListFilesAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var searchPath = string.IsNullOrEmpty(prefix)
            ? _basePath
            : Path.Combine(_basePath, prefix);

        var searchPattern = "*.*";
        var searchDir = _basePath;

        if (!string.IsNullOrEmpty(prefix))
        {
            var prefixDir = Path.GetDirectoryName(prefix);
            if (!string.IsNullOrEmpty(prefixDir))
            {
                searchDir = Path.Combine(_basePath, prefixDir);
            }

            var prefixFile = Path.GetFileName(prefix);
            if (!string.IsNullOrEmpty(prefixFile))
            {
                searchPattern = prefixFile + "*";
            }
        }

        if (!Directory.Exists(searchDir))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var files = Directory.GetFiles(searchDir, searchPattern, SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_basePath, f))
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public async Task DeleteAllAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var files = await ListFilesAsync(prefix, cancellationToken);

        foreach (var file in files)
        {
            await DeleteFileAsync(file, cancellationToken);
        }

        _logger.LogInformation("Deleted {Count} files from local storage", files.Count);
    }

    public Task<string> GetLocalPathAsync(string fileName, CancellationToken cancellationToken = default)
    {
        // For local storage, just return the full path
        return Task.FromResult(GetFullPath(fileName));
    }

    private string GetFullPath(string fileName)
    {
        // Normalize path separators and combine with base path
        var normalizedFileName = fileName.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_basePath, normalizedFileName);
    }
}
