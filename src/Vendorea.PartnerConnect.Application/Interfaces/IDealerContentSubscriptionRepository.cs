using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for managing dealer content subscriptions.
/// </summary>
public interface IDealerContentSubscriptionRepository
{
    /// <summary>
    /// Gets a subscription by ID.
    /// </summary>
    Task<DealerContentSubscription?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a subscription for a dealer and trading partner.
    /// </summary>
    Task<DealerContentSubscription?> GetByDealerAndPartnerAsync(
        int dealerId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all subscriptions for a dealer.
    /// </summary>
    Task<IReadOnlyList<DealerContentSubscription>> GetByDealerIdAsync(
        int dealerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active subscriptions.
    /// </summary>
    Task<IReadOnlyList<DealerContentSubscription>> GetActiveSubscriptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all subscriptions for a trading partner.
    /// </summary>
    Task<IReadOnlyList<DealerContentSubscription>> GetByTradingPartnerIdAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new subscription.
    /// </summary>
    Task<DealerContentSubscription> CreateAsync(
        DealerContentSubscription subscription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing subscription.
    /// </summary>
    Task UpdateAsync(
        DealerContentSubscription subscription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables enhanced content for a subscription.
    /// </summary>
    Task SetEnabledAsync(
        int subscriptionId,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last content version for a subscription.
    /// </summary>
    Task UpdateLastContentVersionAsync(
        int subscriptionId,
        string contentVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the subscribed locales.
    /// </summary>
    Task UpdateSubscribedLocalesAsync(
        int subscriptionId,
        List<string> locales,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a subscription.
    /// </summary>
    Task DeleteAsync(int subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a dealer has enhanced content enabled.
    /// </summary>
    Task<bool> IsEnhancedContentEnabledAsync(
        int dealerId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets subscriptions that need content refresh.
    /// </summary>
    Task<IReadOnlyList<DealerContentSubscription>> GetSubscriptionsNeedingRefreshAsync(
        string currentContentVersion,
        CancellationToken cancellationToken = default);
}
