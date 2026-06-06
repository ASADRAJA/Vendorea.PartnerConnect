using System.Text;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Storage.Interfaces;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <summary>
/// Implementation of IDocumentContentProvider that wraps IDocumentStorage.
/// Provides document content retrieval for the application layer.
/// </summary>
public class DocumentContentProvider : IDocumentContentProvider
{
    private readonly IDocumentStorage _storage;

    public DocumentContentProvider(IDocumentStorage storage)
    {
        _storage = storage;
    }

    public async Task<string> GetContentAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var bytes = await _storage.RetrieveBytesAsync(storagePath, cancellationToken);
        return Encoding.UTF8.GetString(bytes);
    }

    public Task<byte[]> GetContentBytesAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        return _storage.RetrieveBytesAsync(storagePath, cancellationToken);
    }

    public Task<Stream> GetContentStreamAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        return _storage.RetrieveAsync(storagePath, cancellationToken);
    }

    public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        return _storage.ExistsAsync(storagePath, cancellationToken);
    }
}
