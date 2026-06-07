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
    private readonly IDealerPartnerConnectionRepository _connectionRepository;
    private readonly IMerchant360Client _merchant360Client;
    private readonly ILogger<AdminSubscriptionsController> _logger;

    public AdminSubscriptionsController(
        IMerchantSubscriptionRequestRepository subscriptionRepository,
        IDealerPartnerConnectionRepository connectionRepository,
        IMerchant360Client merchant360Client,
        ILogger<AdminSubscriptionsController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _connectionRepository = connectionRepository;
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
            TenantName = request.TenantName,
            TenantCode = request.TenantCode,
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

        var previousStatus = subscription.Status;
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

        // Create or update the DealerPartnerConnection
        await CreateOrUpdateConnectionAsync(subscription, ConnectionStatus.Active, cancellationToken);

        _logger.LogInformation("Approved subscription {Id} for TenantId={TenantId}, TradingPartnerId={TradingPartnerId}",
            id, subscription.TenantId, subscription.TradingPartnerId);

        // Notify M360 about the approval
        await NotifyM360SubscriptionStatusChangeAsync(subscription, previousStatus, cancellationToken);

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

        var previousStatus = subscription.Status;
        subscription.Status = SubscriptionRequestStatus.Denied;
        subscription.DeniedAt = DateTime.UtcNow;
        subscription.DenialReason = request.Reason;
        subscription.Notes = request.Notes ?? subscription.Notes;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        _logger.LogInformation("Denied subscription {Id} for TenantId={TenantId}, Reason={Reason}",
            id, subscription.TenantId, request.Reason);

        // Notify M360 about the denial
        await NotifyM360SubscriptionStatusChangeAsync(subscription, previousStatus, cancellationToken);

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

        var previousStatus = subscription.Status;
        subscription.Status = SubscriptionRequestStatus.Suspended;
        subscription.SuspendedAt = DateTime.UtcNow;
        subscription.Notes = request.Notes ?? subscription.Notes;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        // Update the DealerPartnerConnection status
        await UpdateConnectionStatusAsync(subscription.TenantId, subscription.TradingPartnerId, ConnectionStatus.Suspended, cancellationToken);

        _logger.LogInformation("Suspended subscription {Id} for TenantId={TenantId}", id, subscription.TenantId);

        // Notify M360 about the suspension
        await NotifyM360SubscriptionStatusChangeAsync(subscription, previousStatus, cancellationToken);

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

        // Update the DealerPartnerConnection status back to Active
        await UpdateConnectionStatusAsync(subscription.TenantId, subscription.TradingPartnerId, ConnectionStatus.Active, cancellationToken);

        _logger.LogInformation("Reactivated subscription {Id} for TenantId={TenantId}", id, subscription.TenantId);

        // Notify M360 about the reactivation
        await NotifyM360SubscriptionStatusChangeAsync(subscription, SubscriptionRequestStatus.Suspended, cancellationToken);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Unsubscribes/terminates an active subscription (admin-initiated).
    /// </summary>
    [HttpPost("{id:int}/unsubscribe")]
    public async Task<IActionResult> UnsubscribeSubscription(
        int id,
        [FromBody] UnsubscribeSubscriptionDto? request = null,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription == null)
            return NotFound();

        if (subscription.Status != SubscriptionRequestStatus.Approved &&
            subscription.Status != SubscriptionRequestStatus.Suspended)
        {
            return BadRequest(new { error = "InvalidStatus", message = "Can only unsubscribe from approved or suspended subscriptions" });
        }

        var previousStatus = subscription.Status;
        subscription.Status = SubscriptionRequestStatus.Cancelled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.Notes = request?.Notes ?? subscription.Notes;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        // Update the DealerPartnerConnection status to Disconnected
        await UpdateConnectionStatusAsync(subscription.TenantId, subscription.TradingPartnerId, ConnectionStatus.Disconnected, cancellationToken);

        _logger.LogInformation("Admin unsubscribed subscription {Id} for TenantId={TenantId}", id, subscription.TenantId);

        // Notify M360 about the unsubscription
        await NotifyM360SubscriptionStatusChangeAsync(subscription, previousStatus, cancellationToken);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Manually resync a subscription status to M360.
    /// Use this when M360 callback failed and needs to be retried.
    /// </summary>
    [HttpPost("{id:int}/resync")]
    public async Task<IActionResult> ResyncSubscription(
        int id,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(id, cancellationToken);
        if (subscription == null)
            return NotFound();

        _logger.LogInformation("Manual resync requested for subscription {Id}, TenantId={TenantId}, Status={Status}",
            id, subscription.TenantId, subscription.Status);

        // Ensure DealerPartnerConnection exists for approved/suspended subscriptions
        if (subscription.Status == SubscriptionRequestStatus.Approved)
        {
            await CreateOrUpdateConnectionAsync(subscription, ConnectionStatus.Active, cancellationToken);
        }
        else if (subscription.Status == SubscriptionRequestStatus.Suspended)
        {
            await CreateOrUpdateConnectionAsync(subscription, ConnectionStatus.Suspended, cancellationToken);
        }

        // Get the connection ID for approved/suspended subscriptions
        int? connectionId = null;
        if (subscription.Status == SubscriptionRequestStatus.Approved || subscription.Status == SubscriptionRequestStatus.Suspended)
        {
            var connection = await _connectionRepository.GetByDealerAndPartnerAsync(
                subscription.TenantId, subscription.TradingPartnerId, cancellationToken);
            connectionId = connection?.Id;
        }

        var statusChange = new SubscriptionStatusChangedDto
        {
            TenantId = subscription.TenantId,
            TradingPartnerId = subscription.TradingPartnerId,
            TradingPartnerCode = subscription.TradingPartner?.Code ?? string.Empty,
            AccountNumber = subscription.AccountNumber,
            PreviousStatus = subscription.Status.ToString(), // Same as current since this is a resync
            NewStatus = subscription.Status.ToString(),
            ChangedAt = DateTime.UtcNow,
            ChangedBy = "admin-resync",
            Reason = subscription.DenialReason,
            Notes = subscription.Notes,
            // Include PC integration IDs
            PartnerConnectConnectionId = connectionId,
            PCOrganizationId = connectionId != null ? 1 : null,
            PCMerchantId = connectionId != null ? subscription.TenantId : null
        };

        var success = await _merchant360Client.NotifySubscriptionStatusChangedAsync(statusChange, cancellationToken);

        if (success)
        {
            _logger.LogInformation("Successfully resynced subscription {Id} to M360", id);
            return Ok(new { success = true, message = "Subscription status synced to M360 and connection ensured" });
        }
        else
        {
            _logger.LogWarning("Failed to resync subscription {Id} to M360", id);
            return Ok(new { success = true, message = "Connection ensured but M360 sync failed - may need manual sync" });
        }
    }

    /// <summary>
    /// Gets merchants with their active partner subscriptions.
    /// Used by price feed and content pages to filter dropdowns.
    /// Uses locally stored tenant names (no M360 call needed).
    /// </summary>
    [HttpGet("active-by-merchant")]
    public async Task<IActionResult> GetActiveSubscriptionsByMerchant(CancellationToken cancellationToken = default)
    {
        // Get all approved subscriptions
        var approvedSubscriptions = await _subscriptionRepository.GetByStatusAsync(
            SubscriptionRequestStatus.Approved, cancellationToken);

        // Group by tenant (merchant) - use locally stored names
        var result = approvedSubscriptions
            .GroupBy(s => s.TenantId)
            .Select(g =>
            {
                var firstSub = g.First();
                return new MerchantWithSubscriptionsDto
                {
                    MerchantId = g.Key,
                    MerchantName = firstSub.TenantName ?? $"Merchant {g.Key}",
                    MerchantCode = firstSub.TenantCode ?? $"M{g.Key}",
                    Partners = g.Select(s => new SubscribedPartnerDto
                    {
                        TradingPartnerId = s.TradingPartnerId,
                        TradingPartnerCode = s.TradingPartner?.Code ?? "",
                        TradingPartnerName = s.TradingPartner?.Name ?? $"Partner {s.TradingPartnerId}",
                        AccountNumber = s.AccountNumber
                    }).ToList()
                };
            })
            .OrderBy(m => m.MerchantName)
            .ToList();

        return Ok(result);
    }

    private MerchantSubscriptionDto MapToDto(MerchantSubscriptionRequest entity, Dictionary<int, (string Name, string Code)> merchantNames)
    {
        // Prefer locally stored tenant name/code, fall back to M360 lookup
        merchantNames.TryGetValue(entity.TenantId, out var merchant);

        return new MerchantSubscriptionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TenantName = entity.TenantName ?? merchant.Name ?? $"Tenant {entity.TenantId}",
            TenantCode = entity.TenantCode ?? merchant.Code ?? $"T{entity.TenantId}",
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
            // Use a short timeout (5 seconds) for M360 calls to avoid blocking the UI
            // If M360 is slow or unavailable, we'll use fallback names
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var merchants = await _merchant360Client.GetMerchantsAsync(activeOnly: false, linkedCts.Token);
            foreach (var id in tenantIds)
            {
                var merchant = merchants.FirstOrDefault(m => m.Id == id);
                if (merchant != null)
                {
                    result[id] = (merchant.Name, merchant.Code ?? $"T{id}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("M360 merchant names request timed out, using defaults");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get merchant names from M360, using defaults");
        }

        return result;
    }

    private async Task NotifyM360SubscriptionStatusChangeAsync(
        MerchantSubscriptionRequest subscription,
        SubscriptionRequestStatus previousStatus,
        CancellationToken cancellationToken)
    {
        // Get the connection ID for approved subscriptions
        int? connectionId = null;
        if (subscription.Status == SubscriptionRequestStatus.Approved)
        {
            var connection = await _connectionRepository.GetByDealerAndPartnerAsync(
                subscription.TenantId, subscription.TradingPartnerId, cancellationToken);
            connectionId = connection?.Id;
        }

        var statusChange = new SubscriptionStatusChangedDto
        {
            TenantId = subscription.TenantId,
            TradingPartnerId = subscription.TradingPartnerId,
            TradingPartnerCode = subscription.TradingPartner?.Code ?? string.Empty,
            AccountNumber = subscription.AccountNumber,
            PreviousStatus = previousStatus.ToString(),
            NewStatus = subscription.Status.ToString(),
            ChangedAt = DateTime.UtcNow,
            ChangedBy = "admin", // TODO: Get actual admin user from context
            Reason = subscription.DenialReason,
            Notes = subscription.Notes,
            // Include PC integration IDs for approved subscriptions
            PartnerConnectConnectionId = connectionId,
            PCOrganizationId = subscription.Status == SubscriptionRequestStatus.Approved ? 1 : null,
            PCMerchantId = subscription.Status == SubscriptionRequestStatus.Approved ? subscription.TenantId : null
        };

        try
        {
            var success = await _merchant360Client.NotifySubscriptionStatusChangedAsync(statusChange, cancellationToken);

            if (!success)
            {
                _logger.LogWarning("Failed to notify M360 about subscription status change for TenantId={TenantId}, TradingPartnerId={TradingPartnerId}. M360 may need to sync manually.",
                    subscription.TenantId, subscription.TradingPartnerId);
                // Don't throw - the local status change succeeded, M360 can sync later
            }
            else
            {
                _logger.LogInformation("Notified M360 about subscription status change: TenantId={TenantId}, {PreviousStatus} -> {NewStatus}",
                    subscription.TenantId, previousStatus, subscription.Status);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - M360 may not have the endpoint implemented yet
            _logger.LogWarning(ex, "Exception notifying M360 about subscription status change for TenantId={TenantId}, TradingPartnerId={TradingPartnerId}. M360 may need to sync manually.",
                subscription.TenantId, subscription.TradingPartnerId);
        }
    }

    private async Task CreateOrUpdateConnectionAsync(
        MerchantSubscriptionRequest subscription,
        ConnectionStatus status,
        CancellationToken cancellationToken)
    {
        var existingConnection = await _connectionRepository.GetByDealerAndPartnerAsync(
            subscription.TenantId, subscription.TradingPartnerId, cancellationToken);

        if (existingConnection != null)
        {
            existingConnection.Status = status;
            existingConnection.UpdatedAt = DateTime.UtcNow;
            if (status == ConnectionStatus.Active)
            {
                existingConnection.ConnectedAt = DateTime.UtcNow;
                existingConnection.DisconnectedAt = null;
            }
            await _connectionRepository.UpdateAsync(existingConnection, cancellationToken);
            _logger.LogInformation("Updated DealerPartnerConnection for DealerId={DealerId}, TradingPartnerId={TradingPartnerId} to {Status}",
                subscription.TenantId, subscription.TradingPartnerId, status);
        }
        else
        {
            var connection = new DealerPartnerConnection
            {
                DealerId = subscription.TenantId,
                TradingPartnerId = subscription.TradingPartnerId,
                ExternalAccountId = subscription.AccountNumber,
                Status = status,
                ConnectedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _connectionRepository.AddAsync(connection, cancellationToken);
            _logger.LogInformation("Created DealerPartnerConnection for DealerId={DealerId}, TradingPartnerId={TradingPartnerId} with Status={Status}",
                subscription.TenantId, subscription.TradingPartnerId, status);
        }
    }

    private async Task UpdateConnectionStatusAsync(
        int dealerId,
        int tradingPartnerId,
        ConnectionStatus status,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByDealerAndPartnerAsync(dealerId, tradingPartnerId, cancellationToken);
        if (connection != null)
        {
            connection.Status = status;
            connection.UpdatedAt = DateTime.UtcNow;
            if (status == ConnectionStatus.Disconnected)
            {
                connection.DisconnectedAt = DateTime.UtcNow;
            }
            await _connectionRepository.UpdateAsync(connection, cancellationToken);
            _logger.LogInformation("Updated DealerPartnerConnection status for DealerId={DealerId}, TradingPartnerId={TradingPartnerId} to {Status}",
                dealerId, tradingPartnerId, status);
        }
        else
        {
            _logger.LogWarning("No DealerPartnerConnection found for DealerId={DealerId}, TradingPartnerId={TradingPartnerId} to update status",
                dealerId, tradingPartnerId);
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
    public string? TenantName { get; set; }
    public string? TenantCode { get; set; }
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

public class UnsubscribeSubscriptionDto
{
    public string? Notes { get; set; }
}

public class MerchantWithSubscriptionsDto
{
    public int MerchantId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public string MerchantCode { get; set; } = string.Empty;
    public List<SubscribedPartnerDto> Partners { get; set; } = new();
}

public class SubscribedPartnerDto
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public string TradingPartnerName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}

#endregion
