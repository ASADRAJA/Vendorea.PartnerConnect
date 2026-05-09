using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Vendorea.PartnerConnect.Transport.Interfaces;

namespace Vendorea.PartnerConnect.Transport.Sftp;

public sealed class SftpFileTransportClient : IFileTransportClient
{
    private readonly ILogger<SftpFileTransportClient> _logger;
    private SftpClient? _client;
    private bool _disposed;

    public SftpFileTransportClient(ILogger<SftpFileTransportClient> logger)
    {
        _logger = logger;
    }

    public bool IsConnected => _client?.IsConnected ?? false;

    public Task ConnectAsync(TransportConnectionInfo connectionInfo, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var connectionMethods = BuildAuthenticationMethods(connectionInfo);
        var connectionInfo2 = new ConnectionInfo(
            connectionInfo.Host,
            connectionInfo.Port,
            connectionInfo.Username,
            connectionMethods);

        if (connectionInfo.ConnectionTimeout.HasValue)
        {
            connectionInfo2.Timeout = connectionInfo.ConnectionTimeout.Value;
        }

        _client = new SftpClient(connectionInfo2);

        _logger.LogInformation("Connecting to SFTP server {Host}:{Port}", connectionInfo.Host, connectionInfo.Port);
        _client.Connect();
        _logger.LogInformation("Connected to SFTP server {Host}:{Port}", connectionInfo.Host, connectionInfo.Port);

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client?.IsConnected == true)
        {
            _logger.LogInformation("Disconnecting from SFTP server");
            _client.Disconnect();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RemoteFileInfo>> ListFilesAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var files = _client!.ListDirectory(remotePath)
            .Where(f => f.Name != "." && f.Name != "..")
            .Select(f => new RemoteFileInfo(
                f.Name,
                f.FullName,
                f.Length,
                f.LastWriteTimeUtc,
                f.IsDirectory))
            .ToList();

        return Task.FromResult<IReadOnlyList<RemoteFileInfo>>(files);
    }

    public Task<Stream> DownloadFileAsync(string remoteFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var memoryStream = new MemoryStream();
        _client!.DownloadFile(remoteFilePath, memoryStream);
        memoryStream.Position = 0;

        return Task.FromResult<Stream>(memoryStream);
    }

    public Task DownloadFileAsync(string remoteFilePath, string localFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var fileStream = File.Create(localFilePath);
        _client!.DownloadFile(remoteFilePath, fileStream);

        _logger.LogDebug("Downloaded {RemotePath} to {LocalPath}", remoteFilePath, localFilePath);

        return Task.CompletedTask;
    }

    public Task UploadFileAsync(string localFilePath, string remoteFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        using var fileStream = File.OpenRead(localFilePath);
        _client!.UploadFile(fileStream, remoteFilePath);

        _logger.LogDebug("Uploaded {LocalPath} to {RemotePath}", localFilePath, remoteFilePath);

        return Task.CompletedTask;
    }

    public Task UploadFileAsync(Stream content, string remoteFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        _client!.UploadFile(content, remoteFilePath);

        _logger.LogDebug("Uploaded stream to {RemotePath}", remoteFilePath);

        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(string remoteFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        _client!.DeleteFile(remoteFilePath);

        _logger.LogDebug("Deleted {RemotePath}", remoteFilePath);

        return Task.CompletedTask;
    }

    public Task MoveFileAsync(string sourceRemotePath, string destinationRemotePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        _client!.RenameFile(sourceRemotePath, destinationRemotePath);

        _logger.LogDebug("Moved {SourcePath} to {DestinationPath}", sourceRemotePath, destinationRemotePath);

        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string remoteFilePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var exists = _client!.Exists(remoteFilePath);

        return Task.FromResult(exists);
    }

    public Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        _client!.CreateDirectory(remotePath);

        _logger.LogDebug("Created directory {RemotePath}", remotePath);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await DisconnectAsync();
        _client?.Dispose();
        _disposed = true;
    }

    private void EnsureConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to SFTP server. Call ConnectAsync first.");
        }
    }

    private static AuthenticationMethod[] BuildAuthenticationMethods(TransportConnectionInfo info)
    {
        var methods = new List<AuthenticationMethod>();

        if (!string.IsNullOrEmpty(info.PrivateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(info.PrivateKeyPassphrase)
                ? new PrivateKeyFile(info.PrivateKeyPath)
                : new PrivateKeyFile(info.PrivateKeyPath, info.PrivateKeyPassphrase);

            methods.Add(new PrivateKeyAuthenticationMethod(info.Username, keyFile));
        }

        if (!string.IsNullOrEmpty(info.Password))
        {
            methods.Add(new PasswordAuthenticationMethod(info.Username, info.Password));
        }

        if (methods.Count == 0)
        {
            throw new ArgumentException("Either password or private key must be provided for authentication.");
        }

        return methods.ToArray();
    }
}
