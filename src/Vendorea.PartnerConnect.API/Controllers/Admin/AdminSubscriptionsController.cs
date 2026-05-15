using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Contracts.Interfaces;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Proxy controller for merchant subscription management.
/// Forwards requests to Merchant360 subscription endpoints.
/// </summary>
[ApiController]
[Route("api/admin/subscriptions")]
[AllowAnonymous] // TODO: Restore authorization in production
public class AdminSubscriptionsController : ControllerBase
{
    private readonly IMerchant360Client _merchant360Client;
    private readonly ILogger<AdminSubscriptionsController> _logger;

    public AdminSubscriptionsController(
        IMerchant360Client merchant360Client,
        ILogger<AdminSubscriptionsController> logger)
    {
        _merchant360Client = merchant360Client;
        _logger = logger;
    }

    /// <summary>
    /// Gets all subscriptions with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSubscriptions(
        [FromQuery] string? status = null,
        [FromQuery] int? tenantId = null,
        [FromQuery] int? tradingPartnerId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _merchant360Client.GetSubscriptionsAsync(status, tenantId, tradingPartnerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a single subscription by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetSubscription(int id, CancellationToken cancellationToken = default)
    {
        var subscription = await _merchant360Client.GetSubscriptionAsync(id, cancellationToken);
        if (subscription == null)
            return NotFound();
        return Ok(subscription);
    }

    /// <summary>
    /// Creates a new subscription (admin-initiated).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateSubscription(
        [FromBody] CreateSubscriptionDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _merchant360Client.CreateSubscriptionAsync(request, cancellationToken);
        if (result == null)
            return BadRequest(new { error = "Failed to create subscription" });
        return CreatedAtAction(nameof(GetSubscription), new { id = result.Id }, result);
    }

    /// <summary>
    /// Approves a pending subscription.
    /// </summary>
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> ApproveSubscription(
        int id,
        [FromBody] ApproveSubscriptionDto request,
        CancellationToken cancellationToken = default)
    {
        var success = await _merchant360Client.ApproveSubscriptionAsync(id, request, cancellationToken);
        if (!success)
            return BadRequest(new { error = "Failed to approve subscription" });
        return Ok(new { success = true });
    }

    /// <summary>
    /// Denies a pending subscription.
    /// </summary>
    [HttpPost("{id:int}/deny")]
    public async Task<IActionResult> DenySubscription(
        int id,
        [FromBody] DenySubscriptionDto request,
        CancellationToken cancellationToken = default)
    {
        var success = await _merchant360Client.DenySubscriptionAsync(id, request, cancellationToken);
        if (!success)
            return BadRequest(new { error = "Failed to deny subscription" });
        return Ok(new { success = true });
    }

    /// <summary>
    /// Suspends an approved subscription.
    /// </summary>
    [HttpPost("{id:int}/suspend")]
    public async Task<IActionResult> SuspendSubscription(
        int id,
        [FromBody] SuspendSubscriptionDto request,
        CancellationToken cancellationToken = default)
    {
        var success = await _merchant360Client.SuspendSubscriptionAsync(id, request, cancellationToken);
        if (!success)
            return BadRequest(new { error = "Failed to suspend subscription" });
        return Ok(new { success = true });
    }

    /// <summary>
    /// Reactivates a suspended subscription.
    /// </summary>
    [HttpPost("{id:int}/reactivate")]
    public async Task<IActionResult> ReactivateSubscription(
        int id,
        CancellationToken cancellationToken = default)
    {
        var success = await _merchant360Client.ReactivateSubscriptionAsync(id, cancellationToken);
        if (!success)
            return BadRequest(new { error = "Failed to reactivate subscription" });
        return Ok(new { success = true });
    }
}
