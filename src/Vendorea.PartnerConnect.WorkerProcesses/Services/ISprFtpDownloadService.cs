using Vendorea.PartnerConnect.WorkerProcesses.Configuration;

namespace Vendorea.PartnerConnect.WorkerProcesses.Services;

/// <summary>
/// Service for downloading SPR content files from Etilize FTP.
/// </summary>
public interface ISprFtpDownloadService
{
    /// <summary>
    /// Downloads all configured content files from FTP.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Download result with all file information.</returns>
    Task<DownloadResult> DownloadAllFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a specific file from FTP.
    /// </summary>
    Task<string?> DownloadFileAsync(string remotePath, string localPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the FTP connection and returns detailed result.
    /// </summary>
    Task<FtpConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an FTP connection test.
/// </summary>
public class FtpConnectionTestResult
{
    public bool Success { get; set; }
    public int FilesFound { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Information about a downloaded file.
/// </summary>
public class DownloadedFileInfo
{
    public SprFileMapping Mapping { get; set; } = new();
    public string LocalPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime DownloadedAt { get; set; }
}

/// <summary>
/// Result of a download operation.
/// </summary>
public class DownloadResult
{
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;
    public List<DownloadedFileInfo> Files { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public int FilesDownloaded => Files.Count(f => f.Success);
    public int FilesFailed => Files.Count(f => !f.Success);
    public long TotalBytesDownloaded => Files.Where(f => f.Success).Sum(f => f.FileSizeBytes);
}
