using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Integration;

namespace Vendorea.PartnerConnect.Api.Controllers;

/// <summary>
/// Integration API for platform-to-platform order submission.
/// Used by Merchant360 and other authorized platforms to submit supplier orders.
/// This is the canonical intake endpoint - no partner-specific details exposed.
/// </summary>
[ApiController]
[Route("api/integrations/orders")]
[Produces("application/json")]
public class IntegrationOrdersController : ControllerBase
{
    private readonly ISupplierOrderIntakeService _intakeService;
    private readonly ILogger<IntegrationOrdersController> _logger;

    public IntegrationOrdersController(
        ISupplierOrderIntakeService intakeService,
        ILogger<IntegrationOrdersController> logger)
    {
        _intakeService = intakeService;
        _logger = logger;
    }

    /// <summary>
    /// Submits a canonical supplier order for processing.
    /// </summary>
    /// <remarks>
    /// This is the primary endpoint for platform integrations (e.g., Merchant360) to submit orders.
    ///
    /// The request contains:
    /// - Routing context (organization, merchant, partner connection)
    /// - Canonical business data (PO number, addresses, lines)
    /// - Business options (partial shipment, backorder preferences)
    ///
    /// Partner-specific details (XML formats, EDI codes, SFTP paths) are NOT required.
    /// PartnerConnect resolves these internally from partner connection configuration.
    ///
    /// Idempotency: Duplicate submissions with the same idempotencyKey return the existing order.
    /// Conflict: Same idempotencyKey with different content returns 409 Conflict.
    /// </remarks>
    /// <param name="request">The canonical order submission request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order acceptance response with tracking IDs</returns>
    [HttpPost]
    [AllowAnonymous] // TODO: Require service-to-service authentication in production
    [ProducesResponseType(typeof(SubmitSupplierOrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(SubmitSupplierOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SubmitSupplierOrderResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SubmitSupplierOrderResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitOrder(
        [FromBody] SubmitSupplierOrderRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received order submission from {SourcePlatform}, ExternalOrderId={ExternalOrderId}, CorrelationId={CorrelationId}",
            request.SourcePlatform, request.ExternalOrderId, request.CorrelationId);

        var result = await _intakeService.SubmitOrderAsync(request, cancellationToken);

        if (!result.Accepted)
        {
            if (result.Status == "Conflict")
            {
                _logger.LogWarning(
                    "Order submission conflict for IdempotencyKey={IdempotencyKey}",
                    request.IdempotencyKey);
                return Conflict(result);
            }

            _logger.LogWarning(
                "Order submission validation failed: {Errors}",
                string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
            return BadRequest(result);
        }

        if (result.IsDuplicate)
        {
            _logger.LogInformation(
                "Returning existing order for duplicate submission, OrderId={OrderId}",
                result.PartnerConnectOrderId);
            return Ok(result);
        }

        _logger.LogInformation(
            "Order accepted, OrderId={OrderId}, CorrelationId={CorrelationId}",
            result.PartnerConnectOrderId, result.CorrelationId);

        return Accepted(result);
    }

    /// <summary>
    /// Gets an order by external order ID from the source platform.
    /// </summary>
    /// <param name="sourcePlatform">The source platform identifier (e.g., "Merchant360")</param>
    /// <param name="externalOrderId">The external order ID from the source platform</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order details if found</returns>
    [HttpGet]
    [AllowAnonymous] // TODO: Require authentication in production
    [ProducesResponseType(typeof(IntegrationOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderByExternalId(
        [FromQuery] string sourcePlatform,
        [FromQuery] string externalOrderId,
        CancellationToken cancellationToken)
    {
        var order = await _intakeService.GetOrderByExternalIdAsync(
            sourcePlatform, externalOrderId, cancellationToken);

        if (order == null)
        {
            return NotFound(new { Message = "Order not found" });
        }

        return Ok(MapToDetailDto(order));
    }

    /// <summary>
    /// Gets an order by PartnerConnect order ID.
    /// </summary>
    /// <param name="id">The PartnerConnect order ID</param>
    /// <param name="orderService">The order service (injected)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order details if found</returns>
    [HttpGet("{id:int}")]
    [AllowAnonymous] // TODO: Require authentication in production
    [ProducesResponseType(typeof(IntegrationOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(
        int id,
        [FromServices] IOrderService orderService,
        CancellationToken cancellationToken)
    {
        // Note: We need the tenantId for the existing service - this is a limitation
        // For integration API, we should probably add a method that doesn't require tenantId
        // For now, get without tenant filter
        var order = await _intakeService.GetOrderByExternalIdAsync(
            string.Empty, string.Empty, cancellationToken);

        // Actually need to update the service to support lookup by PC order ID
        // This is a placeholder - would need proper implementation
        return NotFound(new { Message = "Get by PC Order ID not yet implemented" });
    }

    private static IntegrationOrderDetailDto MapToDetailDto(Domain.Entities.Order order)
    {
        return new IntegrationOrderDetailDto
        {
            PartnerConnectOrderId = order.Id,
            ExternalOrderId = order.ExternalOrderId,
            SourcePlatform = order.SourcePlatform,
            CorrelationId = order.CorrelationId.ToString(),
            Status = order.Status.ToString(),
            PoNumber = order.PoNumber,
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
            ErrorMessage = order.ErrorMessage
        };
    }
}

/// <summary>
/// Order details returned to integration callers.
/// </summary>
public class IntegrationOrderDetailDto
{
    public int PartnerConnectOrderId { get; set; }
    public string? ExternalOrderId { get; set; }
    public string? SourcePlatform { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
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
}
