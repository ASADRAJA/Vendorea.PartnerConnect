using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Application.Interfaces;

public interface IIntegrationEventRepository
{
    /// <summary>
    /// Stages an event without saving, so it commits with the caller's transaction.
    /// </summary>
    /// <remarks>
    /// No <c>SaveChanges</c> here on purpose. An order that committed without its event would
    /// never reach Merchant360 and nothing would record that it had not.
    /// </remarks>
    void Stage(IntegrationEvent integrationEvent);

    Task<IntegrationEvent?> GetAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Publications for one order, newest first.</summary>
    Task<IReadOnlyList<IntegrationEvent>> GetForOrderAsync(
        Guid salesOrderId, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}

/// <summary>Publishes sales-order events to Merchant360.</summary>
public interface ISalesOrderEventPublisher
{
    /// <summary>
    /// Stages an event for an order in the caller's transaction, returning its id.
    /// </summary>
    /// <remarks>
    /// Staged, not sent. The caller's <c>SaveChanges</c> commits the order, the event row and the
    /// outbox message together; delivery happens later from the outbox. That is what makes "the
    /// order exists but Merchant360 never heard about it" impossible rather than unlikely.
    /// </remarks>
    Guid StageForOrder(
        SalesOrder order, IntegrationConnection connection, string providerCode,
        IntegrationEventType eventType, EventPublicationReason reason, string? correlationId);

    /// <summary>
    /// Publishes the current snapshot as a new event, committing immediately.
    /// </summary>
    /// <remarks>
    /// Used by the redelivery endpoint, where there is no enclosing order write to join. A new
    /// event id carrying the current state — it must update the same consumer order, never
    /// create a second, because order identity is the canonical key and the event id never is.
    /// </remarks>
    Task<Guid> RepublishAsync(
        SalesOrder order, IntegrationConnection connection, string providerCode,
        EventPublicationReason reason, string? correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>Resolves the secret that signs an outbound delivery, and the ones that still verify.</summary>
public interface IIntegrationSigningKeyProvider
{
    /// <summary>The key new deliveries are signed with.</summary>
    (string KeyId, string Secret)? Primary { get; }

    /// <summary>
    /// Every key still accepted, primary included.
    /// </summary>
    /// <remarks>
    /// More than one during a rotation overlap. Publishing with the new key while the old one
    /// still verifies is what lets a secret change without a redeploy and without a delivery gap.
    /// </remarks>
    IReadOnlyDictionary<string, string> AcceptedKeys { get; }

    string? Resolve(string? keyId);
}

/// <summary>Signs and delivers one published event to the consumer.</summary>
public interface IIntegrationEventDeliveryService
{
    /// <summary>
    /// Delivers the event, recording the outcome on its row.
    /// </summary>
    /// <remarks>
    /// Returns false for anything the outbox should retry. A 2xx records the acknowledgement and
    /// nothing more: the bytes arrived, which the contract is explicit is not the same as the
    /// business processing having succeeded.
    /// </remarks>
    Task<bool> DeliverAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Records what Merchant360's handler did, applying steps 4 to 6 of the validation ladder.
/// </summary>
/// <remarks>
/// Steps 1 to 3 — credential, organization owns the tenant, order belongs to the tenant — are
/// HTTP concerns and stay in the controller. These three are not: whether the event was really
/// published for this order, whether a result is already recorded, and whether a second result
/// contradicts the first are decisions about the exchange, and they are testable without a web
/// host only if they live here.
/// </remarks>
public interface IProcessingResultRecorder
{
    Task<ProcessingResultOutcome> RecordAsync(
        int tenantId, Domain.Entities.Sales.SalesOrder order, Guid sourceEventId,
        IntegrationEventProcessingResult outcome, string? consumerOrderId, string? error,
        DateTime? attemptedAt, CancellationToken cancellationToken = default);
}

public enum ProcessingResultDecision
{
    /// <summary>Recorded for the first time.</summary>
    Recorded,

    /// <summary>The same result was already recorded. Idempotent; the original stands.</summary>
    AlreadyRecorded,

    /// <summary>That event was never published for this order.</summary>
    UnknownSourceEvent,

    /// <summary>A different result is already recorded. The first one wins.</summary>
    Conflicting
}

public sealed record ProcessingResultOutcome(
    ProcessingResultDecision Decision,
    IntegrationEventProcessingResult? Recorded = null,
    DateTime? RecordedAt = null);
