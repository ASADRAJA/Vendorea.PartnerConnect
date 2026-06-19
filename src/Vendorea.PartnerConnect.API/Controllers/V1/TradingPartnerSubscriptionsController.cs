using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Merchant-facing (org API key) lifecycle actions on a trading-partner subscription/connection.
/// The merchant cancels a still-pending request or unsubscribes from an approved connection.
/// The subscription is identified by the merchant's tenant id and PartnerConnect's trading-partner
/// id (the value returned by GET /api/v1/org/partners — NOT the caller's local partner id).
/// </summary>
[ApiController]
[Route("api/v1/trading-partner-subscriptions")]
[Produces("application/json")]
[Authorize]
public class TradingPartnerSubscriptionsController : ControllerBase
{
    private readonly ITenantConnectionService _connectionService;
    private readonly ILogger<TradingPartnerSubscriptionsController> _logger;

    public TradingPartnerSubscriptionsController(
        ITenantConnectionService connectionService,
        ILogger<TradingPartnerSubscriptionsController> logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    /// <summary>Cancels a still-pending connection request for the calling org's tenant.</summary>
    [HttpPost("cancel")]
    [RequireScope(ApiScopes.ConnectionsWrite)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel([FromBody] SubscriptionChangeRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolve(request, out var orgId, out var error))
            return error!;

        var result = await _connectionService.CancelConnectionAsync(
            orgId, request.TenantId.ToString(), request.TradingPartnerId, cancellationToken);
        return MapResult(result);
    }

    /// <summary>Unsubscribes (disables) an approved connection for the calling org's tenant.</summary>
    [HttpPost("unsubscribe")]
    [RequireScope(ApiScopes.ConnectionsWrite)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unsubscribe([FromBody] SubscriptionChangeRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolve(request, out var orgId, out var error))
            return error!;

        var result = await _connectionService.UnsubscribeConnectionAsync(
            orgId, request.TenantId.ToString(), request.TradingPartnerId, cancellationToken);
        return MapResult(result);
    }

    private bool TryResolve(SubscriptionChangeRequest request, out int organizationId, out IActionResult? error)
    {
        organizationId = 0;
        error = null;

        var orgId = User.GetOrganizationId();
        if (orgId is null)
        {
            error = StatusCode(StatusCodes.Status403Forbidden, new { error = "An organization API key is required for this operation" });
            return false;
        }

        if (request is null || request.TenantId <= 0 || request.TradingPartnerId <= 0)
        {
            error = BadRequest(new { error = "tenantId and tradingPartnerId are required" });
            return false;
        }

        organizationId = orgId.Value;
        return true;
    }

    private IActionResult MapResult(ConnectionChangeResult result) => result.Status switch
    {
        ConnectionChangeStatus.Ok => Ok(new
        {
            success = true,
            connectionId = result.Connection?.Id,
            status = result.Connection?.ApprovalStatus.ToString(),
            isActive = result.Connection?.IsActive
        }),
        ConnectionChangeStatus.NotFound => NotFound(new { error = result.Error }),
        ConnectionChangeStatus.InvalidState => Conflict(new { error = result.Error }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { error = "Unexpected result" })
    };
}

/// <summary>
/// Request body for cancel/unsubscribe. Both fields are the caller's identifiers:
/// <c>tenantId</c> is the merchant's tenant id (stored on the connection as ExternalTenantId);
/// <c>tradingPartnerId</c> is PartnerConnect's trading-partner id from GET /api/v1/org/partners.
/// </summary>
public class SubscriptionChangeRequest
{
    public int TenantId { get; set; }
    public int TradingPartnerId { get; set; }
}
