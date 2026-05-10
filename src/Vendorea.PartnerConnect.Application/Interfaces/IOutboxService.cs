using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for managing outbox messages.
/// </summary>
public interface IOutboxService
{
    /// <summary>
    /// Enqueues a message to the outbox.
    /// </summary>
    Task<Guid> EnqueueAsync<T>(
        string messageType,
        T payload,
        string? destination = null,
        string? correlationId = null,
        int priority = 0,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Enqueues a webhook delivery to the outbox.
    /// </summary>
    Task<Guid> EnqueueWebhookAsync(
        string webhookUrl,
        object payload,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a document state change notification.
    /// </summary>
    Task<Guid> EnqueueDocumentStateChangeAsync(
        int documentId,
        string previousState,
        string newState,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes pending outbox messages.
    /// </summary>
    Task<int> ProcessPendingAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes messages due for retry.
    /// </summary>
    Task<int> ProcessRetriesAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about outbox messages.
    /// </summary>
    Task<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old delivered messages.
    /// </summary>
    Task<int> CleanupAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default);
}
