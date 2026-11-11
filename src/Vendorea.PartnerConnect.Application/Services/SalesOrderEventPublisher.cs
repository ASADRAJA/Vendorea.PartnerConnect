using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class SalesOrderEventPublisher : ISalesOrderEventPublisher
{
    /// <summary>The outbox message type the processor dispatches on.</summary>
    public const string OutboxMessageType = "IntegrationEventDelivery";

    /// <summary>
    /// Serialised once, and the bytes are kept.
    /// </summary>
    /// <remarks>
    /// The signature is computed over the raw body, so a redelivery that re-serialised the
    /// envelope would change whitespace or key order and the signature would stop matching what
    /// the consumer verifies. Property names come from attributes rather than a naming policy for
    /// the same reason: a convention applies process-wide and this contract must survive one
    /// being changed.
    /// </remarks>
    private static readonly JsonSerializerOptions EnvelopeJson = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private readonly IIntegrationEventRepository _events;
    private readonly IOutboxService _outbox;
    private readonly IntegrationDeliveryOptions _options;
    private readonly ILogger<SalesOrderEventPublisher> _logger;

    public SalesOrderEventPublisher(
        IIntegrationEventRepository events,
        IOutboxService outbox,
        IOptions<IntegrationDeliveryOptions> options,
        ILogger<SalesOrderEventPublisher> logger)
    {
        _events = events;
        _outbox = outbox;
        _options = options.Value;
        _logger = logger;
    }

    public Guid StageForOrder(
        SalesOrder order, IntegrationConnection connection, string providerCode,
        IntegrationEventType eventType, EventPublicationReason reason, string? correlationId)
    {
        var (integrationEvent, _) = Build(order, connection, providerCode, eventType, reason, correlationId);

        _events.Stage(integrationEvent);

        // Staged in the caller's transaction, beside the order and the event row. All three
        // commit together or none of them do.
        integrationEvent.OutboxMessageId = _outbox.EnqueueInCurrentTransaction(
            OutboxMessageType,
            new IntegrationEventDeliveryMessage(integrationEvent.EventId),
            destination: _options.EndpointUrl,
            correlationId: correlationId);

        return integrationEvent.EventId;
    }

    public async Task<Guid> RepublishAsync(
        SalesOrder order, IntegrationConnection connection, string providerCode,
        EventPublicationReason reason, string? correlationId,
        CancellationToken cancellationToken = default)
    {
        // A republication is always an update, never a creation. The consumer keys on order
        // identity, so a second "created" for an order it already holds would be a contradiction
        // rather than new information.
        var eventId = StageForOrder(
            order, connection, providerCode,
            IntegrationEventType.SalesOrderUpdated, reason, correlationId);

        await _events.SaveAsync(cancellationToken);

        _logger.LogInformation(
            "Republished sales order {SalesOrderId} as event {EventId} ({Reason}).",
            order.Id, eventId, reason);

        return eventId;
    }

    private (IntegrationEvent Event, IntegrationEventEnvelope Envelope) Build(
        SalesOrder order, IntegrationConnection connection, string providerCode,
        IntegrationEventType eventType, EventPublicationReason reason, string? correlationId)
    {
        var eventId = Guid.NewGuid();
        var publishedAt = DateTime.UtcNow;
        var correlation = correlationId ?? eventId.ToString();

        var envelope = new IntegrationEventEnvelope
        {
            EventId = eventId,
            EventType = WireEventType(eventType),
            EventVersion = "v1",

            // The provider's timestamp where we have one. "When it happened at Amazon" is a
            // different fact from "when we published", and a consumer ordering by the wrong one
            // would order by our scheduling rather than by the business.
            OccurredAt = order.SourceLastUpdatedAt == default ? publishedAt : order.SourceLastUpdatedAt,
            PublishedAt = publishedAt,

            OrganizationId = connection.OrganizationId,
            TenantId = order.TenantId,
            IntegrationConnectionId = order.IntegrationConnectionId,
            Provider = providerCode,
            MarketplaceId = order.MarketplaceId,
            CorrelationId = correlation,
            DeduplicationKey = DeduplicationKey(order),
            PayloadKind = "snapshot",
            PublicationReason = reason.ToString(),
            Payload = JsonSerializer.SerializeToNode(
                BuildSnapshot(order, providerCode), EnvelopeJson)
        };

        var json = JsonSerializer.Serialize(envelope, EnvelopeJson);

        return (new IntegrationEvent
        {
            EventId = eventId,
            EventType = eventType,
            EventVersion = "v1",
            OrganizationId = connection.OrganizationId,
            TenantId = order.TenantId,
            IntegrationConnectionId = order.IntegrationConnectionId,
            SalesOrderId = order.Id,
            DeduplicationKey = envelope.DeduplicationKey,
            PublicationReason = reason,
            OccurredAt = envelope.OccurredAt,
            PublishedAt = publishedAt,
            CorrelationId = correlation,
            EnvelopeJson = json
        }, envelope);
    }

    /// <summary>
    /// A key that is the same across retries of one transition and different across transitions.
    /// </summary>
    /// <remarks>
    /// Derived from the order and the provider's own view of its state, because that is what a
    /// transition is. Deliberately not the order identity — a consumer needs to tell "I have seen
    /// this change" apart from "this is a new change", and the identity cannot answer either.
    /// Hashed so the key stays a bounded length whatever an order id looks like.
    /// </remarks>
    private static string DeduplicationKey(SalesOrder order)
    {
        var material =
            $"{order.Id:N}|{order.Status}|{order.SourceLastUpdatedAt:O}|{order.ExternalFulfillmentStatusRaw}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant()[..32];
    }

    private SalesOrderSnapshotDto BuildSnapshot(SalesOrder order, string providerCode)
    {
        var snapshot = new SalesOrderSnapshotDto
        {
            Identity = new OrderIdentityDto
            {
                Provider = providerCode,
                IntegrationConnectionId = order.IntegrationConnectionId,
                ExternalOrderId = order.ExternalOrderId
            },
            PcSalesOrderId = order.Id,
            Status = order.Status.ToString(),

            // Required by the schema, so an absent provider status becomes an explicit marker
            // rather than an omitted field that fails validation at the consumer.
            ExternalStatusRaw = order.ExternalStatusRaw ?? "UNKNOWN",
            ExternalFulfillmentStatusRaw = order.ExternalFulfillmentStatusRaw,
            FulfillmentChannel = order.FulfillmentChannel.ToString(),
            MappingStatus = order.MappingStatus.ToString(),
            ProgramsRaw = ParsePrograms(order.ProgramsRawJson),
            PurchaseDate = order.PurchaseDate,
            SourceLastUpdatedAt = order.SourceLastUpdatedAt,
            SourceVersion = order.SourceVersion,
            OrderTotal = EventMoneyDto.From(order.OrderTotalAmount, order.CurrencyCode),
            BuyerDataAvailable = order.BuyerDataAvailable,
            Lines = order.Lines
                .OrderBy(l => l.ExternalLineId)
                .Select(MapLine)
                .ToList()
        };

        // Personal data only when it has been deliberately enabled, and never once the order has
        // been redacted — republishing a redacted order with its address restored would make
        // retention something a redelivery could undo.
        if (_options.IncludeBuyerData && order.BuyerDataAvailable && order.PiiRedactedAt is null)
        {
            snapshot.Buyer = new EventBuyerDto
            {
                EmailRef = order.BuyerEmailRef,
                Name = order.BuyerName
            };

            snapshot.ShipTo = new EventAddressDto
            {
                Name = order.ShipToName,
                Line1 = order.ShipToLine1,
                Line2 = order.ShipToLine2,
                City = order.ShipToCity,
                StateOrRegion = order.ShipToStateOrRegion,
                PostalCode = order.ShipToPostalCode,
                CountryCode = order.ShipToCountryCode
            };
        }

        return snapshot;
    }

    private static SalesOrderLineSnapshotDto MapLine(SalesOrderLine line) => new()
    {
        ExternalLineId = line.ExternalLineId,
        SellerSku = line.SellerSku,
        Asin = line.Asin,
        Title = line.Title,
        QuantityOrdered = line.QuantityOrdered,
        QuantityShipped = line.QuantityShipped,
        MappingStatus = line.MappingStatus.ToString(),

        // A hint, named as one. The consumer owns product identity and must verify rather than
        // trust it — a mapping taken on faith decrements the wrong stock.
        M360CatalogItemIdHint = line.M360CatalogItemId,
        ItemPrice = EventMoneyDto.From(line.ItemPrice, line.CurrencyCode),
        ItemTax = EventMoneyDto.From(line.ItemTax, line.CurrencyCode),
        ShippingPrice = EventMoneyDto.From(line.ShippingPrice, line.CurrencyCode),
        PromotionDiscount = EventMoneyDto.From(line.PromotionDiscount, line.CurrencyCode)
    };

    private static List<string> ParsePrograms(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// The schema's event-type strings.
    /// </summary>
    /// <remarks>
    /// Mapped explicitly rather than derived from the enum name. The wire values are versioned
    /// (<c>sales_order.created.v1</c>) and renaming a C# enum member must not silently change
    /// what a consumer receives.
    /// </remarks>
    private static string WireEventType(IntegrationEventType type) => type switch
    {
        IntegrationEventType.SalesOrderCreated => "sales_order.created.v1",
        IntegrationEventType.SalesOrderUpdated => "sales_order.updated.v1",
        IntegrationEventType.SalesOrderFulfillmentStatusChanged
            => "sales_order.fulfillment_status_changed.v1",
        IntegrationEventType.ConnectionAttentionRequired
            => "integration.connection_attention_required.v1",
        IntegrationEventType.SyncFailed => "integration.sync_failed.v1",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No wire event type is mapped.")
    };
}

/// <summary>
/// The outbox payload: an event id and nothing else.
/// </summary>
/// <remarks>
/// The envelope itself lives on the event row. Putting it in the outbox message too would store
/// the same bytes twice and let them diverge — and the delivery has to sign the stored bytes
/// rather than a copy of them.
/// </remarks>
public sealed record IntegrationEventDeliveryMessage(Guid EventId);
