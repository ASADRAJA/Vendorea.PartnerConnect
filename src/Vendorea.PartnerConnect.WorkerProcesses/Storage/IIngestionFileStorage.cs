namespace Vendorea.PartnerConnect.WorkerProcesses.Storage;

/// <summary>
/// Abstraction for ingestion file storage, supporting both local file system and Azure Blob Storage.
/// </summary>
public interface IIngestionFileStorage
{
    /// <summary>
    /// Saves a file to storage.
    /// </summary>
    /// <param name="stream">The file content stream.</param>
    /// <param name="fileName">The file name (relative path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full path or URI to the saved file.</returns>
    Task<string> SaveFileAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file for reading.
    /// </summary>
    /// <param name="fileName">The file name (relative path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream to read the file content.</returns>
    Task<Stream> OpenReadAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="fileName">The file name (relative path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists in storage.
    /// </summary>
    /// <param name="fileName">The file name (relative path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file exists.</returns>
    Task<bool> FileExistsAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists files in storage with an optional prefix filter.
    /// </summary>
    /// <param name="prefix">Optional prefix to filter files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of file names.</returns>
    Task<IReadOnlyList<string>> ListFilesAsync(string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all files in storage with an optional prefix filter.
    /// </summary>
    /// <param name="prefix">Optional prefix to filter files to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAllAsync(string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the local file path for a file (for services that need direct file access).
    /// For blob storage, this downloads the file to a temp location first.
    /// </summary>
    /// <param name="fileName">The file name (relative path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Local file path.</returns>
    Task<string> GetLocalPathAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the storage type (Local or AzureBlob).
    /// </summary>
    StorageType StorageType { get; }
}

/// <summary>
/// Type of ingestion file storage.
/// </summary>
public enum StorageType
{
    Local,
    AzureBlob
}
