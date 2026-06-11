using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for outbox message operations.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Adds a new message to the outbox.
    /// </summary>
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple messages to the outbox.
    /// </summary>
    Task AddRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a message by ID.
    /// </summary>
    Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending messages ready for processing.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages scheduled for retry.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetRetryDueAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages by correlation ID.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets messages in the given status, newest first (paged).
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetByStatusAsync(
        OutboxMessageStatus status,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a message.
    /// </summary>
    Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple messages.
    /// </summary>
    Task UpdateRangeAsync(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old delivered messages.
    /// </summary>
    Task<int> CleanupDeliveredAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about outbox messages.
    /// </summary>
    Task<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about outbox messages.
/// </summary>
public record OutboxStatistics
{
    public int PendingCount { get; init; }
    public int ProcessingCount { get; init; }
    public int RetryCount { get; init; }
    public int FailedCount { get; init; }
    public int DeliveredLast24Hours { get; init; }
    public double AverageDeliveryTimeMs { get; init; }
}
