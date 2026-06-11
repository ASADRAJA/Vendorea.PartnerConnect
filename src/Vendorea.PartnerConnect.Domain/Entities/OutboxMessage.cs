namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a message in the outbox for reliable delivery.
/// Implements the Outbox pattern for transactional messaging.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Type of the message (e.g., "DocumentStateChanged", "WebhookDelivery").
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Serialized message payload (JSON).
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Destination for the message (e.g., webhook URL, queue name).
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>
    /// Optional correlation ID for tracking related messages.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Current status of the message.
    /// </summary>
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    /// <summary>
    /// Number of delivery attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Maximum number of retries before marking as failed.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Error message from the last failed attempt.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the message was last processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// When the next retry should occur.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// When the message was successfully delivered.
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Optional reference to the related entity ID.
    /// </summary>
    public int? RelatedEntityId { get; set; }

    /// <summary>
    /// Optional reference to the related entity type.
    /// </summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>
    /// Priority of the message (higher = more important).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Calculates the next retry time using exponential backoff.
    /// </summary>
    public void ScheduleRetry()
    {
        RetryCount++;
        if (RetryCount <= MaxRetries)
        {
            // Exponential backoff: 30s, 1m, 2m, 4m, 8m, etc.
            var delaySeconds = Math.Pow(2, RetryCount - 1) * 30;
            NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
            Status = OutboxMessageStatus.Retry;
        }
        else
        {
            Status = OutboxMessageStatus.Failed;
        }
    }

    /// <summary>
    /// Marks the message as successfully delivered.
    /// </summary>
    public void MarkDelivered()
    {
        Status = OutboxMessageStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the message as in processing.
    /// </summary>
    public void MarkProcessing()
    {
        Status = OutboxMessageStatus.Processing;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Manually requeues the message for delivery (operator replay of a Failed/Cancelled message).
    /// Resets the retry budget and schedules immediate pickup; the prior LastError is retained for
    /// audit until the next attempt overwrites it.
    /// </summary>
    public void Requeue()
    {
        Status = OutboxMessageStatus.Pending;
        RetryCount = 0;
        NextRetryAt = null;
    }
}

/// <summary>
/// Status of an outbox message.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>
    /// Message is waiting to be processed.
    /// </summary>
    Pending,

    /// <summary>
    /// Message is currently being processed.
    /// </summary>
    Processing,

    /// <summary>
    /// Message was successfully delivered.
    /// </summary>
    Delivered,

    /// <summary>
    /// Message is scheduled for retry.
    /// </summary>
    Retry,

    /// <summary>
    /// Message delivery failed after max retries.
    /// </summary>
    Failed,

    /// <summary>
    /// Message was cancelled.
    /// </summary>
    Cancelled
}
