using FluentFTP;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;
using Vendorea.PartnerConnect.WorkerProcesses.Storage;

namespace Vendorea.PartnerConnect.WorkerProcesses.Services;

/// <summary>
/// Service for downloading SPR content files from Etilize FTP using FluentFTP.
/// Supports both local file system and Azure Blob Storage for downloaded files.
/// </summary>
public class SprFtpDownloadService : ISprFtpDownloadService
{
    private readonly ILogger<SprFtpDownloadService> _logger;
    private readonly SprContentIngestionOptions _options;
    private readonly IIngestionFileStorage _storage;
    private readonly string _tempPath;

    public SprFtpDownloadService(
        ILogger<SprFtpDownloadService> logger,
        IOptions<SprContentIngestionOptions> options,
        IIngestionFileStorage storage)
    {
        _logger = logger;
        _options = options.Value;
        _storage = storage;

        // Use system temp for FTP downloads (FluentFTP needs local path)
        _tempPath = Path.Combine(Path.GetTempPath(), "spr-ftp-temp");
        Directory.CreateDirectory(_tempPath);
    }

    public async Task<DownloadResult> DownloadAllFilesAsync(CancellationToken cancellationToken = default)
    {
        var downloadResult = new DownloadResult
        {
            StartedAt = DateTime.UtcNow
        };
        var mappings = SprFtpFileMapping.GetFileMappings(_options.Locale, _options.DatabaseType, _options);

        using var client = CreateFtpClient();

        try
        {
            await client.Connect(cancellationToken);
            _logger.LogInformation("Connected to FTP server: {Host} (Storage: {StorageType})",
                _options.FtpHost, _storage.StorageType);

            foreach (var mapping in mappings)
            {
                var fileResult = new DownloadedFileInfo
                {
                    Mapping = mapping,
                    LocalPath = mapping.LocalZipName, // Now this is the relative storage path
                    DownloadedAt = DateTime.UtcNow
                };

                try
                {
                    _logger.LogInformation("Downloading: {RemotePath}", mapping.RemotePath);

                    // Download to temp location first (FluentFTP requires local path)
                    var tempFilePath = Path.Combine(_tempPath, mapping.LocalZipName);
                    var tempDir = Path.GetDirectoryName(tempFilePath);
                    if (!string.IsNullOrEmpty(tempDir))
                    {
                        Directory.CreateDirectory(tempDir);
                    }

                    var status = await client.DownloadFile(
                        tempFilePath,
                        mapping.RemotePath,
                        FtpLocalExists.Overwrite,
                        FtpVerify.None,
                        null,
                        cancellationToken);

                    if (status == FtpStatus.Success)
                    {
                        var fileInfo = new FileInfo(tempFilePath);
                        fileResult.FileSizeBytes = fileInfo.Length;

                        // Upload to storage (local or blob)
                        await using var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
                        var storagePath = await _storage.SaveFileAsync(fileStream, mapping.LocalZipName, cancellationToken);

                        fileResult.Success = true;
                        fileResult.LocalPath = mapping.LocalZipName;

                        _logger.LogInformation("Downloaded and stored: {FileName} ({Size:N0} bytes) -> {StorageType}",
                            mapping.LocalZipName, fileResult.FileSizeBytes, _storage.StorageType);

                        // Clean up temp file if using blob storage
                        if (_storage.StorageType == StorageType.AzureBlob)
                        {
                            try
                            {
                                File.Delete(tempFilePath);
                            }
                            catch
                            {
                                // Ignore cleanup errors
                            }
                        }
                    }
                    else
                    {
                        fileResult.Success = false;
                        fileResult.ErrorMessage = $"FTP download returned status: {status}";
                        _logger.LogWarning("Failed to download {FileName}: {Status}",
                            mapping.LocalZipName, status);
                    }
                }
                catch (Exception ex)
                {
                    fileResult.Success = false;
                    fileResult.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "Error downloading {FileName}", mapping.LocalZipName);
                }

                downloadResult.Files.Add(fileResult);

                // Check cancellation between files
                cancellationToken.ThrowIfCancellationRequested();
            }

            downloadResult.Success = downloadResult.FilesFailed == 0;
        }
        catch (Exception ex)
        {
            downloadResult.Success = false;
            downloadResult.Errors.Add(ex.Message);
            _logger.LogError(ex, "FTP download failed");
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.Disconnect(cancellationToken);
            }
        }

        downloadResult.CompletedAt = DateTime.UtcNow;
        _logger.LogInformation("FTP download completed: {Success} succeeded, {Failed} failed",
            downloadResult.FilesDownloaded, downloadResult.FilesFailed);

        return downloadResult;
    }

    public async Task<string?> DownloadFileAsync(string remotePath, string localPath, CancellationToken cancellationToken = default)
    {
        using var client = CreateFtpClient();

        try
        {
            await client.Connect(cancellationToken);

            // Download to temp first
            var tempFilePath = Path.Combine(_tempPath, Path.GetFileName(localPath));
            var tempDir = Path.GetDirectoryName(tempFilePath);
            if (!string.IsNullOrEmpty(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var status = await client.DownloadFile(
                tempFilePath,
                remotePath,
                FtpLocalExists.Overwrite,
                FtpVerify.None,
                null,
                cancellationToken);

            if (status == FtpStatus.Success)
            {
                // Upload to storage
                await using var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
                var storagePath = await _storage.SaveFileAsync(fileStream, localPath, cancellationToken);

                // Clean up temp if using blob
                if (_storage.StorageType == StorageType.AzureBlob)
                {
                    try { File.Delete(tempFilePath); } catch { }
                }

                return localPath;
            }

            _logger.LogWarning("Failed to download {RemotePath}: {Status}", remotePath, status);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading {RemotePath}", remotePath);
            return null;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.Disconnect(cancellationToken);
            }
        }
    }

    public async Task<FtpConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateFtpClient();

        try
        {
            await client.Connect(cancellationToken);
            _logger.LogInformation("FTP connection test successful: {Host}", _options.FtpHost);

            // Try to list files in the root directory
            var files = await client.GetListing(_options.BasePath ?? "/", cancellationToken);
            var fileCount = files?.Length ?? 0;

            await client.Disconnect(cancellationToken);

            return new FtpConnectionTestResult
            {
                Success = true,
                FilesFound = fileCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FTP connection test failed: {Host}", _options.FtpHost);
            return new FtpConnectionTestResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private AsyncFtpClient CreateFtpClient()
    {
        var client = new AsyncFtpClient(
            _options.FtpHost,
            _options.FtpUsername,
            _options.FtpPassword);

        // Use plain FTP - many traditional FTP servers don't support FTPS
        client.Config.EncryptionMode = FtpEncryptionMode.None;
        client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
        client.Config.ConnectTimeout = 30000;
        client.Config.ReadTimeout = 60000;
        client.Config.DataConnectionConnectTimeout = 30000;
        client.Config.DataConnectionReadTimeout = 60000;

        return client;
    }
}
