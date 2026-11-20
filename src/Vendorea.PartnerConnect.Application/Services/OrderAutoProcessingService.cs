using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Outbox message types for automatic order processing.
/// </summary>
public static class OrderAutoProcessingMessageTypes
{
    public const string Transmit = "OrderAutoTransmit";
}

/// <summary>Outbox envelope naming the order to transmit.</summary>
public record OrderAutoTransmitPayload
{
    public int OrderId { get; init; }
}

/// <summary>
/// Acknowledges a newly submitted order and queues its dispatch to the supplier, for tenants set
/// to <see cref="OrderProcessingMode.Automatic"/>.
/// </summary>
/// <remarks>
/// Two decisions are load-bearing here.
///
/// The transmission is <b>queued, not performed</b>. Transmitting means generating an EZPO4,
/// validating it against the XSD and pushing it over SFTP to the supplier. Doing that inside the
/// request that submitted the order would put a third party's availability on the submitter's
/// critical path: when SPR's SFTP was unreachable for 86 days, every inbound order would have
/// hung for the connection timeout and then failed, with the order already saved. Going through
/// the outbox means acceptance stays fast and the dispatch gets retry and backoff it would not
/// otherwise have.
///
/// Acknowledgement happens <b>before</b> the message is staged. If the enqueue then fails, the
/// order sits Acknowledged and an admin can transmit it by hand — the same position a Manual
/// tenant's order is in. The other order of operations risks a queued transmission for an order
/// whose acknowledgement was rolled back, which would dispatch an order no record says was
/// accepted.
/// </remarks>
public class OrderAutoProcessingService : IOrderAutoProcessingService
{
    private readonly ITenantRepository _tenants;
    private readonly IOrderRepository _orders;
    private readonly IOutboxService _outbox;
    private readonly ILogger<OrderAutoProcessingService> _logger;

    public OrderAutoProcessingService(
        ITenantRepository tenants,
        IOrderRepository orders,
        IOutboxService outbox,
        ILogger<OrderAutoProcessingService> logger)
    {
        _tenants = tenants;
        _orders = orders;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<bool> TryAutoProcessAsync(Order order, CancellationToken cancellationToken = default)
    {
        try
        {
            // Only a Submitted order is a candidate. Anything else has already moved on, and
            // re-acknowledging it would rewrite history that something else owns.
            if (order.Status != OrderStatus.Submitted)
            {
                return false;
            }

            var tenant = await _tenants.GetByIdAsync(order.TenantId, cancellationToken);
            if (tenant is null)
            {
                _logger.LogWarning(
                    "Order {OrderId} has no tenant {TenantId}; leaving it for manual handling",
                    order.Id, order.TenantId);
                return false;
            }

            if (tenant.OrderProcessingMode != OrderProcessingMode.Automatic)
            {
                return false;
            }

            var previousStatus = order.Status;
            order.Status = OrderStatus.Acknowledged;
            order.AcknowledgedAt = DateTime.UtcNow;
            await _orders.UpdateAsync(order, cancellationToken);

            await _orders.AddStatusHistoryAsync(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = previousStatus,
                ToStatus = OrderStatus.Acknowledged,
                ChangedAt = DateTime.UtcNow,
                Source = "Auto",
                Reason = "Auto-acknowledged: tenant order processing is Automatic"
            }, cancellationToken);

            await _outbox.EnqueueAsync(
                OrderAutoProcessingMessageTypes.Transmit,
                new OrderAutoTransmitPayload { OrderId = order.Id },
                correlationId: order.PoNumber,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Auto-acknowledged order {OrderId} (PO {PoNumber}) for tenant {TenantId} and queued its transmission",
                order.Id, order.PoNumber, order.TenantId);

            return true;
        }
        catch (Exception ex)
        {
            // Deliberately swallowed. The order is already accepted and persisted; failing the
            // submission now would tell the submitter the order does not exist when it does.
            // Manual acknowledge and transmit remain available, which is the Manual path.
            _logger.LogError(ex,
                "Automatic processing failed for order {OrderId}; it remains available for manual handling",
                order.Id);
            return false;
        }
    }
}
