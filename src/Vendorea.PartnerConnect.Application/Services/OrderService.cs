using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITenantManagementService _tenantManagementService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        ITenantManagementService tenantManagementService,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _tenantManagementService = tenantManagementService;
        _logger = logger;
    }

    public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Validate order context (org, tenant, account)
        var validation = await _tenantManagementService.ValidateOrderContextAsync(
            request.OrganizationId,
            request.TenantId,
            request.TradingPartnerId,
            request.AccountNumber,
            cancellationToken);

        if (!validation.IsValid)
        {
            return OrderResult.Failed(validation.ErrorCode!, validation.ErrorMessage!);
        }

        // 2. Validate lines
        if (request.Lines.Count == 0)
        {
            return OrderResult.Failed("NO_LINES", "Order must have at least one line item");
        }

        // 3. Create order
        var order = new Order
        {
            OrganizationId = request.OrganizationId,
            TenantId = request.TenantId,
            TradingPartnerId = request.TradingPartnerId,
            TenantPartnerAccountId = validation.Account!.Id,
            PoNumber = request.PoNumber,
            Status = OrderStatus.Submitted,
            OrderDate = DateTime.UtcNow,
            RequestedShipDate = request.RequestedShipDate,
            RequestedDeliveryDate = request.RequestedDeliveryDate,
            ShipToJson = request.ShipTo != null ? JsonSerializer.Serialize(request.ShipTo) : null,
            BillToJson = request.BillTo != null ? JsonSerializer.Serialize(request.BillTo) : null,
            ShippingMethod = request.ShippingMethod,
            Notes = request.Notes,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow
        };

        // 4. Create order lines
        decimal subTotal = 0;
        int lineNumber = 1;
        foreach (var lineRequest in request.Lines)
        {
            var lineTotal = lineRequest.Quantity * lineRequest.UnitPrice;
            var line = new OrderLine
            {
                LineNumber = lineNumber++,
                Sku = lineRequest.Sku,
                VendorSku = lineRequest.VendorSku,
                Upc = lineRequest.Upc,
                Description = lineRequest.Description,
                Quantity = lineRequest.Quantity,
                UnitOfMeasure = lineRequest.UnitOfMeasure,
                UnitPrice = lineRequest.UnitPrice,
                LineTotal = lineTotal,
                Status = OrderLineStatus.Pending,
                Notes = lineRequest.Notes,
                CreatedAt = DateTime.UtcNow
            };
            order.Lines.Add(line);
            subTotal += lineTotal;
        }

        order.SubTotal = subTotal;
        order.TotalAmount = subTotal + order.TaxAmount + order.ShippingAmount;

        // 5. Save order
        order = await _orderRepository.AddAsync(order, cancellationToken);

        // 6. Record initial status history
        var statusHistory = new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = null,
            ToStatus = OrderStatus.Submitted,
            ChangedAt = DateTime.UtcNow,
            Source = "API",
            Reason = "Order created"
        };
        await _orderRepository.AddStatusHistoryAsync(statusHistory, cancellationToken);

        _logger.LogInformation(
            "Created order {OrderId} (PO: {PoNumber}) for tenant {TenantId} with {LineCount} lines, total: {Total}",
            order.Id, order.PoNumber, order.TenantId, order.Lines.Count, order.TotalAmount);

        return OrderResult.Succeeded(order);
    }

    public async Task<Order?> GetOrderAsync(int orderId, int tenantId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order == null || order.TenantId != tenantId)
        {
            return null;
        }
        return order;
    }

    public async Task<Order?> GetOrderWithDetailsAsync(int orderId, int tenantId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdWithFullDetailsAsync(orderId, cancellationToken);
        if (order == null || order.TenantId != tenantId)
        {
            return null;
        }
        return order;
    }

    public async Task<OrderListResult> GetOrdersAsync(
        int tenantId,
        OrderStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await _orderRepository.GetCountByTenantIdAsync(tenantId, status, cancellationToken);
        var offset = (page - 1) * pageSize;
        var orders = await _orderRepository.GetByTenantIdAsync(tenantId, status, pageSize, offset, cancellationToken);

        return new OrderListResult
        {
            Orders = orders,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<OrderResult> CancelOrderAsync(int orderId, int tenantId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            return OrderResult.Failed("ORDER_NOT_FOUND", $"Order with ID {orderId} not found");
        }

        if (order.TenantId != tenantId)
        {
            return OrderResult.Failed("ORDER_NOT_FOUND", $"Order with ID {orderId} not found");
        }

        // Check if order can be cancelled
        var cancellableStatuses = new[] { OrderStatus.Draft, OrderStatus.Submitted };
        if (!cancellableStatuses.Contains(order.Status))
        {
            return OrderResult.Failed("CANNOT_CANCEL", $"Order cannot be cancelled (status: {order.Status})");
        }

        var previousStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = reason;
        order.UpdatedAt = DateTime.UtcNow;

        await _orderRepository.UpdateAsync(order, cancellationToken);

        // Record status history
        var statusHistory = new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = OrderStatus.Cancelled,
            ChangedAt = DateTime.UtcNow,
            Source = "API",
            Reason = reason ?? "Cancelled by user"
        };
        await _orderRepository.AddStatusHistoryAsync(statusHistory, cancellationToken);

        _logger.LogInformation("Cancelled order {OrderId} (PO: {PoNumber})", order.Id, order.PoNumber);

        return OrderResult.Succeeded(order);
    }

    public async Task<OrderResult> UpdateOrderStatusAsync(
        int orderId,
        OrderStatus newStatus,
        string? source = null,
        string? reason = null,
        int? ediDocumentId = null,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            return OrderResult.Failed("ORDER_NOT_FOUND", $"Order with ID {orderId} not found");
        }

        var previousStatus = order.Status;
        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        // Update timestamps based on status
        switch (newStatus)
        {
            case OrderStatus.Acknowledged:
                order.AcknowledgedAt = DateTime.UtcNow;
                order.AcknowledgmentDocumentId = ediDocumentId;
                break;
            case OrderStatus.Shipped:
            case OrderStatus.PartiallyShipped:
                order.ShippedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Completed:
                order.CompletedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Cancelled:
                order.CancelledAt = DateTime.UtcNow;
                order.CancellationReason = reason;
                break;
            case OrderStatus.Failed:
                order.ErrorMessage = reason;
                break;
        }

        await _orderRepository.UpdateAsync(order, cancellationToken);

        // Record status history
        var statusHistory = new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            Source = source,
            Reason = reason,
            EdiDocumentId = ediDocumentId
        };
        await _orderRepository.AddStatusHistoryAsync(statusHistory, cancellationToken);

        _logger.LogInformation(
            "Updated order {OrderId} status from {PreviousStatus} to {NewStatus} (source: {Source})",
            order.Id, previousStatus, newStatus, source);

        return OrderResult.Succeeded(order);
    }
}
