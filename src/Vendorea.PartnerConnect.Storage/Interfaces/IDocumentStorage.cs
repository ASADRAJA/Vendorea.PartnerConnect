using Vendorea.PartnerConnect.Storage.Models;

namespace Vendorea.PartnerConnect.Storage.Interfaces;

/// <summary>
/// Interface for document storage operations.
/// Supports storing raw documents from partners for audit, replay, and troubleshooting.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>
    /// Stores a document and returns the storage path.
    /// </summary>
    /// <param name="content">The document content stream.</param>
    /// <param name="path">The relative storage path.</param>
    /// <param name="metadata">Document metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full storage path.</returns>
    Task<string> StoreAsync(
        Stream content,
        string path,
        StorageMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a document from a byte array.
    /// </summary>
    Task<string> StoreAsync(
        byte[] content,
        string path,
        StorageMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a document as a stream.
    /// </summary>
    /// <param name="path">The storage path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document content stream.</returns>
    Task<Stream> RetrieveAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a document as a byte array.
    /// </summary>
    Task<byte[]> RetrieveBytesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata for a stored document.
    /// </summary>
    /// <param name="path">The storage path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The metadata, or null if not found.</returns>
    Task<StorageMetadata?> GetMetadataAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document exists at the given path.
    /// </summary>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists documents matching the given prefix.
    /// </summary>
    /// <param name="prefix">Path prefix to filter by.</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of stored documents.</returns>
    Task<IReadOnlyList<StoredDocument>> ListAsync(
        string prefix,
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the SHA-256 hash of a stream.
    /// </summary>
    Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory for creating document storage instances.
/// </summary>
public interface IDocumentStorageFactory
{
    /// <summary>
    /// Creates a document storage instance.
    /// </summary>
    IDocumentStorage Create();
}
