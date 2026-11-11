using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class IntegrationProviderRepository : IIntegrationProviderRepository
{
    private readonly PartnerConnectDbContext _context;
    public IntegrationProviderRepository(PartnerConnectDbContext context) => _context = context;

    public async Task<IReadOnlyList<IntegrationProvider>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => await _context.IntegrationProviders.AsNoTracking()
            .Where(p => p.IsEnabled).OrderBy(p => p.Name).ToListAsync(cancellationToken);

    public async Task<IntegrationProvider?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await _context.IntegrationProviders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == code, cancellationToken);

    public async Task<IntegrationProvider?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.IntegrationProviders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
}

public class IntegrationConnectionRepository : IIntegrationConnectionRepository
{
    private readonly PartnerConnectDbContext _context;
    public IntegrationConnectionRepository(PartnerConnectDbContext context) => _context = context;

    // Credentials are never Included. The navigation exists for cascade delete, not for reading
    // secrets on a list screen.
    private IQueryable<IntegrationConnection> WithProvider() =>
        _context.IntegrationConnections.Include(c => c.Provider);

    public async Task<IReadOnlyList<IntegrationConnection>> GetForTenantAsync(
        int tenantId, CancellationToken cancellationToken = default)
        => await WithProvider().AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IntegrationConnection?> GetAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken = default)
        => await WithProvider()
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.TenantId == tenantId, cancellationToken);

    public async Task<IntegrationConnection?> FindByExternalAccountAsync(
        int tenantId, int providerId, string externalAccountId, CancellationToken cancellationToken = default)
        => await _context.IntegrationConnections
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId
                     && c.IntegrationProviderId == providerId
                     && c.ExternalAccountId == externalAccountId,
                cancellationToken);

    public async Task<IntegrationConnection> AddAsync(
        IntegrationConnection connection, CancellationToken cancellationToken = default)
    {
        _context.IntegrationConnections.Add(connection);
        await _context.SaveChangesAsync(cancellationToken);
        return connection;
    }

    public async Task<int?> FindOwningTenantAsync(
        int providerId, string externalAccountId, CancellationToken cancellationToken = default)
    {
        // Projects the tenant id alone. Returning the connection would put another tenant's row
        // — and its navigation properties — in the hands of a caller that has no business with it.
        var owner = await _context.IntegrationConnections
            .AsNoTracking()
            .Where(c => c.IntegrationProviderId == providerId
                        && c.ExternalAccountId == externalAccountId
                        && c.AccountOwnership == IntegrationAccountOwnership.Primary
                        && c.Status != IntegrationConnectionStatus.Revoked)
            .Select(c => (int?)c.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        return owner;
    }

    public async Task UpdateAsync(IntegrationConnection connection, CancellationToken cancellationToken = default)
    {
        connection.UpdatedAt = DateTime.UtcNow;
        _context.IntegrationConnections.Update(connection);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IntegrationConnection>> GetByStatusAsync(
        IntegrationConnectionStatus status, int limit, CancellationToken cancellationToken = default)
        => await WithProvider().AsNoTracking()
            .Where(c => c.Status == status)
            .OrderBy(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
}

public class IntegrationCredentialRepository : IIntegrationCredentialRepository
{
    private readonly PartnerConnectDbContext _context;
    public IntegrationCredentialRepository(PartnerConnectDbContext context) => _context = context;

    public async Task<IntegrationCredential?> GetAsync(
        Guid connectionId, IntegrationCredentialType type, CancellationToken cancellationToken = default)
        => await _context.IntegrationCredentials
            .FirstOrDefaultAsync(
                c => c.IntegrationConnectionId == connectionId && c.CredentialType == type,
                cancellationToken);

    public async Task UpsertAsync(IntegrationCredential credential, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(
            credential.IntegrationConnectionId, credential.CredentialType, cancellationToken);

        if (existing is null)
        {
            _context.IntegrationCredentials.Add(credential);
        }
        else
        {
            // Replace in place and bump the version: rotation history belongs in the audit log,
            // not in a growing pile of superseded secrets.
            existing.ProtectedValue = credential.ProtectedValue;
            existing.ExpiresAt = credential.ExpiresAt;
            existing.Version += 1;
            existing.RotatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        await _context.IntegrationCredentials
            .Where(c => c.IntegrationConnectionId == connectionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(IntegrationCredentialType Type, int Version, DateTime? ExpiresAt)>> DescribeAsync(
        Guid connectionId, CancellationToken cancellationToken = default)
    {
        // Projects away ProtectedValue: the describe path cannot leak a secret even by accident.
        var rows = await _context.IntegrationCredentials.AsNoTracking()
            .Where(c => c.IntegrationConnectionId == connectionId)
            .Select(c => new { c.CredentialType, c.Version, c.ExpiresAt })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.CredentialType, r.Version, r.ExpiresAt)).ToList();
    }
}

public class IntegrationSyncStateRepository : IIntegrationSyncStateRepository
{
    private readonly PartnerConnectDbContext _context;
    public IntegrationSyncStateRepository(PartnerConnectDbContext context) => _context = context;

    public async Task<IReadOnlyList<IntegrationSyncState>> GetForConnectionAsync(
        Guid connectionId, CancellationToken cancellationToken = default)
        => await _context.IntegrationSyncStates.AsNoTracking()
            .Where(s => s.IntegrationConnectionId == connectionId)
            .ToListAsync(cancellationToken);

    public async Task<IntegrationSyncState?> GetAsync(
        Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
        CancellationToken cancellationToken = default)
        => await _context.IntegrationSyncStates
            .FirstOrDefaultAsync(
                s => s.IntegrationConnectionId == connectionId
                     && s.Capability == capability
                     && s.MarketplaceId == marketplaceId,
                cancellationToken);

    public async Task AdvanceWatermarkAsync(
        Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
        DateTime watermark, string? cursor, CancellationToken cancellationToken = default)
    {
        var state = await GetAsync(connectionId, capability, marketplaceId, cancellationToken);
        if (state is null)
        {
            state = new IntegrationSyncState
            {
                IntegrationConnectionId = connectionId,
                Capability = capability,
                MarketplaceId = marketplaceId
            };
            _context.IntegrationSyncStates.Add(state);
        }

        // Never move a watermark backwards: a delayed run must not undo a later one's progress.
        if (state.Watermark is null || watermark > state.Watermark)
        {
            state.Watermark = watermark;
        }

        state.Cursor = cursor;
        state.LastSuccessAt = DateTime.UtcNow;
        state.LastAttemptAt = DateTime.UtcNow;
        state.ConsecutiveFailures = 0;
        state.LastError = null;
        state.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(
        Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
        string error, CancellationToken cancellationToken = default)
    {
        var state = await GetAsync(connectionId, capability, marketplaceId, cancellationToken);
        if (state is null)
        {
            state = new IntegrationSyncState
            {
                IntegrationConnectionId = connectionId,
                Capability = capability,
                MarketplaceId = marketplaceId
            };
            _context.IntegrationSyncStates.Add(state);
        }

        // The watermark is deliberately untouched. A failed run has persisted nothing it can
        // claim credit for.
        state.LastAttemptAt = DateTime.UtcNow;
        state.ConsecutiveFailures += 1;
        state.LastError = error.Length > 2000 ? error[..2000] : error;
        state.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> OrderByStalenessAsync(
        IntegrationCapabilities capability, IReadOnlyList<Guid> candidateConnectionIds,
        CancellationToken cancellationToken = default)
    {
        if (candidateConnectionIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var attempts = await _context.IntegrationSyncStates
            .AsNoTracking()
            .Where(s => s.Capability == capability && candidateConnectionIds.Contains(s.IntegrationConnectionId))
            .GroupBy(s => s.IntegrationConnectionId)
            .Select(g => new { ConnectionId = g.Key, LastAttemptAt = g.Max(s => s.LastAttemptAt) })
            .ToDictionaryAsync(x => x.ConnectionId, x => x.LastAttemptAt, cancellationToken);

        // A connection with no state has never been attempted, so it sorts ahead of every
        // connection that has. A new seller must not wait behind an established one's backlog.
        return candidateConnectionIds
            .OrderBy(id => attempts.TryGetValue(id, out var at) ? at ?? DateTime.MinValue : DateTime.MinValue)
            .ToList();
    }

    public async Task RecordAttemptAsync(
        Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
        CancellationToken cancellationToken = default)
    {
        var state = await GetAsync(connectionId, capability, marketplaceId, cancellationToken);
        if (state is null)
        {
            state = new IntegrationSyncState
            {
                IntegrationConnectionId = connectionId,
                Capability = capability,
                MarketplaceId = marketplaceId
            };
            _context.IntegrationSyncStates.Add(state);
        }

        // Recorded even when the run does nothing — a rate-limited connection has still had its
        // turn, and without this it would be picked first again on every cycle.
        state.LastAttemptAt = DateTime.UtcNow;
        state.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class IntegrationAccountDelegationRepository : IIntegrationAccountDelegationRepository
{
    private readonly PartnerConnectDbContext _context;

    public IntegrationAccountDelegationRepository(PartnerConnectDbContext context) => _context = context;

    public Task<IntegrationAccountDelegation?> FindActiveAsync(
        int providerId, string externalAccountId, int delegateTenantId,
        CancellationToken cancellationToken = default)
        => _context.IntegrationAccountDelegations
            .FirstOrDefaultAsync(
                d => d.IntegrationProviderId == providerId
                     && d.ExternalAccountId == externalAccountId
                     && d.DelegateTenantId == delegateTenantId
                     && d.RevokedAt == null,
                cancellationToken);

    public async Task<IReadOnlyList<IntegrationAccountDelegation>> GetForAccountAsync(
        int providerId, string externalAccountId, CancellationToken cancellationToken = default)
        => await _context.IntegrationAccountDelegations
            .AsNoTracking()
            .Where(d => d.IntegrationProviderId == providerId && d.ExternalAccountId == externalAccountId)
            .OrderByDescending(d => d.GrantedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        IntegrationAccountDelegation delegation, CancellationToken cancellationToken = default)
    {
        _context.IntegrationAccountDelegations.Add(delegation);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<IntegrationAccountDelegation?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.IntegrationAccountDelegations.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
