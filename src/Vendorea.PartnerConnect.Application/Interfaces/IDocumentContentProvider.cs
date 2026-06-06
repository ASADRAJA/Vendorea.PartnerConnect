namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Provides document content retrieval for the application layer.
/// This abstraction allows retrieving stored document content
/// without directly depending on storage implementation details.
/// </summary>
public interface IDocumentContentProvider
{
    /// <summary>
    /// Retrieves document content as a string.
    /// </summary>
    /// <param name="storagePath">The storage path of the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document content as a string.</returns>
    Task<string> GetContentAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves document content as bytes.
    /// </summary>
    /// <param name="storagePath">The storage path of the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document content as a byte array.</returns>
    Task<byte[]> GetContentBytesAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves document content as a stream.
    /// </summary>
    /// <param name="storagePath">The storage path of the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the document content.</returns>
    Task<Stream> GetContentStreamAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document exists at the given path.
    /// </summary>
    Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default);
}
