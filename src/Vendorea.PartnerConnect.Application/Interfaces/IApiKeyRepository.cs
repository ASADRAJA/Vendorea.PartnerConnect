using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for API key operations.
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>
    /// Adds a new API key.
    /// </summary>
    Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an API key by ID.
    /// </summary>
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an API key by its hash.
    /// </summary>
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all API keys for a dealer.
    /// </summary>
    Task<IReadOnlyList<ApiKey>> GetByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active API keys for a dealer.
    /// </summary>
    Task<IReadOnlyList<ApiKey>> GetActiveByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an API key.
    /// </summary>
    Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an API key.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets expired keys.
    /// </summary>
    Task<IReadOnlyList<ApiKey>> GetExpiredKeysAsync(CancellationToken cancellationToken = default);
}
