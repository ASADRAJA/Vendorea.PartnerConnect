using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers;

/// <summary>
/// Integration API for platform-to-platform order submission and tracking.
/// Used by Merchant360 and other authorized platforms. This is the canonical, partner-agnostic
/// order surface — submit, look up, list, and cancel — and the only order API M360 should use.
/// Requires an API key (org key for Merchant360); callers may only act within their own org.
/// </summary>
[ApiController]
[Route("api/integrations/orders")]
[Produces("application/json")]
[Authorize]
public class IntegrationOrdersController : ControllerBase
{
    private readonly ISupplierOrderIntakeService _intakeService;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderService _orderService;
    private readonly ILogger<IntegrationOrdersController> _logger;

    public IntegrationOrdersController(
        ISupplierOrderIntakeService intakeService,
        IOrderRepository orderRepository,
        IOrderService orderService,
        ILogger<IntegrationOrdersController> logger)
    {
        _intakeService = intakeService;
        _orderRepository = orderRepository;
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Submits a canonical supplier order for processing.
    /// </summary>
    /// <remarks>
    /// Routing context (organization, merchant, partner connection), canonical business data
    /// (PO number, addresses, lines), and business options. Partner-specific details (XML/EDI
    /// formats, SFTP paths) are NOT required — PartnerConnect resolves those internally.
    /// Idempotent on <c>idempotencyKey</c>.
    /// </remarks>
    [HttpPost]
    [RequireScope(ApiScopes.OrdersWrite)]
    [ProducesResponseType(typeof(SubmitSupplierOrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(SubmitSupplierOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SubmitSupplierOrderResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(SubmitSupplierOrderResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitOrder(
        [FromBody] SubmitSupplierOrderRequest request,
        CancellationToken cancellationToken)
    {
        // Anti-impersonation: an org-authenticated caller may only submit for its own organization.
        var callerOrgId = User.GetOrganizationId();
        if (callerOrgId is int orgId)
        {
            if (request.OrganizationId > 0 && request.OrganizationId != orgId)
                return Forbid();

            var callerOrgCode = User.FindFirst("org_code")?.Value;
            if (!string.IsNullOrWhiteSpace(request.OrganizationCode)
                && !string.IsNullOrWhiteSpace(callerOrgCode)
                && !string.Equals(request.OrganizationCode, callerOrgCode, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            // Stamp the authenticated org so the body can't route the order elsewhere.
            if (request.OrganizationId <= 0)
                request = request with { OrganizationId = orgId };
        }

        _logger.LogInformation(
            "Received order submission from {SourcePlatform}, ExternalOrderId={ExternalOrderId}, CorrelationId={CorrelationId}",
            request.SourcePlatform, request.ExternalOrderId, request.CorrelationId);

        var result = await _intakeService.SubmitOrderAsync(request, cancellationToken);

        if (!result.Accepted)
        {
            if (result.Status == "Conflict")
                return Conflict(result);

            return BadRequest(result);
        }

        if (result.IsDuplicate)
            return Ok(result);

        return Accepted(result);
    }

    /// <summary>Looks up an order by the source platform's external order id.</summary>
    [HttpGet]
    [RequireScope(ApiScopes.OrdersRead)]
    [ProducesResponseType(typeof(IntegrationOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderByExternalId(
        [FromQuery] string sourcePlatform,
        [FromQuery] string externalOrderId,
        CancellationToken cancellationToken)
    {
        var order = await _intakeService.GetOrderByExternalIdAsync(sourcePlatform, externalOrderId, cancellationToken);
        if (order == null || !CallerOwns(order))
            return NotFound(new { Message = "Order not found" });

        // Re-load with full details (lines) for the response.
        var detailed = await _orderRepository.GetByIdWithFullDetailsAsync(order.Id, cancellationToken) ?? order;
        return Ok(MapToDetailDto(detailed));
    }

    /// <summary>Gets an order by its PartnerConnect order id.</summary>
    [HttpGet("{id:int}")]
    [RequireScope(ApiScopes.OrdersRead)]
    [ProducesResponseType(typeof(IntegrationOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(int id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdWithFullDetailsAsync(id, cancellationToken);
        if (order == null || !CallerOwns(order))
            return NotFound(new { Message = "Order not found" });

        return Ok(MapToDetailDto(order));
    }

    /// <summary>Lists the caller org's orders (most recent first), optionally filtered by status.</summary>
    [HttpGet("list")]
    [RequireScope(ApiScopes.OrdersRead)]
    [ProducesResponseType(typeof(IReadOnlyList<IntegrationOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListOrders(
        [FromQuery] string? status,
        [FromQuery] int? organizationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // Org callers are pinned to their own org; admin/dealer callers must name an org.
        var targetOrg = User.GetOrganizationId() ?? organizationId ?? 0;
        if (targetOrg <= 0)
            return BadRequest(new { Message = "organizationId is required" });

        OrderStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsed))
            statusFilter = parsed;

        pageSize = Math.Clamp(pageSize, 1, 200);
        var offset = Math.Max(0, (Math.Max(1, page) - 1) * pageSize);

        var orders = await _orderRepository.GetByOrganizationIdAsync(
            targetOrg, statusFilter, pageSize, offset, cancellationToken);

        return Ok(orders.Select(MapToDetailDto).ToList());
    }

    /// <summary>Cancels an order the caller owns.</summary>
    [HttpPost("{id:int}/cancel")]
    [RequireScope(ApiScopes.OrdersWrite)]
    [ProducesResponseType(typeof(IntegrationOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOrder(
        int id,
        [FromBody] CancelIntegrationOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken);
        if (order == null || !CallerOwns(order))
            return NotFound(new { Message = "Order not found" });

        var result = await _orderService.CancelOrderAsync(id, order.TenantId, request?.Reason, cancellationToken);
        if (!result.Success)
        {
            if (result.ErrorCode == "ORDER_NOT_FOUND")
                return NotFound();
            return BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage });
        }

        var updated = await _orderRepository.GetByIdWithFullDetailsAsync(id, cancellationToken) ?? result.Order!;
        return Ok(MapToDetailDto(updated));
    }

    /// <summary>True if the caller may access this order (admin/dealer keys: yes; org keys: same org only).</summary>
    private bool CallerOwns(Order order)
    {
        var callerOrgId = User.GetOrganizationId();
        return callerOrgId == null || order.OrganizationId == callerOrgId.Value;
    }

    private static IntegrationOrderDetailDto MapToDetailDto(Order order) => new()
    {
        PartnerConnectOrderId = order.Id,
        ExternalOrderId = order.ExternalOrderId,
        SourcePlatform = order.SourcePlatform,
        CorrelationId = order.CorrelationId.ToString(),
        Status = order.Status.ToString(),
        PoNumber = order.PoNumber,
        OrderType = order.OrderType,
        DistributionCenterCode = order.DistributionCenterCode,
        Attn = order.Attn,
        TradingPartnerId = order.TradingPartnerId,
        PartnerOrderNumber = order.PartnerOrderNumber,
        OrderDate = order.OrderDate,
        TotalAmount = order.TotalAmount,
        Currency = order.Currency,
        LineCount = order.Lines?.Count ?? 0,
        SubmittedAt = order.SubmittedAt,
        AcknowledgedAt = order.AcknowledgedAt,
        ShippedAt = order.ShippedAt,
        CompletedAt = order.CompletedAt,
        CancelledAt = order.CancelledAt,
        ErrorMessage = order.ErrorMessage,
        Lines = (order.Lines ?? new List<OrderLine>())
            .OrderBy(l => l.LineNumber)
            .Select(l => new IntegrationOrderLineDto
            {
                LineNumber = l.LineNumber,
                Sku = l.Sku,
                VendorSku = l.VendorSku,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitOfMeasure = l.UnitOfMeasure,
                UnitPrice = l.UnitPrice,
                Status = l.Status.ToString()
            })
            .ToList()
    };
}

/// <summary>Cancel request body for the integration order API.</summary>
public class CancelIntegrationOrderRequest
{
    public string? Reason { get; set; }
}

/// <summary>Order details returned to integration callers.</summary>
public class IntegrationOrderDetailDto
{
    public int PartnerConnectOrderId { get; set; }
    public string? ExternalOrderId { get; set; }
    public string? SourcePlatform { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string? DistributionCenterCode { get; set; }
    public string? Attn { get; set; }
    public int TradingPartnerId { get; set; }
    public string? PartnerOrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public int LineCount { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<IntegrationOrderLineDto> Lines { get; set; } = new();
}

/// <summary>Order line summary returned to integration callers.</summary>
public class IntegrationOrderLineDto
{
    public int LineNumber { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? VendorSku { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public decimal UnitPrice { get; set; }
    public string Status { get; set; } = string.Empty;
}
