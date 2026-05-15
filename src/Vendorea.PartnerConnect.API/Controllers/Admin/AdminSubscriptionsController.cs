using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for merchant subscription management.
/// Uses local MerchantSubscriptionRequests table as source of truth.
/// </summary>
[ApiController]
[Route("api/admin/subscriptions")]
[AllowAnonymous] // TODO: Restore authorization in production
public class AdminSubscriptionsController : ControllerBase
{
    private readonly IMerchantSubscriptionRequestRepository _subscriptionRepository;
    private readonly IMerchant360Client _merchant360Client;
    private readonly ILogger<AdminSubscriptionsController> _logger;

    public AdminSubscriptionsController(
        IMerchantSubscriptionRequestRepository subscriptionRepository,
        IMerchant360Client merchant360Client,
        ILogger<AdminSubscriptionsController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
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
        IReadOnlyList<MerchantSubscriptionRequest> subscriptions;

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SubscriptionRequestStatus>(status, true, out var statusEnum))
        {
            subscriptions = await _subscriptionRepository.GetByStatusAsync(statusEnum, cancellationToken);
        }
        else
        {
            subscriptions = await _subscriptionRepository.GetAllAsync(cancellationToken);
        }

        // Apply additional filters
        var filtered = subscriptions.AsEnumerable();
        if (tenantId.HasValue)
            filtered = filtered.Where(s => s.TenantId == tenantId.Value);
        if (tradingPartnerId.HasValue)
            filtered = filtered.Where(s => s.TradingPartnerId == tradingPartnerId.Value);

        var items = filtered.ToList();

        // Try to get merchant names from M360
        var merchantNames = await GetMerchantNamesAsync(items.Select(s => s.TenantId).Distinct(), cancellationToken);

        var result = new SubscriptionListResult
        {
            Total = items.Count,
            PendingCount = items.Count(s => s.Status == SubscriptionRequestStatus.Pending),
            ApprovedCount = items.Count(s => s.Status == SubscriptionRequestStatus.Approved),
            DeniedCount = items.Count(s => s.Status == SubscriptionRequestStatus.Denied),
            SuspendedCount = items.Count(s => s.Status == SubscriptionRequestStatus.Suspended),
            Items = items.Select(s => MapToDto(s, merchantNames)).ToList()
        };

        return Ok(result);
    }

    /// <summary>
    /// Gets a single subscription by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetSubscription(int id, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription == null)
            return NotFound();

        var merchantNames = await GetMerchantNamesAsync(new[] { subscription.TenantId }, cancellationToken);
        return Ok(MapToDto(subscription, merchantNames));
    }

    /// <summary>
    /// Creates a new subscription (admin-initiated).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateSubscription(
        [FromBody] CreateSubscriptionDto request,
        CancellationToken cancellationToken = default)
    {
        var subscription = new MerchantSubscriptionRequest
        {
            TenantId = request.TenantId,
            TradingPartnerId = request.TradingPartnerId,
            AccountNumber = request.AccountNumber,
            Notes = request.Notes,
            Status = SubscriptionRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        await _subscriptionRepository.AddAsync(subscription, cancellationToken);

        var merchantNames = await GetMerchantNamesAsync(new[] { subscription.TenantId }, cancellationToken);
        return CreatedAtAction(nameof(GetSubscription), new { id = subscription.Id }, MapToDto(subscription, merchantNames));
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
        var subscription = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription == null)
            return NotFound();

        if (subscription.Status != SubscriptionRequestStatus.Pending && subscription.Status != SubscriptionRequestStatus.Denied)
            return BadRequest(new { error = "InvalidStatus", message = "Can only approve pending or denied subscriptions" });

        subscription.Status = SubscriptionRequestStatus.Approved;
        subscription.ApprovedAt = DateTime.UtcNow;
        subscription.Notes = request.Notes ?? subscription.Notes;
        // Clear any previous denial
        subscription.DeniedAt = null;
        subscription.DeniedByUserId = null;
        subscription.DenialReason = null;
        // Clear any suspension
        subscription.SuspendedAt = null;
        subscription.SuspendedByUserId = null;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        _logger.LogInformation("Approved subscription {Id} for TenantId={TenantId}, TradingPartnerId={TradingPartnerId}",
            id, subscription.TenantId, subscription.TradingPartnerId);

        // Notify M360 about the approval
        await NotifyM360SubscriptionStatusChangeAsync(subscription, cancellationToken);

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
        var subscription = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription == null)
            return NotFound();

        if (subscription.Status != SubscriptionRequestStatus.Pending)
            return BadRequest(new { error = "InvalidStatus", message = "Can only deny pending subscriptions" });

        subscription.Status = SubscriptionRequestStatus.Denied;
        subscription.DeniedAt = DateTime.UtcNow;
        subscription.DenialReason = request.Reason;
        subscription.Notes = request.Notes ?? subscription.Notes;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        _logger.LogInformation("Denied subscription {Id} for TenantId={TenantId}, Reason={Reason}",
            id, subscription.TenantId, request.Reason);

        // Notify M360 about the denial
        await NotifyM360SubscriptionStatusChangeAsync(subscription, cancellationToken);

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
        var subscription = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription == null)
            return NotFound();

        if (subscription.Status != SubscriptionRequestStatus.Approved)
            return BadRequest(new { error = "InvalidStatus", message = "Can only suspend approved subscriptions" });

        subscription.Status = SubscriptionRequestStatus.Suspended;
        subscription.SuspendedAt = DateTime.UtcNow;
        subscription.Notes = request.Notes ?? subscription.Notes;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        _logger.LogInformation("Suspended subscription {Id} for TenantId={TenantId}", id, subscription.TenantId);

        // Notify M360 about the suspension
        await NotifyM360SubscriptionStatusChangeAsync(subscription, cancellationToken);

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
        var subscription = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription == null)
            return NotFound();

        if (subscription.Status != SubscriptionRequestStatus.Suspended)
            return BadRequest(new { error = "InvalidStatus", message = "Can only reactivate suspended subscriptions" });

        subscription.Status = SubscriptionRequestStatus.Approved;
        subscription.SuspendedAt = null;
        subscription.SuspendedByUserId = null;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        _logger.LogInformation("Reactivated subscription {Id} for TenantId={TenantId}", id, subscription.TenantId);

        // Notify M360 about the reactivation
        await NotifyM360SubscriptionStatusChangeAsync(subscription, cancellationToken);

        return Ok(new { success = true });
    }

    private MerchantSubscriptionDto MapToDto(MerchantSubscriptionRequest entity, Dictionary<int, (string Name, string Code)> merchantNames)
    {
        merchantNames.TryGetValue(entity.TenantId, out var merchant);

        return new MerchantSubscriptionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TenantName = merchant.Name ?? $"Tenant {entity.TenantId}",
            TenantCode = merchant.Code ?? $"T{entity.TenantId}",
            TradingPartnerId = entity.TradingPartnerId,
            TradingPartnerCode = entity.TradingPartner?.Code,
            TradingPartnerName = entity.TradingPartner?.Name,
            AccountNumber = entity.AccountNumber,
            Status = entity.Status.ToString(),
            RequestedAt = entity.RequestedAt,
            ApprovedAt = entity.ApprovedAt,
            ApprovedByUserId = entity.ApprovedByUserId,
            DenialReason = entity.DenialReason,
            Notes = entity.Notes,
            SuspendedAt = entity.SuspendedAt,
            SuspendedByUserId = entity.SuspendedByUserId
        };
    }

    private async Task<Dictionary<int, (string Name, string Code)>> GetMerchantNamesAsync(
        IEnumerable<int> tenantIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, (string Name, string Code)>();

        try
        {
            var merchants = await _merchant360Client.GetMerchantsAsync(activeOnly: false, cancellationToken);
            foreach (var id in tenantIds)
            {
                var merchant = merchants.FirstOrDefault(m => m.Id == id);
                if (merchant != null)
                {
                    result[id] = (merchant.Name, merchant.Code ?? $"T{id}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get merchant names from M360, using defaults");
        }

        return result;
    }

    private async Task NotifyM360SubscriptionStatusChangeAsync(
        MerchantSubscriptionRequest subscription,
        CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Implement webhook or API call to notify M360 about status changes
            _logger.LogInformation("Would notify M360 about subscription {Id} status change to {Status}",
                subscription.Id, subscription.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify M360 about subscription status change");
        }
    }
}

#region DTOs

public class SubscriptionListResult
{
    public int Total { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int DeniedCount { get; set; }
    public int SuspendedCount { get; set; }
    public List<MerchantSubscriptionDto> Items { get; set; } = new();
}

public class MerchantSubscriptionDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantCode { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public string? TradingPartnerName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApprovedByUserName { get; set; }
    public string? DenialReason { get; set; }
    public string? Notes { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public int? SuspendedByUserId { get; set; }
    public string? SuspendedByUserName { get; set; }
}

public class CreateSubscriptionDto
{
    public int TenantId { get; set; }
    public int TradingPartnerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ApproveSubscriptionDto
{
    public string? Notes { get; set; }
}

public class DenySubscriptionDto
{
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class SuspendSubscriptionDto
{
    public string? Notes { get; set; }
}

#endregion
