using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for webhook subscription operations.
/// </summary>
public interface IWebhookSubscriptionRepository
{
    /// <summary>
    /// Adds a new subscription.
    /// </summary>
    Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a subscription by ID.
    /// </summary>
    Task<WebhookSubscription?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all subscriptions for a dealer.
    /// </summary>
    Task<IReadOnlyList<WebhookSubscription>> GetByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active subscriptions for a specific event.
    /// </summary>
    Task<IReadOnlyList<WebhookSubscription>> GetActiveForEventAsync(
        int dealerId,
        string eventType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a subscription.
    /// </summary>
    Task UpdateAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a subscription.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets subscriptions with too many consecutive failures.
    /// </summary>
    Task<IReadOnlyList<WebhookSubscription>> GetFailingSubscriptionsAsync(
        int minConsecutiveFailures = 5,
        CancellationToken cancellationToken = default);
}
