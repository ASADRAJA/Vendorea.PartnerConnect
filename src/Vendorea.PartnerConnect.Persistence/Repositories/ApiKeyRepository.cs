using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository for API key operations.
/// </summary>
public class ApiKeyRepository : IApiKeyRepository
{
    private readonly PartnerConnectDbContext _context;

    public ApiKeyRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        await _context.ApiKeys.AddAsync(apiKey, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiKey>> GetByDealerIdAsync(
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeys
            .Where(k => k.DealerId == dealerId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiKey>> GetActiveByDealerIdAsync(
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.ApiKeys
            .Where(k => k.DealerId == dealerId
                && k.IsActive
                && !k.RevokedAt.HasValue
                && (!k.ExpiresAt.HasValue || k.ExpiresAt > now))
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        _context.ApiKeys.Update(apiKey);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetByIdAsync(id, cancellationToken);
        if (apiKey == null) return false;

        _context.ApiKeys.Remove(apiKey);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiKey>> GetExpiredKeysAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.ApiKeys
            .Where(k => k.IsActive
                && !k.RevokedAt.HasValue
                && k.ExpiresAt.HasValue
                && k.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
    }
}
