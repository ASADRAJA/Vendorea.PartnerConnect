using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// API v1 controller for trading partner subscription requests.
/// Called by Merchant360 to request subscriptions to trading partner data.
/// </summary>
[ApiController]
[Route("api/v1/trading-partner-subscriptions")]
[AllowAnonymous] // TODO: Restore [Authorize] in production - M360 should authenticate via OAuth2
public class TradingPartnerSubscriptionsController : ControllerBase
{
    private readonly IMerchantSubscriptionRequestRepository _subscriptionRepository;
    private readonly ITradingPartnerRepository _tradingPartnerRepository;
    private readonly ILogger<TradingPartnerSubscriptionsController> _logger;

    public TradingPartnerSubscriptionsController(
        IMerchantSubscriptionRequestRepository subscriptionRepository,
        ITradingPartnerRepository tradingPartnerRepository,
        ILogger<TradingPartnerSubscriptionsController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _tradingPartnerRepository = tradingPartnerRepository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new subscription request from Merchant360.
    /// Called by M360 when a tenant wants to subscribe to a trading partner's data.
    /// </summary>
    [HttpPost("request")]
    public async Task<IActionResult> CreateSubscriptionRequest(
        [FromBody] CreateSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received subscription request from M360: TenantId={TenantId}, TradingPartnerId={TradingPartnerId}, AccountNumber={AccountNumber}",
            request.TenantId, request.TradingPartnerId, request.AccountNumber);

        // Validate trading partner exists
        var tradingPartner = await _tradingPartnerRepository.GetByIdAsync(request.TradingPartnerId, cancellationToken);
        if (tradingPartner == null)
        {
            _logger.LogWarning("Trading partner {TradingPartnerId} not found", request.TradingPartnerId);
            return BadRequest(new { error = "InvalidTradingPartner", message = $"Trading partner {request.TradingPartnerId} not found" });
        }

        // Check for existing pending request
        var existing = await _subscriptionRepository.GetByTenantAndPartnerAsync(
            request.TenantId, request.TradingPartnerId, cancellationToken);

        if (existing != null)
        {
            if (existing.Status == SubscriptionRequestStatus.Pending)
            {
                _logger.LogInformation("Subscription request already exists and is pending: Id={Id}", existing.Id);
                return Ok(new SubscriptionRequestResponseDto
                {
                    Id = existing.Id,
                    TenantId = existing.TenantId,
                    TradingPartnerId = existing.TradingPartnerId,
                    TradingPartnerCode = tradingPartner.Code,
                    TradingPartnerName = tradingPartner.Name,
                    AccountNumber = existing.AccountNumber,
                    Status = existing.Status.ToString(),
                    RequestedAt = existing.RequestedAt,
                    Message = "Subscription request already pending"
                });
            }
            else if (existing.Status == SubscriptionRequestStatus.Approved)
            {
                _logger.LogInformation("Subscription already approved: Id={Id}", existing.Id);
                return Ok(new SubscriptionRequestResponseDto
                {
                    Id = existing.Id,
                    TenantId = existing.TenantId,
                    TradingPartnerId = existing.TradingPartnerId,
                    TradingPartnerCode = tradingPartner.Code,
                    TradingPartnerName = tradingPartner.Name,
                    AccountNumber = existing.AccountNumber,
                    Status = existing.Status.ToString(),
                    RequestedAt = existing.RequestedAt,
                    ApprovedAt = existing.ApprovedAt,
                    Message = "Subscription already approved"
                });
            }
            // If denied, allow re-request by updating the existing record
            existing.Status = SubscriptionRequestStatus.Pending;
            existing.AccountNumber = request.AccountNumber;
            existing.RequestedAt = DateTime.UtcNow;
            existing.DeniedAt = null;
            existing.DeniedByUserId = null;
            existing.DenialReason = null;

            await _subscriptionRepository.UpdateAsync(existing, cancellationToken);

            _logger.LogInformation("Re-submitted previously denied subscription request: Id={Id}", existing.Id);

            return Ok(new SubscriptionRequestResponseDto
            {
                Id = existing.Id,
                TenantId = existing.TenantId,
                TradingPartnerId = existing.TradingPartnerId,
                TradingPartnerCode = tradingPartner.Code,
                TradingPartnerName = tradingPartner.Name,
                AccountNumber = existing.AccountNumber,
                Status = existing.Status.ToString(),
                RequestedAt = existing.RequestedAt,
                Message = "Subscription request re-submitted"
            });
        }

        // Create new subscription request
        var subscriptionRequest = new MerchantSubscriptionRequest
        {
            TenantId = request.TenantId,
            TradingPartnerId = request.TradingPartnerId,
            AccountNumber = request.AccountNumber,
            Status = SubscriptionRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        await _subscriptionRepository.AddAsync(subscriptionRequest, cancellationToken);

        _logger.LogInformation("Created subscription request: Id={Id}", subscriptionRequest.Id);

        return CreatedAtAction(nameof(GetSubscriptionRequest), new { id = subscriptionRequest.Id }, new SubscriptionRequestResponseDto
        {
            Id = subscriptionRequest.Id,
            TenantId = subscriptionRequest.TenantId,
            TradingPartnerId = subscriptionRequest.TradingPartnerId,
            TradingPartnerCode = tradingPartner.Code,
            TradingPartnerName = tradingPartner.Name,
            AccountNumber = subscriptionRequest.AccountNumber,
            Status = subscriptionRequest.Status.ToString(),
            RequestedAt = subscriptionRequest.RequestedAt,
            Message = "Subscription request created successfully"
        });
    }

    /// <summary>
    /// Gets a subscription request by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetSubscriptionRequest(int id, CancellationToken cancellationToken)
    {
        var request = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (request == null)
            return NotFound();

        return Ok(new SubscriptionRequestResponseDto
        {
            Id = request.Id,
            TenantId = request.TenantId,
            TradingPartnerId = request.TradingPartnerId,
            TradingPartnerCode = request.TradingPartner?.Code,
            TradingPartnerName = request.TradingPartner?.Name,
            AccountNumber = request.AccountNumber,
            Status = request.Status.ToString(),
            RequestedAt = request.RequestedAt,
            ApprovedAt = request.ApprovedAt,
            DeniedAt = request.DeniedAt,
            DenialReason = request.DenialReason
        });
    }

    /// <summary>
    /// Gets all subscription requests with optional status filter.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSubscriptionRequests(
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MerchantSubscriptionRequest> requests;

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SubscriptionRequestStatus>(status, true, out var statusEnum))
        {
            requests = await _subscriptionRepository.GetByStatusAsync(statusEnum, cancellationToken);
        }
        else
        {
            requests = await _subscriptionRepository.GetAllAsync(cancellationToken);
        }

        var response = requests.Select(r => new SubscriptionRequestResponseDto
        {
            Id = r.Id,
            TenantId = r.TenantId,
            TradingPartnerId = r.TradingPartnerId,
            TradingPartnerCode = r.TradingPartner?.Code,
            TradingPartnerName = r.TradingPartner?.Name,
            AccountNumber = r.AccountNumber,
            Status = r.Status.ToString(),
            RequestedAt = r.RequestedAt,
            ApprovedAt = r.ApprovedAt,
            DeniedAt = r.DeniedAt,
            DenialReason = r.DenialReason
        }).ToList();

        return Ok(new
        {
            Total = response.Count,
            PendingCount = response.Count(r => r.Status == "Pending"),
            ApprovedCount = response.Count(r => r.Status == "Approved"),
            DeniedCount = response.Count(r => r.Status == "Denied"),
            Items = response
        });
    }

    /// <summary>
    /// Approves a pending subscription request.
    /// </summary>
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> ApproveSubscriptionRequest(
        int id,
        [FromBody] ApproveRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        var subscriptionRequest = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscriptionRequest == null)
            return NotFound();

        if (subscriptionRequest.Status != SubscriptionRequestStatus.Pending)
            return BadRequest(new { error = "InvalidStatus", message = "Can only approve pending requests" });

        subscriptionRequest.Status = SubscriptionRequestStatus.Approved;
        subscriptionRequest.ApprovedAt = DateTime.UtcNow;
        subscriptionRequest.Notes = request?.Notes;

        await _subscriptionRepository.UpdateAsync(subscriptionRequest, cancellationToken);

        _logger.LogInformation("Approved subscription request: Id={Id}", id);

        return Ok(new { success = true, message = "Subscription request approved" });
    }

    /// <summary>
    /// Denies a pending subscription request.
    /// </summary>
    [HttpPost("{id:int}/deny")]
    public async Task<IActionResult> DenySubscriptionRequest(
        int id,
        [FromBody] DenyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var subscriptionRequest = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscriptionRequest == null)
            return NotFound();

        if (subscriptionRequest.Status != SubscriptionRequestStatus.Pending)
            return BadRequest(new { error = "InvalidStatus", message = "Can only deny pending requests" });

        subscriptionRequest.Status = SubscriptionRequestStatus.Denied;
        subscriptionRequest.DeniedAt = DateTime.UtcNow;
        subscriptionRequest.DenialReason = request.Reason;
        subscriptionRequest.Notes = request.Notes;

        await _subscriptionRepository.UpdateAsync(subscriptionRequest, cancellationToken);

        _logger.LogInformation("Denied subscription request: Id={Id}, Reason={Reason}", id, request.Reason);

        return Ok(new { success = true, message = "Subscription request denied" });
    }

    /// <summary>
    /// Cancels a pending subscription request.
    /// Called by M360 when a merchant wants to cancel their pending request.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelSubscriptionRequest(
        int id,
        CancellationToken cancellationToken = default)
    {
        var subscriptionRequest = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscriptionRequest == null)
            return NotFound();

        if (subscriptionRequest.Status != SubscriptionRequestStatus.Pending)
            return BadRequest(new { error = "InvalidStatus", message = "Can only cancel pending requests" });

        subscriptionRequest.Status = SubscriptionRequestStatus.Cancelled;
        subscriptionRequest.CancelledAt = DateTime.UtcNow;

        await _subscriptionRepository.UpdateAsync(subscriptionRequest, cancellationToken);

        _logger.LogInformation("Cancelled subscription request: Id={Id}, TenantId={TenantId}", id, subscriptionRequest.TenantId);

        return Ok(new { success = true, message = "Subscription request cancelled" });
    }

    /// <summary>
    /// Cancels a pending subscription request by tenant and trading partner.
    /// Called by M360 when a merchant wants to cancel their pending request.
    /// </summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelSubscriptionRequestByTenantAndPartner(
        [FromBody] CancelSubscriptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var subscriptionRequest = await _subscriptionRepository.GetByTenantAndPartnerAsync(
            request.TenantId, request.TradingPartnerId, cancellationToken);

        if (subscriptionRequest == null)
            return NotFound(new { error = "NotFound", message = "Subscription request not found" });

        if (subscriptionRequest.Status != SubscriptionRequestStatus.Pending)
            return BadRequest(new { error = "InvalidStatus", message = "Can only cancel pending requests" });

        subscriptionRequest.Status = SubscriptionRequestStatus.Cancelled;
        subscriptionRequest.CancelledAt = DateTime.UtcNow;

        await _subscriptionRepository.UpdateAsync(subscriptionRequest, cancellationToken);

        _logger.LogInformation("Cancelled subscription request: TenantId={TenantId}, TradingPartnerId={TradingPartnerId}",
            request.TenantId, request.TradingPartnerId);

        return Ok(new { success = true, message = "Subscription request cancelled" });
    }

    /// <summary>
    /// Unsubscribes from an active subscription.
    /// Called by M360 when a merchant wants to stop receiving data from a trading partner.
    /// </summary>
    [HttpPost("{id:int}/unsubscribe")]
    public async Task<IActionResult> UnsubscribeById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var subscriptionRequest = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscriptionRequest == null)
            return NotFound();

        if (subscriptionRequest.Status != SubscriptionRequestStatus.Approved &&
            subscriptionRequest.Status != SubscriptionRequestStatus.Suspended)
        {
            return BadRequest(new { error = "InvalidStatus", message = "Can only unsubscribe from approved or suspended subscriptions" });
        }

        subscriptionRequest.Status = SubscriptionRequestStatus.Cancelled;
        subscriptionRequest.CancelledAt = DateTime.UtcNow;

        await _subscriptionRepository.UpdateAsync(subscriptionRequest, cancellationToken);

        _logger.LogInformation("Unsubscribed: Id={Id}, TenantId={TenantId}, TradingPartnerId={TradingPartnerId}",
            id, subscriptionRequest.TenantId, subscriptionRequest.TradingPartnerId);

        return Ok(new { success = true, message = "Successfully unsubscribed" });
    }

    /// <summary>
    /// Unsubscribes from an active subscription by tenant and trading partner.
    /// Called by M360 when a merchant wants to stop receiving data from a trading partner.
    /// </summary>
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> UnsubscribeByTenantAndPartner(
        [FromBody] UnsubscribeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var subscriptionRequest = await _subscriptionRepository.GetByTenantAndPartnerAsync(
            request.TenantId, request.TradingPartnerId, cancellationToken);

        if (subscriptionRequest == null)
            return NotFound(new { error = "NotFound", message = "Subscription not found" });

        if (subscriptionRequest.Status != SubscriptionRequestStatus.Approved &&
            subscriptionRequest.Status != SubscriptionRequestStatus.Suspended)
        {
            return BadRequest(new { error = "InvalidStatus", message = "Can only unsubscribe from approved or suspended subscriptions" });
        }

        subscriptionRequest.Status = SubscriptionRequestStatus.Cancelled;
        subscriptionRequest.CancelledAt = DateTime.UtcNow;

        await _subscriptionRepository.UpdateAsync(subscriptionRequest, cancellationToken);

        _logger.LogInformation("Unsubscribed: TenantId={TenantId}, TradingPartnerId={TradingPartnerId}",
            request.TenantId, request.TradingPartnerId);

        return Ok(new { success = true, message = "Successfully unsubscribed" });
    }
}

#region DTOs

/// <summary>
/// Request to create a new subscription from M360.
/// </summary>
public class CreateSubscriptionRequestDto
{
    /// <summary>
    /// The M360 tenant ID requesting the subscription.
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// The trading partner ID in PartnerConnect.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// The merchant's account number with the trading partner.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;
}

/// <summary>
/// Response for subscription request operations.
/// </summary>
public class SubscriptionRequestResponseDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public string? TradingPartnerName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DeniedAt { get; set; }
    public string? DenialReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Request to approve a subscription.
/// </summary>
public class ApproveRequestDto
{
    public string? Notes { get; set; }
}

/// <summary>
/// Request to deny a subscription.
/// </summary>
public class DenyRequestDto
{
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// Request to cancel a subscription by tenant and trading partner.
/// </summary>
public class CancelSubscriptionRequestDto
{
    public int TenantId { get; set; }
    public int TradingPartnerId { get; set; }
}

/// <summary>
/// Request to unsubscribe by tenant and trading partner.
/// </summary>
public class UnsubscribeRequestDto
{
    public int TenantId { get; set; }
    public int TradingPartnerId { get; set; }
}

#endregion
