namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a webhook delivery attempt.
/// </summary>
public class WebhookDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The subscription this delivery belongs to.
    /// </summary>
    public int WebhookSubscriptionId { get; set; }

    /// <summary>
    /// The event type that triggered this delivery.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// The payload that was/will be delivered (JSON).
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Target URL for the delivery.
    /// </summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    /// Status of the delivery.
    /// </summary>
    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;

    /// <summary>
    /// HTTP status code returned by the target.
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Response body from the target (truncated).
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Error message if delivery failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of delivery attempts.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Request duration in milliseconds.
    /// </summary>
    public int? DurationMs { get; set; }

    /// <summary>
    /// When the delivery was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the delivery was last attempted.
    /// </summary>
    public DateTime? AttemptedAt { get; set; }

    /// <summary>
    /// When the delivery was successfully completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// When the next retry should occur.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Reference to related entity ID.
    /// </summary>
    public int? RelatedEntityId { get; set; }

    /// <summary>
    /// Reference to related entity type.
    /// </summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>
    /// HMAC signature sent with the webhook.
    /// </summary>
    public string? Signature { get; set; }

    // Navigation
    public WebhookSubscription? Subscription { get; set; }

    /// <summary>
    /// Schedules a retry with exponential backoff.
    /// </summary>
    public void ScheduleRetry()
    {
        AttemptCount++;
        if (AttemptCount < MaxAttempts)
        {
            // Exponential backoff: 30s, 1m, 2m, 4m, 8m
            var delaySeconds = Math.Pow(2, AttemptCount - 1) * 30;
            NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
            Status = WebhookDeliveryStatus.Retry;
        }
        else
        {
            Status = WebhookDeliveryStatus.Failed;
        }
    }

    /// <summary>
    /// Marks the delivery as successful.
    /// </summary>
    public void MarkSuccessful(int statusCode, string? responseBody, int durationMs)
    {
        Status = WebhookDeliveryStatus.Delivered;
        HttpStatusCode = statusCode;
        ResponseBody = responseBody?.Length > 1000 ? responseBody[..1000] : responseBody;
        DurationMs = durationMs;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the delivery attempt as failed.
    /// </summary>
    public void MarkFailed(string errorMessage, int? statusCode = null, string? responseBody = null)
    {
        ErrorMessage = errorMessage;
        HttpStatusCode = statusCode;
        ResponseBody = responseBody?.Length > 1000 ? responseBody[..1000] : responseBody;
        AttemptedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Status of a webhook delivery.
/// </summary>
public enum WebhookDeliveryStatus
{
    /// <summary>
    /// Delivery is pending.
    /// </summary>
    Pending,

    /// <summary>
    /// Delivery is in progress.
    /// </summary>
    Sending,

    /// <summary>
    /// Delivery was successful.
    /// </summary>
    Delivered,

    /// <summary>
    /// Delivery is scheduled for retry.
    /// </summary>
    Retry,

    /// <summary>
    /// Delivery failed after max retries.
    /// </summary>
    Failed,

    /// <summary>
    /// Delivery was cancelled.
    /// </summary>
    Cancelled
}
