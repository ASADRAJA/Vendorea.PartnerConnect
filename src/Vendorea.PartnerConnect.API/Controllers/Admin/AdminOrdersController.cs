using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for managing orders.
/// </summary>
[ApiController]
[Route("api/admin/orders")]
[AllowAnonymous] // TODO: Restore [Authorize(Policy = "RequireSystemAdmin")] in production
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ISprOutboundOrderService _outboundOrderService;
    private readonly ILogger<AdminOrdersController> _logger;

    public AdminOrdersController(
        IOrderRepository orderRepository,
        ISprOutboundOrderService outboundOrderService,
        ILogger<AdminOrdersController> logger)
    {
        _orderRepository = orderRepository;
        _outboundOrderService = outboundOrderService;
        _logger = logger;
    }

    /// <summary>
    /// Gets orders with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int? organizationId,
        [FromQuery] int? tenantId,
        [FromQuery] string? status,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Order> orders;
        int totalCount;

        OrderStatus? orderStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var parsed))
        {
            orderStatus = parsed;
        }

        if (tenantId.HasValue)
        {
            orders = await _orderRepository.GetByTenantIdAsync(tenantId.Value, orderStatus, take, skip, cancellationToken);
            totalCount = await _orderRepository.GetCountByTenantIdAsync(tenantId.Value, orderStatus, cancellationToken);
        }
        else if (organizationId.HasValue)
        {
            orders = await _orderRepository.GetByOrganizationIdAsync(organizationId.Value, orderStatus, take, skip, cancellationToken);
            totalCount = await _orderRepository.GetCountAsync(organizationId.Value, null, orderStatus, cancellationToken);
        }
        else
        {
            orders = await _orderRepository.GetAllAsync(orderStatus, take, skip, cancellationToken);
            totalCount = await _orderRepository.GetCountAsync(null, null, orderStatus, cancellationToken);
        }

        // Get counts by status for the result header
        var submittedCount = await _orderRepository.GetCountAsync(organizationId, tenantId, OrderStatus.Submitted, cancellationToken);
        var processingCount = await _orderRepository.GetCountAsync(organizationId, tenantId, OrderStatus.Acknowledged, cancellationToken) +
                              await _orderRepository.GetCountAsync(organizationId, tenantId, OrderStatus.Processing, cancellationToken);
        var completedCount = await _orderRepository.GetCountAsync(organizationId, tenantId, OrderStatus.Completed, cancellationToken) +
                             await _orderRepository.GetCountAsync(organizationId, tenantId, OrderStatus.Shipped, cancellationToken);
        var cancelledCount = await _orderRepository.GetCountAsync(organizationId, tenantId, OrderStatus.Cancelled, cancellationToken);

        var orderDtos = orders.Select(MapToDto).ToList();

        return Ok(new OrderListResult
        {
            Total = totalCount,
            SubmittedCount = submittedCount,
            ProcessingCount = processingCount,
            CompletedCount = completedCount,
            CancelledCount = cancelledCount,
            DraftCount = await _orderRepository.GetCountAsync(organizationId, tenantId, OrderStatus.Draft, cancellationToken),
            Items = orderDtos
        });
    }

    /// <summary>
    /// Gets an order by ID with full details.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrder(int id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdWithFullDetailsAsync(id, cancellationToken);
        if (order == null)
            return NotFound();

        return Ok(MapToDetailDto(order));
    }

    /// <summary>
    /// Acknowledges an order.
    /// </summary>
    [HttpPost("{id:int}/acknowledge")]
    public async Task<IActionResult> AcknowledgeOrder(int id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken);
        if (order == null)
            return NotFound();

        if (order.Status != OrderStatus.Submitted)
            return BadRequest(new { error = "Order must be in Submitted status to acknowledge" });

        var previousStatus = order.Status;
        order.Status = OrderStatus.Acknowledged;
        order.AcknowledgedAt = DateTime.UtcNow;

        await _orderRepository.UpdateAsync(order, cancellationToken);

        // Add status history
        await _orderRepository.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = OrderStatus.Acknowledged,
            ChangedAt = DateTime.UtcNow,
            Reason = "Acknowledged by admin"
        }, cancellationToken);

        _logger.LogInformation("Acknowledged order {OrderId}", id);

        return NoContent();
    }

    /// <summary>
    /// Transmits the order to SPR as an outbound EZPO4 PO (generate -> strict XSD validate ->
    /// SFTP send) and advances it to Processing. This is the explicit supplier-dispatch action
    /// and is also the manual retry path; it is intentionally separate from acknowledge.
    /// </summary>
    [HttpPost("{id:int}/transmit")]
    public async Task<IActionResult> TransmitOrder(int id, CancellationToken cancellationToken)
    {
        var result = await _outboundOrderService.TransmitOrderAsync(id, cancellationToken);

        if (result.NotFound)
            return NotFound();

        if (result.InvalidState)
            return BadRequest(new { error = result.ErrorMessage });

        if (result.ValidationFailed)
            return UnprocessableEntity(new { error = result.ErrorMessage, errors = result.Errors });

        if (!result.Success)
            return StatusCode(StatusCodes.Status502BadGateway, new { error = result.ErrorMessage });

        _logger.LogInformation("Transmitted order {OrderId} to SPR (document {DocumentId})", id, result.DocumentId);
        return Ok(new { documentId = result.DocumentId, status = "Processing" });
    }

    /// <summary>
    /// Cancels an order.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelOrder(int id, [FromBody] CancelOrderRequest? request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken);
        if (order == null)
            return NotFound();

        var cancellableStatuses = new[] { OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Acknowledged };
        if (!cancellableStatuses.Contains(order.Status))
            return BadRequest(new { error = "Order cannot be cancelled in its current status" });

        var previousStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = request?.Reason;

        await _orderRepository.UpdateAsync(order, cancellationToken);

        // Add status history
        await _orderRepository.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = OrderStatus.Cancelled,
            ChangedAt = DateTime.UtcNow,
            Reason = request?.Reason ?? "Cancelled by admin"
        }, cancellationToken);

        _logger.LogInformation("Cancelled order {OrderId}", id);

        return NoContent();
    }

    private static OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrganizationId = order.OrganizationId,
            OrganizationName = order.Organization?.Name,
            TenantId = order.TenantId,
            TenantName = order.Tenant?.Name,
            TradingPartnerId = order.TradingPartnerId,
            TradingPartnerName = order.TradingPartner?.Name,
            AccountNumber = order.TenantPartnerAccount?.AccountNumber ?? "",
            PoNumber = order.PoNumber,
            Status = order.Status.ToString(),
            SubTotal = order.SubTotal,
            TaxAmount = order.TaxAmount,
            ShippingAmount = order.ShippingAmount,
            TotalAmount = order.TotalAmount,
            LineCount = order.Lines?.Count ?? 0,
            OrderDate = order.OrderDate,
            SubmittedAt = order.SubmittedAt,
            AcknowledgedAt = order.AcknowledgedAt
        };
    }

    private static OrderDetailDto MapToDetailDto(Order order)
    {
        var dto = new OrderDetailDto
        {
            Id = order.Id,
            OrganizationId = order.OrganizationId,
            OrganizationName = order.Organization?.Name,
            TenantId = order.TenantId,
            TenantName = order.Tenant?.Name,
            TradingPartnerId = order.TradingPartnerId,
            TradingPartnerName = order.TradingPartner?.Name,
            AccountNumber = order.TenantPartnerAccount?.AccountNumber ?? "",
            PoNumber = order.PoNumber,
            Status = order.Status.ToString(),
            SubTotal = order.SubTotal,
            TaxAmount = order.TaxAmount,
            ShippingAmount = order.ShippingAmount,
            TotalAmount = order.TotalAmount,
            LineCount = order.Lines?.Count ?? 0,
            OrderDate = order.OrderDate,
            SubmittedAt = order.SubmittedAt,
            AcknowledgedAt = order.AcknowledgedAt,
            RequestedShipDate = order.RequestedShipDate,
            RequestedDeliveryDate = order.RequestedDeliveryDate,
            ShippingMethod = order.ShippingMethod,
            Notes = order.Notes,
            Lines = order.Lines?.Select(l => new OrderLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                Sku = l.Sku,
                VendorSku = l.VendorSku,
                Description = l.Description,
                QuantityOrdered = (int)l.Quantity,
                QuantityShipped = (int)(l.ShippedQuantity ?? 0),
                QuantityCancelled = l.Status == OrderLineStatus.Cancelled ? (int)l.Quantity : 0,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
                Status = l.Status.ToString()
            }).ToList() ?? new(),
            StatusHistory = order.StatusHistory?.Select(h => new OrderStatusHistoryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus?.ToString() ?? "",
                ToStatus = h.ToStatus.ToString(),
                ChangedByUserId = h.ChangedBy,
                Reason = h.Reason,
                ChangedAt = h.ChangedAt
            }).ToList() ?? new()
        };

        // Parse JSON addresses
        if (!string.IsNullOrEmpty(order.ShipToJson))
        {
            try
            {
                dto.ShipTo = JsonSerializer.Deserialize<AddressDto>(order.ShipToJson);
            }
            catch { /* Ignore parse errors */ }
        }

        if (!string.IsNullOrEmpty(order.BillToJson))
        {
            try
            {
                dto.BillTo = JsonSerializer.Deserialize<AddressDto>(order.BillToJson);
            }
            catch { /* Ignore parse errors */ }
        }

        return dto;
    }
}

public class OrderDto
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public int TenantId { get; set; }
    public string? TenantName { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int LineCount { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

public class OrderDetailDto : OrderDto
{
    public DateTime? RequestedShipDate { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public string? ShippingMethod { get; set; }
    public string? Notes { get; set; }
    public AddressDto? ShipTo { get; set; }
    public AddressDto? BillTo { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();
    public List<OrderStatusHistoryDto> StatusHistory { get; set; } = new();
}

public class OrderLineDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? VendorSku { get; set; }
    public string? Description { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityShipped { get; set; }
    public int QuantityCancelled { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class OrderStatusHistoryDto
{
    public int Id { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string? ChangedByUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class AddressDto
{
    public string? Name { get; set; }
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class OrderListResult
{
    public int Total { get; set; }
    public int DraftCount { get; set; }
    public int SubmittedCount { get; set; }
    public int ProcessingCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public List<OrderDto> Items { get; set; } = new();
}

public class CancelOrderRequest
{
    public string? Reason { get; set; }
}
