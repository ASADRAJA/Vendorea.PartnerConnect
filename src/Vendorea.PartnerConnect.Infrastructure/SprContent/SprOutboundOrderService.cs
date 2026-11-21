using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent;

/// <summary>
/// Dispatches a PartnerConnect order to SPR as an outbound EZPO4 PO. See
/// <see cref="ISprOutboundOrderService"/> for the contract.
/// </summary>
public class SprOutboundOrderService : ISprOutboundOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ISprXmlDocumentProcessingService _processingService;
    private readonly ILogger<SprOutboundOrderService> _logger;

    public SprOutboundOrderService(
        IOrderRepository orderRepository,
        ITradingPartnerRepository partnerRepository,
        ISprXmlDocumentProcessingService processingService,
        ILogger<SprOutboundOrderService> logger)
    {
        _orderRepository = orderRepository;
        _partnerRepository = partnerRepository;
        _processingService = processingService;
        _logger = logger;
    }

    public async Task<SprTransmitResult> TransmitOrderAsync(
        int orderId, bool force = false, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdWithFullDetailsAsync(orderId, cancellationToken);
        if (order == null)
            return SprTransmitResult.NotFoundResult();

        // Order.CanTransmit owns the rule; this is not a copy of it. The endpoint has to apply it
        // too, because hiding the button only stops the portal - the duplicate is created by the
        // call, not by the click.
        if (!order.CanTransmit && !force)
        {
            return new SprTransmitResult
            {
                InvalidState = true,
                ErrorMessage = order.TransmitBlockedReason
                    ?? $"Order in status {order.Status} cannot be transmitted to SPR"
            };
        }

        if (force && !order.CanTransmit)
        {
            // Logged at Warning with the reason that was overridden: if a duplicate PO turns up
            // at SPR, this line is the record of who sent it and what they overrode.
            _logger.LogWarning(
                "Forced transmit of order {OrderId} (PO {PoNumber}) in status {Status}: {Reason} " +
                "A second EZPO4 with the same CustomerPONo is a duplicate order at SPR.",
                order.Id, order.PoNumber, order.Status, order.TransmitBlockedReason);
        }

        var partner = await ResolveSprPartnerAsync(order, cancellationToken);
        if (partner == null)
        {
            return new SprTransmitResult
            {
                ErrorMessage = "No SPR trading partner is configured for this order's trading partner"
            };
        }

        var purchaseOrder = OrderToPurchaseOrderMapper.Map(order);

        // BuyerOrganizationCode on the EZPO4 is the tenant's SPR account number, held per-connection
        // (SpecialIdentifyingCode — the same value used as the web-service GroupCode).
        var buyerOrgCode = order.TenantPartnerAccount?.SpecialIdentifyingCode;

        // Generate + strictly validate the EZPO4 against the real SPR schema.
        var createResult = await _processingService.CreateOutboundOrderAsync(
            partner.Id, purchaseOrder, buyerOrgCode, cancellationToken);

        if (!createResult.Success || createResult.SprXmlDocumentId == null)
        {
            var message = createResult.ErrorMessage ?? "Outbound EZPO4 generation/validation failed";
            await MarkOrderFailedAsync(order, message, cancellationToken);
            return new SprTransmitResult
            {
                ValidationFailed = true,
                Errors = createResult.Errors,
                ErrorMessage = message
            };
        }

        // Send over SFTP (direct upload, one PO per file).
        var sendResult = await _processingService.SendOutboundDocumentAsync(
            createResult.SprXmlDocumentId.Value, cancellationToken);

        if (!sendResult.Success)
        {
            // Treat a transport failure as transient: leave the order in its current status
            // so it can be retried via the /transmit endpoint.
            await AddHistoryAsync(order, order.Status, order.Status,
                $"EZPO4 send to SPR failed: {sendResult.ErrorMessage}", cancellationToken);
            return new SprTransmitResult
            {
                DocumentId = createResult.PartnerDocumentId,
                ErrorMessage = sendResult.ErrorMessage ?? "SFTP send failed"
            };
        }

        var previousStatus = order.Status;
        order.Status = OrderStatus.Processing;
        order.EdiDocumentId = createResult.PartnerDocumentId;
        order.SubmittedAt ??= DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        // Record connection usage so the portal's "Last sync" reflects the outbound exchange.
        // TenantPartnerAccount is eager-loaded on the order and shares this scope's DbContext, so
        // the change is persisted by the order save below.
        if (order.TenantPartnerAccount is not null)
            order.TenantPartnerAccount.LastUsedAt = DateTime.UtcNow;

        await _orderRepository.UpdateAsync(order, cancellationToken);
        await AddHistoryAsync(order, previousStatus, OrderStatus.Processing,
            $"EZPO4 transmitted to SPR (PO {order.PoNumber})", cancellationToken);

        _logger.LogInformation(
            "Transmitted order {OrderId} (PO {PoNumber}) to SPR as document {DocumentId}",
            order.Id, order.PoNumber, createResult.PartnerDocumentId);

        return new SprTransmitResult
        {
            Success = true,
            DocumentId = createResult.PartnerDocumentId
        };
    }

    private async Task<TradingPartner?> ResolveSprPartnerAsync(
        Order order, CancellationToken cancellationToken)
    {
        // Transport now lives on the trading partner; resolve it directly from the order.
        return await _partnerRepository.GetByIdAsync(order.TradingPartnerId, cancellationToken);
    }

    private async Task MarkOrderFailedAsync(Order order, string error, CancellationToken cancellationToken)
    {
        var previousStatus = order.Status;
        order.Status = OrderStatus.Failed;
        order.ErrorMessage = error;
        order.UpdatedAt = DateTime.UtcNow;
        await _orderRepository.UpdateAsync(order, cancellationToken);
        await AddHistoryAsync(order, previousStatus, OrderStatus.Failed,
            $"EZPO4 transmit failed: {error}", cancellationToken);
    }

    private Task AddHistoryAsync(
        Order order, OrderStatus from, OrderStatus to, string reason, CancellationToken cancellationToken)
    {
        return _orderRepository.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedAt = DateTime.UtcNow,
            Reason = reason
        }, cancellationToken);
    }
}
