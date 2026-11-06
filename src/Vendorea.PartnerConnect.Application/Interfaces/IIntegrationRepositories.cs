using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>Reference data: the external systems PartnerConnect can connect to.</summary>
public interface IIntegrationProviderRepository
{
    Task<IReadOnlyList<IntegrationProvider>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task<IntegrationProvider?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IntegrationProvider?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Connections, always addressed within a tenant.
/// </summary>
/// <remarks>
/// Every read takes a tenant id. There is deliberately no "get by id" without one and no
/// "find by external account" across tenants: a lookup that can cross the tenant boundary is
/// the one mistake that turns an isolation invariant back into a convention.
/// </remarks>
public interface IIntegrationConnectionRepository
{
    Task<IReadOnlyList<IntegrationConnection>> GetForTenantAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task<IntegrationConnection?> GetAsync(
        int tenantId, Guid connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds this tenant's connection for an external account, in any state including Revoked.
    /// </summary>
    /// <remarks>
    /// The reconnect path depends on this seeing revoked rows. Reactivating one preserves its id,
    /// its sync history and its order associations; creating a second would lose the watermark and
    /// re-import every order as new.
    /// </remarks>
    Task<IntegrationConnection?> FindByExternalAccountAsync(
        int tenantId, int providerId, string externalAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the tenant that owns an external account, or null when nobody does.
    /// </summary>
    /// <remarks>
    /// The one cross-tenant lookup in this interface, and it returns a tenant id and nothing
    /// else — not the connection, not its credentials, not its orders. Only the existence of a
    /// claim crosses the boundary, which is what lets the authorization flow refuse a second
    /// tenant without ever reading the first tenant's data.
    ///
    /// Revoked connections are ignored: a tenant that disconnected has released the account, and
    /// a later claim by someone else is a transfer rather than concurrent sharing.
    /// </remarks>
    Task<int?> FindOwningTenantAsync(
        int providerId, string externalAccountId, CancellationToken cancellationToken = default);

    Task<IntegrationConnection> AddAsync(
        IntegrationConnection connection, CancellationToken cancellationToken = default);

    Task UpdateAsync(IntegrationConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Connections in a given state across all tenants — for operators and pollers only.</summary>
    Task<IReadOnlyList<IntegrationConnection>> GetByStatusAsync(
        IntegrationConnectionStatus status, int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// Credential rows. Nothing here returns a decrypted value — see IIntegrationCredentialStore.
/// </summary>
public interface IIntegrationCredentialRepository
{
    Task<IntegrationCredential?> GetAsync(
        Guid connectionId, IntegrationCredentialType type, CancellationToken cancellationToken = default);

    Task UpsertAsync(IntegrationCredential credential, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(Guid connectionId, CancellationToken cancellationToken = default);

    /// <summary>Metadata only — type, version, expiry. Never the protected value.</summary>
    Task<IReadOnlyList<(IntegrationCredentialType Type, int Version, DateTime? ExpiresAt)>> DescribeAsync(
        Guid connectionId, CancellationToken cancellationToken = default);
}

/// <summary>Per-capability, per-marketplace synchronisation position.</summary>
public interface IIntegrationSyncStateRepository
{
    Task<IReadOnlyList<IntegrationSyncState>> GetForConnectionAsync(
        Guid connectionId, CancellationToken cancellationToken = default);

    Task<IntegrationSyncState?> GetAsync(
        Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the watermark. Call only after every row of the page is durably persisted —
    /// advancing earlier silently drops whatever was not written.
    /// </summary>
    Task AdvanceWatermarkAsync(
        Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
        DateTime watermark, string? cursor, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(
        Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
        string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Orders candidate connections by how long it has been since each was last attempted,
    /// least recently first, with never-attempted ones ahead of all of them.
    /// </summary>
    /// <remarks>
    /// This is the fairness mechanism. A worker that simply took the first N connections it
    /// found would serve the same few every cycle and starve the rest — and with a provider
    /// budget of roughly one call every three minutes, a starved connection is one whose orders
    /// never arrive at all.
    /// </remarks>
    Task<IReadOnlyList<Guid>> OrderByStalenessAsync(
        IntegrationCapabilities capability, IReadOnlyList<Guid> candidateConnectionIds,
        CancellationToken cancellationToken = default);

    /// <summary>Records that an attempt was made, whatever its outcome.</summary>
    Task RecordAttemptAsync(
        Guid connectionId, IntegrationCapabilities capability, string? marketplaceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Explicit grants letting a second tenant connect an account another tenant owns.
/// </summary>
public interface IIntegrationAccountDelegationRepository
{
    /// <summary>The active grant for this delegate on this account, if there is one.</summary>
    Task<IntegrationAccountDelegation?> FindActiveAsync(
        int providerId, string externalAccountId, int delegateTenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Every grant for an account, revoked ones included, for audit.</summary>
    Task<IReadOnlyList<IntegrationAccountDelegation>> GetForAccountAsync(
        int providerId, string externalAccountId, CancellationToken cancellationToken = default);

    Task AddAsync(IntegrationAccountDelegation delegation, CancellationToken cancellationToken = default);

    Task<IntegrationAccountDelegation?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
