using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository interface for tenant partner account operations.
/// </summary>
public interface ITenantPartnerAccountRepository
{
    Task<TenantPartnerAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tenant partner account by the unique combination of tenant, partner, and account number.
    /// This is the primary validation method for order placement.
    /// </summary>
    Task<TenantPartnerAccount?> GetByTenantPartnerAccountAsync(
        int tenantId,
        int tradingPartnerId,
        string accountNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantPartnerAccount>> GetByTenantIdAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantPartnerAccount>> GetByTradingPartnerIdAsync(int tradingPartnerId, CancellationToken cancellationToken = default);
    Task<TenantPartnerAccount?> GetDefaultAccountAsync(int tenantId, int tradingPartnerId, CancellationToken cancellationToken = default);
    Task<TenantPartnerAccount> AddAsync(TenantPartnerAccount account, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantPartnerAccount account, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int tenantId, int tradingPartnerId, string accountNumber, CancellationToken cancellationToken = default);

    // --- Tenant-partner connection workflow ---

    /// <summary>
    /// Gets a connection with its Tenant, TradingPartner, and Organization loaded.
    /// </summary>
    Task<TenantPartnerAccount?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists connections (org-scoped), optionally filtered by approval status, with details loaded.
    /// </summary>
    Task<IReadOnlyList<TenantPartnerAccount>> GetConnectionsAsync(
        int? organizationId = null,
        ConnectionApprovalStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if a connection already exists for this (org, org-side tenant id, partner).
    /// </summary>
    Task<bool> ConnectionExistsAsync(
        int organizationId,
        string externalTenantId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default);
}
