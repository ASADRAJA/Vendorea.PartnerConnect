using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Transport.Interfaces;

namespace Vendorea.PartnerConnect.Transport.FileSystem;

public sealed class LocalFileTransportClient : IFileTransportClient
{
    private readonly ILogger<LocalFileTransportClient> _logger;
    private string? _basePath;
    private bool _disposed;

    public LocalFileTransportClient(ILogger<LocalFileTransportClient> logger)
    {
        _logger = logger;
    }

    public bool IsConnected => !string.IsNullOrEmpty(_basePath);

    public Task ConnectAsync(TransportConnectionInfo connectionInfo, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _basePath = connectionInfo.Host;

        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }

        _logger.LogInformation("Connected to local file system at {BasePath}", _basePath);

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _basePath = null;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RemoteFileInfo>> ListFilesAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var fullPath = GetFullPath(remotePath);
        var entries = new List<RemoteFileInfo>();

        if (Directory.Exists(fullPath))
        {
            foreach (var file in Directory.GetFiles(fullPath))
            {
                var info = new FileInfo(file);
                entries.Add(new RemoteFileInfo(
                    info.Name,
                    file,
                    info.Length,
                    info.LastWriteTimeUtc,
                    false));
            }

            foreach (var dir in Directory.GetDirectories(fullPath))
            {
                var info = new DirectoryInfo(dir);
                entries.Add(new RemoteFileInfo(
                    info.Name,
                    dir,
                    0,
                    info.LastWriteTimeUtc,
                    true));
            }
        }

        return Task.FromResult<IReadOnlyList<RemoteFileInfo>>(entries);
    }

    public Task<Stream> DownloadFileAsync(string remoteFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var fullPath = GetFullPath(remoteFilePath);
        var memoryStream = new MemoryStream();

        using (var fileStream = File.OpenRead(fullPath))
        {
            fileStream.CopyTo(memoryStream);
        }

        memoryStream.Position = 0;

        return Task.FromResult<Stream>(memoryStream);
    }

    public Task DownloadFileAsync(string remoteFilePath, string localFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var fullPath = GetFullPath(remoteFilePath);
        var directory = Path.GetDirectoryName(localFilePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(fullPath, localFilePath, overwrite: true);

        _logger.LogDebug("Downloaded {RemotePath} to {LocalPath}", remoteFilePath, localFilePath);

        return Task.CompletedTask;
    }

    public Task UploadFileAsync(string localFilePath, string remoteFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var fullPath = GetFullPath(remoteFilePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(localFilePath, fullPath, overwrite: true);

        _logger.LogDebug("Uploaded {LocalPath} to {RemotePath}", localFilePath, remoteFilePath);

        return Task.CompletedTask;
    }

    public Task UploadFileAsync(Stream content, string remoteFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var fullPath = GetFullPath(remoteFilePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var fileStream = File.Create(fullPath))
        {
            content.CopyTo(fileStream);
        }

        _logger.LogDebug("Uploaded stream to {RemotePath}", remoteFilePath);

        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(string remoteFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var fullPath = GetFullPath(remoteFilePath);
        File.Delete(fullPath);

        _logger.LogDebug("Deleted {RemotePath}", remoteFilePath);

        return Task.CompletedTask;
    }

    public Task MoveFileAsync(string sourceRemotePath, string destinationRemotePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var sourcePath = GetFullPath(sourceRemotePath);
        var destinationPath = GetFullPath(destinationRemotePath);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Move(sourcePath, destinationPath, overwrite: true);

        _logger.LogDebug("Moved {SourcePath} to {DestinationPath}", sourceRemotePath, destinationRemotePath);

        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string remoteFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var fullPath = GetFullPath(remoteFilePath);
        var exists = File.Exists(fullPath);

        return Task.FromResult(exists);
    }

    public Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var fullPath = GetFullPath(remotePath);
        Directory.CreateDirectory(fullPath);

        _logger.LogDebug("Created directory {RemotePath}", remotePath);

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _basePath = null;
        return ValueTask.CompletedTask;
    }

    private void EnsureConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
        }
    }

    private string GetFullPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }

        return Path.Combine(_basePath!, relativePath);
    }
}
