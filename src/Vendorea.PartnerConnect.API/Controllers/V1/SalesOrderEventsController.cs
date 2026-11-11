using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Event history, redelivery, and the processing-result callback.
/// </summary>
/// <remarks>
/// The feedback half of the Merchant360 exchange. The publishing half runs from the outbox; these
/// endpoints are how Merchant360 reports what happened and how an operator finds out where a
/// handoff stopped.
/// </remarks>
[ApiController]
[Route("api/v1/org/tenants/{tenantId:int}/sales-orders/{pcSalesOrderId:guid}")]
[Produces("application/json")]
[Authorize]
public class SalesOrderEventsController : ControllerBase
{
    private readonly ISalesOrderRepository _orders;
    private readonly IIntegrationEventRepository _events;
    private readonly ISalesOrderEventPublisher _publisher;
    private readonly IProcessingResultRecorder _recorder;
    private readonly IIntegrationConnectionService _connections;
    private readonly ITenantRepository _tenants;
    private readonly IOrgApiKeyAuthenticator _authenticator;
    private readonly ILogger<SalesOrderEventsController> _logger;

    public SalesOrderEventsController(
        ISalesOrderRepository orders,
        IIntegrationEventRepository events,
        ISalesOrderEventPublisher publisher,
        IProcessingResultRecorder recorder,
        IIntegrationConnectionService connections,
        ITenantRepository tenants,
        IOrgApiKeyAuthenticator authenticator,
        ILogger<SalesOrderEventsController> logger)
    {
        _orders = orders;
        _events = events;
        _publisher = publisher;
        _recorder = recorder;
        _connections = connections;
        _tenants = tenants;
        _authenticator = authenticator;
        _logger = logger;
    }

    /// <summary>
    /// The five observable states for every publication of one order.
    /// </summary>
    /// <remarks>
    /// So Merchant360 — or an operator — can determine where a handoff stopped without asking
    /// Amazon. Asking Amazon costs a rate-limit slot to learn something about our own plumbing.
    /// </remarks>
    [HttpGet("events")]
    [RequireScope(ApiScopes.OrdersRead)]
    [ProducesResponseType(typeof(IReadOnlyList<EventHistoryEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvents(
        int tenantId, Guid pcSalesOrderId, CancellationToken cancellationToken)
    {
        var (order, error) = await ResolveOrderAsync(tenantId, pcSalesOrderId, cancellationToken);
        if (order is null) return error!;

        var events = await _events.GetForOrderAsync(order.Id, cancellationToken);
        var entries = new List<EventHistoryEntryDto>();

        // The first two states belong to the order, not to any one publication — PartnerConnect
        // fetched and stored it once, however many times it has been published since.
        entries.Add(new EventHistoryEntryDto
        {
            EventId = null,
            EventType = "order",
            State = nameof(IntegrationEventState.AmazonFetched),
            At = order.FirstSeenAt,
            Detail = $"Provider timestamp {order.SourceLastUpdatedAt:u}"
        });

        entries.Add(new EventHistoryEntryDto
        {
            EventId = null,
            EventType = "order",
            State = nameof(IntegrationEventState.PcStored),
            At = order.FirstSeenAt,
            Detail = $"Status {order.Status}"
        });

        foreach (var published in events)
        {
            entries.Add(new EventHistoryEntryDto
            {
                EventId = published.EventId,
                EventType = published.EventType.ToString(),
                State = nameof(IntegrationEventState.OutboxCreated),
                At = published.PublishedAt,
                Detail = $"{published.PublicationReason}, dedupe {published.DeduplicationKey}"
            });

            if (published.DeliveryAcknowledgedAt.HasValue)
            {
                entries.Add(new EventHistoryEntryDto
                {
                    EventId = published.EventId,
                    EventType = published.EventType.ToString(),
                    State = nameof(IntegrationEventState.DeliveryAcknowledged),

                    // Transport only. Said plainly, because reading an acknowledgement as a
                    // business outcome is the specific mistake the contract warns about.
                    Detail = "Accepted for processing; not yet a business outcome",
                    At = published.DeliveryAcknowledgedAt.Value
                });
            }
            else if (published.DeliveryAttempts > 0)
            {
                entries.Add(new EventHistoryEntryDto
                {
                    EventId = published.EventId,
                    EventType = published.EventType.ToString(),
                    State = nameof(IntegrationEventState.OutboxCreated),
                    At = published.LastDeliveryAttemptAt ?? published.PublishedAt,
                    Detail = $"{published.DeliveryAttempts} delivery attempt(s) failed: {published.LastDeliveryError}"
                });
            }

            if (published.ProcessingResult.HasValue)
            {
                entries.Add(new EventHistoryEntryDto
                {
                    EventId = published.EventId,
                    EventType = published.EventType.ToString(),
                    State = published.ProcessingResult == IntegrationEventProcessingResult.Processed
                        ? nameof(IntegrationEventState.ProcessingComplete)
                        : nameof(IntegrationEventState.ProcessingFailed),
                    At = published.ProcessingResultAt ?? published.PublishedAt,
                    Detail = published.ProcessingResult == IntegrationEventProcessingResult.Processed
                        ? $"Merchant360 order {published.ConsumerOrderId}"
                        : $"{published.ProcessingResult}: {published.ProcessingError}"
                });
            }
        }

        return Ok(entries.OrderByDescending(e => e.At).ToList());
    }

    /// <summary>
    /// Re-publishes the current snapshot as a new event.
    /// </summary>
    /// <remarks>
    /// A new event id carrying the current state. It must update the same Merchant360 order and
    /// never create a second one — order identity is the canonical key and the event id never is.
    /// The event type is <c>updated</c> for the same reason: a second "created" for an order the
    /// consumer already holds is a contradiction, not new information.
    /// </remarks>
    [HttpPost("redeliver")]
    [RequireScope(ApiScopes.OrdersWrite)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Redeliver(
        int tenantId, Guid pcSalesOrderId, CancellationToken cancellationToken)
    {
        var (order, error) = await ResolveOrderAsync(tenantId, pcSalesOrderId, cancellationToken);
        if (order is null) return error!;

        var connection = await _connections.GetAsync(
            tenantId, order.IntegrationConnectionId, cancellationToken);

        if (connection is null)
        {
            return UnprocessableEntity(new
            {
                code = "CONNECTION_UNAVAILABLE",
                message = "The connection this order came through is no longer available."
            });
        }

        var eventId = await _publisher.RepublishAsync(
            order, connection, connection.Provider?.Code ?? "AMAZON_SP",
            EventPublicationReason.ManualRedelivery,
            correlationId: HttpContext.TraceIdentifier,
            cancellationToken);

        _logger.LogInformation(
            "Queued a manual redelivery of sales order {SalesOrderId} as event {EventId}.",
            order.Id, eventId);

        return Accepted(new { eventId });
    }

    /// <summary>
    /// Merchant360 reports what its handler actually did. The primary feedback path.
    /// </summary>
    /// <remarks>
    /// This is how PartnerConnect learns whether an accepted event became a sales order. A
    /// delivery acknowledgement says the bytes arrived; only this says the business processing
    /// happened.
    ///
    /// The validation ladder runs in a fixed order, and the order is the point — each step
    /// assumes the previous one held:
    ///
    /// 1. Credential resolves to an active organization, else 401.
    /// 2. The caller's organization owns <c>tenantId</c>, else **404** — not 403, so tenant ids
    ///    cannot be probed.
    /// 3. <c>pcSalesOrderId</c> belongs to that tenant, else 404.
    /// 4. <c>sourceEventId</c> was actually published for that order, else 409. A result for an
    ///    event we never sent is not something to record.
    /// 5. A duplicate identical result returns 200 and keeps the original.
    /// 6. A conflicting result for the same event returns 409, and the first result wins.
    /// </remarks>
    [HttpPost("processing-result")]
    [RequireScope(ApiScopes.OrdersWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReportProcessingResult(
        int tenantId, Guid pcSalesOrderId,
        [FromBody] ProcessingResultDto request,
        CancellationToken cancellationToken)
    {
        // Steps 1-3.
        var (order, error) = await ResolveOrderAsync(tenantId, pcSalesOrderId, cancellationToken);
        if (order is null) return error!;

        if (request is null
            || request.SourceEventId == Guid.Empty
            || !Enum.TryParse<IntegrationEventProcessingResult>(
                request.Outcome, ignoreCase: true, out var outcome))
        {
            return UnprocessableEntity(new
            {
                code = "VALIDATION_FAILED",
                message = "sourceEventId and a valid outcome are required.",
                allowedOutcomes = Enum.GetNames<IntegrationEventProcessingResult>()
            });
        }

        // Steps 4 to 6 live in the recorder: whether the event was really published for this
        // order, whether a result is already recorded, and whether a second one contradicts the
        // first are decisions about the exchange rather than about HTTP.
        var decision = await _recorder.RecordAsync(
            tenantId, order, request.SourceEventId, outcome,
            request.M360SalesOrderId, request.Error, request.AttemptedAt, cancellationToken);

        return decision.Decision switch
        {
            ProcessingResultDecision.Recorded => NoContent(),

            ProcessingResultDecision.AlreadyRecorded => Ok(new
            {
                code = "ALREADY_RECORDED",
                outcome = decision.Recorded?.ToString(),
                recordedAt = decision.RecordedAt
            }),

            ProcessingResultDecision.UnknownSourceEvent => Conflict(new
            {
                code = "UNKNOWN_SOURCE_EVENT",
                message = "That event was not published for this order."
            }),

            _ => Conflict(new
            {
                code = "CONFLICTING_RESULT",
                message = "A different result is already recorded for this event. The first result stands.",
                recorded = decision.Recorded?.ToString(),
                recordedAt = decision.RecordedAt
            })
        };
    }

    // ---- helpers ----

    /// <summary>
    /// Runs steps 1 to 3 of the ladder and returns the order, or the response that replaces it.
    /// </summary>
    private async Task<(Domain.Entities.Sales.SalesOrder? Order, IActionResult? Error)> ResolveOrderAsync(
        int tenantId, Guid pcSalesOrderId, CancellationToken cancellationToken)
    {
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        var org = await _authenticator.ResolveActiveOrganizationAsync(apiKey, cancellationToken);

        if (org is null)
        {
            return (null, Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid or missing API key" }));
        }

        var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null || tenant.OrganizationId != org.Id)
        {
            // 404 and not 403, so a caller cannot enumerate tenants by watching status codes.
            return (null, NotFound(new { code = "NOT_FOUND", message = $"Tenant '{tenantId}' not found" }));
        }

        var order = await _orders.GetAsync(tenantId, pcSalesOrderId, cancellationToken);
        return order is null
            ? (null, NotFound(new { code = "NOT_FOUND", message = "Sales order not found" }))
            : (order, null);
    }

    private static string? Truncate(string? value)
        => value is null || value.Length <= 2000 ? value : value[..2000];
}

/// <summary>One observable state of one handoff.</summary>
public class EventHistoryEntryDto
{
    /// <summary>Null for the order-level states, which belong to no single publication.</summary>
    public Guid? EventId { get; set; }

    public string EventType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public string? Detail { get; set; }
}
