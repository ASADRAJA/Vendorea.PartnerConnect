using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Webhooks.Interfaces;

/// <summary>
/// Service for managing webhook subscriptions and triggering events.
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Creates a new webhook subscription.
    /// </summary>
    Task<WebhookSubscription> CreateSubscriptionAsync(
        int dealerId,
        string name,
        string targetUrl,
        IEnumerable<string> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing subscription.
    /// </summary>
    Task<WebhookSubscription?> UpdateSubscriptionAsync(
        int subscriptionId,
        string? name = null,
        string? targetUrl = null,
        IEnumerable<string>? events = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a subscription.
    /// </summary>
    Task<bool> DeleteSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a subscription by ID.
    /// </summary>
    Task<WebhookSubscription?> GetSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all subscriptions for a dealer.
    /// </summary>
    Task<IReadOnlyList<WebhookSubscription>> GetSubscriptionsByDealerAsync(
        int dealerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets subscriptions for a specific event.
    /// </summary>
    Task<IReadOnlyList<WebhookSubscription>> GetSubscriptionsForEventAsync(
        int dealerId,
        string eventType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a webhook event for delivery.
    /// </summary>
    Task<int> TriggerEventAsync<T>(
        int dealerId,
        string eventType,
        T payload,
        string? correlationId = null,
        int? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Regenerates the secret for a subscription.
    /// </summary>
    Task<string> RegenerateSecretAsync(int subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests a webhook subscription.
    /// </summary>
    Task<WebhookTestResult> TestSubscriptionAsync(
        int subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent deliveries for a subscription.
    /// </summary>
    Task<IReadOnlyList<WebhookDelivery>> GetDeliveriesAsync(
        int subscriptionId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries a failed delivery.
    /// </summary>
    Task<bool> RetryDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a webhook test.
/// </summary>
public record WebhookTestResult
{
    public bool Success { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int DurationMs { get; init; }
    public string? ResponseBody { get; init; }
}
