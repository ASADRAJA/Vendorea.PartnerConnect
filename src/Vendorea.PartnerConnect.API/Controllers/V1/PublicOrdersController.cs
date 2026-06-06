using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Public API v1 controller for order operations.
/// Used by tenants to place orders and track order status.
/// </summary>
[ApiController]
[Route("api/v1/orders")]
[AllowAnonymous] // TODO: Restore [Authorize] in production
public class PublicOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<PublicOrdersController> _logger;

    public PublicOrdersController(
        IOrderService orderService,
        ILogger<PublicOrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new order.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequestDto request, CancellationToken cancellationToken)
    {
        // TODO: In production, extract OrgId and TenantId from authenticated claims
        // For now, they come from the request body for testing
        var createRequest = new CreateOrderRequest
        {
            OrganizationId = request.OrganizationId,
            TenantId = request.TenantId,
            TradingPartnerId = request.TradingPartnerId,
            AccountNumber = request.AccountNumber,
            PoNumber = request.PoNumber,
            RequestedShipDate = request.RequestedShipDate,
            RequestedDeliveryDate = request.RequestedDeliveryDate,
            ShipTo = request.ShipTo != null ? new AddressInfo
            {
                Name = request.ShipTo.Name,
                Company = request.ShipTo.Company,
                Address1 = request.ShipTo.Address1,
                Address2 = request.ShipTo.Address2,
                City = request.ShipTo.City,
                State = request.ShipTo.State,
                PostalCode = request.ShipTo.PostalCode,
                Country = request.ShipTo.Country,
                Phone = request.ShipTo.Phone,
                Email = request.ShipTo.Email
            } : null,
            BillTo = request.BillTo != null ? new AddressInfo
            {
                Name = request.BillTo.Name,
                Company = request.BillTo.Company,
                Address1 = request.BillTo.Address1,
                Address2 = request.BillTo.Address2,
                City = request.BillTo.City,
                State = request.BillTo.State,
                PostalCode = request.BillTo.PostalCode,
                Country = request.BillTo.Country,
                Phone = request.BillTo.Phone,
                Email = request.BillTo.Email
            } : null,
            ShippingMethod = request.ShippingMethod,
            Notes = request.Notes,
            Lines = request.Lines.Select(l => new CreateOrderLineRequest
            {
                Sku = l.Sku,
                VendorSku = l.VendorSku,
                Upc = l.Upc,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitOfMeasure = l.UnitOfMeasure ?? "EA",
                UnitPrice = l.UnitPrice,
                Notes = l.Notes
            }).ToList()
        };

        var result = await _orderService.CreateOrderAsync(createRequest, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = result.ErrorCode!,
                ErrorMessage = result.ErrorMessage!
            });
        }

        var response = MapToOrderResponse(result.Order!);
        return CreatedAtAction(nameof(GetOrder), new { id = result.Order!.Id }, response);
    }

    /// <summary>
    /// Gets orders for the authenticated tenant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(OrderListResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int tenantId, // TODO: Extract from claims in production
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        OrderStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var result = await _orderService.GetOrdersAsync(tenantId, statusFilter, page, pageSize, cancellationToken);

        return Ok(new OrderListResponseDto
        {
            Orders = result.Orders.Select(MapToOrderResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages
        });
    }

    /// <summary>
    /// Gets a specific order by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(
        int id,
        [FromQuery] int tenantId, // TODO: Extract from claims in production
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderWithDetailsAsync(id, tenantId, cancellationToken);

        if (order == null)
        {
            return NotFound();
        }

        return Ok(MapToOrderDetailResponse(order));
    }

    /// <summary>
    /// Cancels an order.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOrder(
        int id,
        [FromBody] CancelOrderRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.CancelOrderAsync(id, request.TenantId, request.Reason, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorCode == "ORDER_NOT_FOUND")
            {
                return NotFound();
            }
            return BadRequest(new ErrorResponseDto
            {
                ErrorCode = result.ErrorCode!,
                ErrorMessage = result.ErrorMessage!
            });
        }

        return Ok(MapToOrderResponse(result.Order!));
    }

    private static OrderResponseDto MapToOrderResponse(Order order) => new()
    {
        Id = order.Id,
        PoNumber = order.PoNumber,
        Status = order.Status.ToString(),
        TradingPartnerId = order.TradingPartnerId,
        OrderDate = order.OrderDate,
        TotalAmount = order.TotalAmount,
        Currency = order.Currency,
        LineCount = order.Lines?.Count ?? 0,
        SubmittedAt = order.SubmittedAt,
        AcknowledgedAt = order.AcknowledgedAt,
        ShippedAt = order.ShippedAt,
        CompletedAt = order.CompletedAt,
        CancelledAt = order.CancelledAt
    };

    private static OrderDetailResponseDto MapToOrderDetailResponse(Order order) => new()
    {
        Id = order.Id,
        PoNumber = order.PoNumber,
        Status = order.Status.ToString(),
        TradingPartnerId = order.TradingPartnerId,
        TradingPartnerName = order.TradingPartner?.Name,
        OrderDate = order.OrderDate,
        RequestedShipDate = order.RequestedShipDate,
        RequestedDeliveryDate = order.RequestedDeliveryDate,
        ShipToJson = order.ShipToJson,
        BillToJson = order.BillToJson,
        ShippingMethod = order.ShippingMethod,
        Notes = order.Notes,
        SubTotal = order.SubTotal,
        TaxAmount = order.TaxAmount,
        ShippingAmount = order.ShippingAmount,
        TotalAmount = order.TotalAmount,
        Currency = order.Currency,
        PartnerOrderNumber = order.PartnerOrderNumber,
        SubmittedAt = order.SubmittedAt,
        AcknowledgedAt = order.AcknowledgedAt,
        ShippedAt = order.ShippedAt,
        CompletedAt = order.CompletedAt,
        CancelledAt = order.CancelledAt,
        CancellationReason = order.CancellationReason,
        ErrorMessage = order.ErrorMessage,
        Lines = order.Lines.Select(l => new OrderLineResponseDto
        {
            Id = l.Id,
            LineNumber = l.LineNumber,
            Sku = l.Sku,
            VendorSku = l.VendorSku,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitOfMeasure = l.UnitOfMeasure,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal,
            Status = l.Status.ToString(),
            AcknowledgedQuantity = l.AcknowledgedQuantity,
            ShippedQuantity = l.ShippedQuantity,
            BackorderedQuantity = l.BackorderedQuantity,
            AcknowledgmentCode = l.AcknowledgmentCode,
            AcknowledgmentMessage = l.AcknowledgmentMessage,
            EstimatedShipDate = l.EstimatedShipDate
        }).ToList()
    };
}

#region DTOs

public class CreateOrderRequestDto
{
    public int OrganizationId { get; set; }
    public int TenantId { get; set; }
    public int TradingPartnerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public DateTime? RequestedShipDate { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public AddressDto? ShipTo { get; set; }
    public AddressDto? BillTo { get; set; }
    public string? ShippingMethod { get; set; }
    public string? Notes { get; set; }
    public List<OrderLineRequestDto> Lines { get; set; } = new();
}

public class AddressDto
{
    public string? Name { get; set; }
    public string? Company { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class OrderLineRequestDto
{
    public string Sku { get; set; } = string.Empty;
    public string? VendorSku { get; set; }
    public string? Upc { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}

public class CancelOrderRequestDto
{
    public int TenantId { get; set; }
    public string? Reason { get; set; }
}

public class OrderResponseDto
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TradingPartnerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public int LineCount { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public class OrderDetailResponseDto : OrderResponseDto
{
    public string? TradingPartnerName { get; set; }
    public DateTime? RequestedShipDate { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public string? ShipToJson { get; set; }
    public string? BillToJson { get; set; }
    public string? ShippingMethod { get; set; }
    public string? Notes { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public string? PartnerOrderNumber { get; set; }
    public string? CancellationReason { get; set; }
    public string? ErrorMessage { get; set; }
    public List<OrderLineResponseDto> Lines { get; set; } = new();
}

public class OrderLineResponseDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? VendorSku { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? AcknowledgedQuantity { get; set; }
    public decimal? ShippedQuantity { get; set; }
    public decimal? BackorderedQuantity { get; set; }
    public string? AcknowledgmentCode { get; set; }
    public string? AcknowledgmentMessage { get; set; }
    public DateTime? EstimatedShipDate { get; set; }
}

public class OrderListResponseDto
{
    public List<OrderResponseDto> Orders { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ErrorResponseDto
{
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

#endregion
