namespace Vendorea.PartnerConnect.Metering.Models;

/// <summary>
/// Represents a single usage record for metering.
/// </summary>
public class UsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The dealer ID this usage is associated with.
    /// </summary>
    public int DealerId { get; set; }

    /// <summary>
    /// The type of metric being recorded.
    /// </summary>
    public MetricType MetricType { get; set; }

    /// <summary>
    /// The value/quantity of the metric.
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Unit of measurement (e.g., "documents", "bytes", "calls").
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// When the usage occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The resource that was used (e.g., document ID, endpoint).
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Additional context as JSON.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Whether this record has been aggregated.
    /// </summary>
    public bool IsAggregated { get; set; }
}

/// <summary>
/// Types of metrics that can be metered.
/// </summary>
public enum MetricType
{
    /// <summary>
    /// Document processed (received, validated, sent).
    /// </summary>
    DocumentProcessed,

    /// <summary>
    /// API call made.
    /// </summary>
    ApiCall,

    /// <summary>
    /// Storage used in bytes.
    /// </summary>
    StorageUsed,

    /// <summary>
    /// Webhook delivery.
    /// </summary>
    WebhookDelivery,

    /// <summary>
    /// Premium feature usage.
    /// </summary>
    PremiumFeature,

    /// <summary>
    /// Partner connection.
    /// </summary>
    PartnerConnection,

    /// <summary>
    /// EDI transaction.
    /// </summary>
    EdiTransaction,

    /// <summary>
    /// Data transfer in bytes.
    /// </summary>
    DataTransfer
}
