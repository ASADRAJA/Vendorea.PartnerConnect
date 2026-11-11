namespace Vendorea.PartnerConnect.Domain.Entities.Integration;

/// <summary>
/// One publication of an integration event to Merchant360, and where it got to.
/// </summary>
/// <remarks>
/// The record behind the five observable states. When a merchant says an Amazon order never
/// appeared, the question is which handoff stopped — did PartnerConnect fetch it, store it,
/// publish it, did Merchant360 acknowledge delivery, and did its handler actually finish? Without
/// a row per publication that is unanswerable without asking Amazon, and asking Amazon costs a
/// rate-limit slot to learn something about our own plumbing.
///
/// The row is staged in the **same transaction** as the order it describes. An order that exists
/// without its event would never reach Merchant360 and nothing would say so; an event without its
/// order would describe something that does not exist.
///
/// <see cref="EventId"/> is unique per publication, not per delivery attempt. Transport retries
/// reuse it so the consumer's inbox dedupes them. A deliberate republication mints a new one and
/// keeps the same order identity — identity governs, the event id never does.
/// </remarks>
public class IntegrationEvent
{
    /// <summary>The envelope's <c>eventId</c>, and the primary key. One row per publication.</summary>
    public Guid EventId { get; set; } = Guid.NewGuid();

    public IntegrationEventType EventType { get; set; }

    public string EventVersion { get; set; } = "v1";

    public int OrganizationId { get; set; }
    public int TenantId { get; set; }
    public Guid IntegrationConnectionId { get; set; }

    /// <summary>The sales order this is about, where the event is about an order.</summary>
    public Guid? SalesOrderId { get; set; }

    /// <summary>
    /// Immutable per state transition, and deliberately not the order identity.
    /// </summary>
    /// <remarks>
    /// Equal across transport retries of one transition and different across genuine transitions,
    /// so a consumer can tell "I have seen this change" from "this is a new change". Derived from
    /// the order plus the status plus the provider timestamp, which is what a transition is.
    /// </remarks>
    public string DeduplicationKey { get; set; } = string.Empty;

    public EventPublicationReason PublicationReason { get; set; } = EventPublicationReason.ProviderChange;

    /// <summary>When the change happened at the provider, where known.</summary>
    public DateTime? OccurredAt { get; set; }

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    public string? CorrelationId { get; set; }

    /// <summary>
    /// The envelope as it was published, byte for byte.
    /// </summary>
    /// <remarks>
    /// Stored because the signature is computed over the raw bytes. A redelivery that
    /// re-serialised the object would change whitespace and key order, and the signature would
    /// no longer match what the consumer verifies against.
    /// </remarks>
    public string EnvelopeJson { get; set; } = string.Empty;

    /// <summary>Which signing key signed it, so a rotated-out key can still be explained.</summary>
    public string? SigningKeyId { get; set; }

    // ---- delivery state ----

    public Guid? OutboxMessageId { get; set; }

    /// <summary>When Merchant360 returned a 2xx for the delivery. Transport only.</summary>
    /// <remarks>
    /// An acknowledgement is **not** a business outcome. A 202 means the bytes arrived, nothing
    /// more — which is why <see cref="ProcessingResult"/> exists separately and why the contract
    /// says a successful acknowledgement must never be read as successful processing.
    /// </remarks>
    public DateTime? DeliveryAcknowledgedAt { get; set; }

    public int DeliveryAttempts { get; set; }

    public DateTime? LastDeliveryAttemptAt { get; set; }

    public string? LastDeliveryError { get; set; }

    // ---- business outcome, reported back by the consumer ----

    public IntegrationEventProcessingResult? ProcessingResult { get; set; }

    public DateTime? ProcessingResultAt { get; set; }

    /// <summary>The consumer's own order id, where it reported one.</summary>
    public string? ConsumerOrderId { get; set; }

    public string? ProcessingError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The furthest state this publication reached.
    /// </summary>
    /// <remarks>
    /// Derived rather than stored. A stored state column and these timestamps would be two
    /// sources of truth for one fact, and they would disagree the first time a write partially
    /// failed.
    /// </remarks>
    public IntegrationEventState State => ProcessingResult switch
    {
        IntegrationEventProcessingResult.Processed => IntegrationEventState.ProcessingComplete,
        IntegrationEventProcessingResult.FailedRetryable => IntegrationEventState.ProcessingFailed,
        IntegrationEventProcessingResult.FailedPermanent => IntegrationEventState.ProcessingFailed,
        _ => DeliveryAcknowledgedAt.HasValue
            ? IntegrationEventState.DeliveryAcknowledged
            : IntegrationEventState.OutboxCreated
    };
}

public enum IntegrationEventType
{
    SalesOrderCreated,
    SalesOrderUpdated,
    SalesOrderFulfillmentStatusChanged,
    ConnectionAttentionRequired,
    SyncFailed
}

/// <summary>Why a publication exists.</summary>
/// <remarks>
/// A redelivery or a reconciliation publication carries a new event id but must update the same
/// order, never create a second one.
/// </remarks>
public enum EventPublicationReason
{
    ProviderChange,
    ManualRedelivery,
    Reconciliation
}

public enum IntegrationEventProcessingResult
{
    Processed,
    FailedRetryable,
    FailedPermanent
}

/// <summary>
/// The five observable states of a handoff.
/// </summary>
/// <remarks>
/// Six values for five states: processing has two outcomes. The first two are properties of the
/// order rather than of any one publication, so the history endpoint reports them from the order
/// and the rest from each event.
/// </remarks>
public enum IntegrationEventState
{
    /// <summary>PartnerConnect read it from the provider.</summary>
    AmazonFetched,

    /// <summary>PartnerConnect persisted it.</summary>
    PcStored,

    /// <summary>An event was published for it.</summary>
    OutboxCreated,

    /// <summary>The consumer returned 2xx for the delivery. Transport only.</summary>
    DeliveryAcknowledged,

    /// <summary>The consumer's handler finished successfully.</summary>
    ProcessingComplete,

    /// <summary>The consumer's handler reported a failure.</summary>
    ProcessingFailed
}
