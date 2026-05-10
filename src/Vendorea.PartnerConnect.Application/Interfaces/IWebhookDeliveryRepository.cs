using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for webhook delivery operations.
/// </summary>
public interface IWebhookDeliveryRepository
{
    /// <summary>
    /// Adds a new delivery.
    /// </summary>
    Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a delivery by ID.
    /// </summary>
    Task<WebhookDelivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets deliveries for a subscription.
    /// </summary>
    Task<IReadOnlyList<WebhookDelivery>> GetBySubscriptionIdAsync(
        int subscriptionId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending deliveries ready to send.
    /// </summary>
    Task<IReadOnlyList<WebhookDelivery>> GetPendingAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets deliveries due for retry.
    /// </summary>
    Task<IReadOnlyList<WebhookDelivery>> GetRetryDueAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a delivery.
    /// </summary>
    Task UpdateAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old delivered messages.
    /// </summary>
    Task<int> CleanupDeliveredAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets delivery statistics.
    /// </summary>
    Task<WebhookDeliveryStatistics> GetStatisticsAsync(
        int? subscriptionId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about webhook deliveries.
/// </summary>
public record WebhookDeliveryStatistics
{
    public int PendingCount { get; init; }
    public int RetryCount { get; init; }
    public int FailedCount { get; init; }
    public int DeliveredLast24Hours { get; init; }
    public double AverageDeliveryTimeMs { get; init; }
    public double SuccessRate { get; init; }
}
