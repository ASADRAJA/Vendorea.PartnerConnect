using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class ProcessingResultRecorder : IProcessingResultRecorder
{
    private readonly IIntegrationEventRepository _events;
    private readonly ISalesOrderRepository _orders;
    private readonly ILogger<ProcessingResultRecorder> _logger;

    public ProcessingResultRecorder(
        IIntegrationEventRepository events,
        ISalesOrderRepository orders,
        ILogger<ProcessingResultRecorder> logger)
    {
        _events = events;
        _orders = orders;
        _logger = logger;
    }

    public async Task<ProcessingResultOutcome> RecordAsync(
        int tenantId, SalesOrder order, Guid sourceEventId,
        IntegrationEventProcessingResult outcome, string? consumerOrderId, string? error,
        DateTime? attemptedAt, CancellationToken cancellationToken = default)
    {
        // Step 4. The relationship among event, order and tenant is checked rather than assumed.
        // A caller could otherwise report a result against an event belonging to another order,
        // and the result would land on the wrong one.
        var published = await _events.GetAsync(sourceEventId, cancellationToken);

        if (published is null || published.SalesOrderId != order.Id || published.TenantId != tenantId)
        {
            _logger.LogWarning(
                "A processing result named event {EventId}, which was not published for order {SalesOrderId} on tenant {TenantId}.",
                sourceEventId, order.Id, tenantId);

            return new ProcessingResultOutcome(ProcessingResultDecision.UnknownSourceEvent);
        }

        if (published.ProcessingResult.HasValue)
        {
            var identical = published.ProcessingResult == outcome
                && string.Equals(published.ConsumerOrderId, consumerOrderId, StringComparison.Ordinal);

            if (identical)
            {
                // Step 5. A retried callback is normal and changes nothing.
                return new ProcessingResultOutcome(
                    ProcessingResultDecision.AlreadyRecorded,
                    published.ProcessingResult, published.ProcessingResultAt);
            }

            // Step 6. First result wins. Two different answers for one event means the consumer's
            // handler ran twice with different effects, and overwriting would hide that rather
            // than surface it.
            _logger.LogWarning(
                "A conflicting processing result for event {EventId}: recorded {Recorded}, received {Received}.",
                published.EventId, published.ProcessingResult, outcome);

            return new ProcessingResultOutcome(
                ProcessingResultDecision.Conflicting,
                published.ProcessingResult, published.ProcessingResultAt);
        }

        published.ProcessingResult = outcome;
        published.ProcessingResultAt = attemptedAt ?? DateTime.UtcNow;
        published.ConsumerOrderId = consumerOrderId;
        published.ProcessingError = Truncate(error);

        // Mirrored onto the order so the ordinary read shows the latest outcome without a join.
        order.LastEventId = published.EventId;
        order.LastProcessingResult = outcome switch
        {
            IntegrationEventProcessingResult.Processed => SalesOrderProcessingResult.Processed,
            IntegrationEventProcessingResult.FailedRetryable => SalesOrderProcessingResult.FailedRetryable,
            _ => SalesOrderProcessingResult.FailedPermanent
        };

        await _orders.SaveAsync(cancellationToken);

        if (outcome == IntegrationEventProcessingResult.Processed)
        {
            _logger.LogInformation(
                "Merchant360 processed event {EventId} as sales order {ConsumerOrderId}.",
                published.EventId, consumerOrderId);
        }
        else
        {
            // A real Amazon order with no Merchant360 order behind it is a merchant-visible
            // problem, not a log line to scroll past.
            _logger.LogWarning(
                "Merchant360 reported {Outcome} for event {EventId} on order {SalesOrderId}: {Error}",
                outcome, published.EventId, order.Id, error);
        }

        return new ProcessingResultOutcome(
            ProcessingResultDecision.Recorded, outcome, published.ProcessingResultAt);
    }

    private static string? Truncate(string? value)
        => value is null || value.Length <= 2000 ? value : value[..2000];
}
