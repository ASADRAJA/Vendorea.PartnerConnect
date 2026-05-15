using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for managing merchant subscription requests from M360.
/// </summary>
public interface IMerchantSubscriptionRequestRepository
{
    Task<MerchantSubscriptionRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MerchantSubscriptionRequest>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MerchantSubscriptionRequest>> GetByStatusAsync(
        SubscriptionRequestStatus status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MerchantSubscriptionRequest>> GetByTenantIdAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MerchantSubscriptionRequest>> GetByTradingPartnerIdAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default);

    Task<MerchantSubscriptionRequest?> GetByTenantAndPartnerAsync(
        int tenantId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default);

    Task<MerchantSubscriptionRequest> AddAsync(
        MerchantSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        MerchantSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}
