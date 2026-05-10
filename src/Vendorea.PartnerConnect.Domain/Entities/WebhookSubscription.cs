namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a webhook subscription for a dealer.
/// </summary>
public class WebhookSubscription
{
    public int Id { get; set; }

    /// <summary>
    /// The dealer ID that owns this subscription.
    /// </summary>
    public int DealerId { get; set; }

    /// <summary>
    /// Name/description of the subscription.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL to deliver webhooks to.
    /// </summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    /// Secret for HMAC signature validation.
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Events this subscription is subscribed to.
    /// </summary>
    public IList<string> Events { get; set; } = new List<string>();

    /// <summary>
    /// Whether the subscription is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional filter criteria (JSON).
    /// </summary>
    public string? FilterCriteria { get; set; }

    /// <summary>
    /// Headers to include in webhook requests (JSON).
    /// </summary>
    public string? CustomHeaders { get; set; }

    /// <summary>
    /// When the subscription was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the subscription was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the subscription was last triggered.
    /// </summary>
    public DateTime? LastTriggeredAt { get; set; }

    /// <summary>
    /// Number of successful deliveries.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed deliveries.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Consecutive failures (resets on success).
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Whether the subscription is suspended due to failures.
    /// </summary>
    public bool IsSuspended { get; set; }

    /// <summary>
    /// When the subscription was suspended.
    /// </summary>
    public DateTime? SuspendedAt { get; set; }

    /// <summary>
    /// When the suspension should be lifted.
    /// </summary>
    public DateTime? SuspendedUntil { get; set; }

    /// <summary>
    /// When the last failure occurred.
    /// </summary>
    public DateTime? LastFailureAt { get; set; }

    /// <summary>
    /// Reason for suspension.
    /// </summary>
    public string? SuspensionReason { get; set; }
}

/// <summary>
/// Known webhook event types.
/// </summary>
public static class WebhookEvents
{
    // Document events
    public const string DocumentReceived = "document.received";
    public const string DocumentValidated = "document.validated";
    public const string DocumentFailed = "document.failed";
    public const string DocumentCompleted = "document.completed";
    public const string DocumentQuarantined = "document.quarantined";

    // Price/Inventory events
    public const string PricesUpdated = "prices.updated";
    public const string InventoryUpdated = "inventory.updated";

    // Order events
    public const string OrderReceived = "order.received";
    public const string OrderAcknowledged = "order.acknowledged";
    public const string ShipmentNotice = "shipment.notice";
    public const string InvoiceReceived = "invoice.received";

    // Connection events
    public const string ConnectionActivated = "connection.activated";
    public const string ConnectionDeactivated = "connection.deactivated";
    public const string ConnectionError = "connection.error";

    public static readonly IReadOnlyList<string> All = new[]
    {
        DocumentReceived, DocumentValidated, DocumentFailed, DocumentCompleted, DocumentQuarantined,
        PricesUpdated, InventoryUpdated,
        OrderReceived, OrderAcknowledged, ShipmentNotice, InvoiceReceived,
        ConnectionActivated, ConnectionDeactivated, ConnectionError
    };
}
