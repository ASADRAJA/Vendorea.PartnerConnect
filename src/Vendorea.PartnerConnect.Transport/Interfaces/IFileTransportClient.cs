namespace Vendorea.PartnerConnect.Transport.Interfaces;

public interface IFileTransportClient : IAsyncDisposable
{
    Task ConnectAsync(TransportConnectionInfo connectionInfo, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    bool IsConnected { get; }

    Task<IReadOnlyList<RemoteFileInfo>> ListFilesAsync(string remotePath, CancellationToken cancellationToken = default);
    Task<Stream> DownloadFileAsync(string remoteFilePath, CancellationToken cancellationToken = default);
    Task DownloadFileAsync(string remoteFilePath, string localFilePath, CancellationToken cancellationToken = default);
    Task UploadFileAsync(string localFilePath, string remoteFilePath, CancellationToken cancellationToken = default);
    Task UploadFileAsync(Stream content, string remoteFilePath, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string remoteFilePath, CancellationToken cancellationToken = default);
    Task MoveFileAsync(string sourceRemotePath, string destinationRemotePath, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string remoteFilePath, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken = default);
}

public record TransportConnectionInfo(
    string Host,
    int Port,
    string Username,
    string? Password = null,
    string? PrivateKeyPath = null,
    string? PrivateKeyPassphrase = null,
    TimeSpan? ConnectionTimeout = null);

public record RemoteFileInfo(
    string Name,
    string FullPath,
    long Size,
    DateTime LastModifiedUtc,
    bool IsDirectory);
