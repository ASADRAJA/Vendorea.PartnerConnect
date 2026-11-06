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
}
