using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Webhooks.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Public API v1 controller for webhook management.
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous] // TODO: Restore [Authorize(AuthenticationSchemes = "ApiKey")] in production
public class PublicWebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<PublicWebhooksController> _logger;

    public PublicWebhooksController(
        IWebhookService webhookService,
        ILogger<PublicWebhooksController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    /// <summary>
    /// Gets webhook subscriptions for the authenticated dealer.
    /// </summary>
    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions(CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var subscriptions = await _webhookService.GetSubscriptionsByDealerAsync(dealerId.Value, cancellationToken);

        return Ok(subscriptions.Select(s => new
        {
            s.Id,
            s.Name,
            Url = s.TargetUrl,
            EventTypes = s.Events,
            IsEnabled = s.IsActive,
            s.CreatedAt,
            LastDeliveryAt = s.LastTriggeredAt,
            SuccessfulDeliveries = s.SuccessCount,
            FailedDeliveries = s.FailureCount
        }));
    }

    /// <summary>
    /// Creates a webhook subscription.
    /// </summary>
    [HttpPost("subscriptions")]
    public async Task<IActionResult> CreateSubscription(
        [FromBody] CreateWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        // Validate URL
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            return BadRequest("Invalid webhook URL. Must be a valid HTTP(S) URL.");
        }

        // Validate event types
        var invalidEvents = request.EventTypes.Except(WebhookEvents.All).ToList();
        if (invalidEvents.Any())
        {
            return BadRequest($"Invalid event types: {string.Join(", ", invalidEvents)}");
        }

        var subscription = await _webhookService.CreateSubscriptionAsync(
            dealerId.Value,
            request.Name,
            request.Url,
            request.EventTypes,
            cancellationToken);

        _logger.LogInformation(
            "Created webhook subscription {SubscriptionId} for dealer {DealerId}",
            subscription.Id,
            dealerId.Value);

        return CreatedAtAction(
            nameof(GetSubscription),
            new { id = subscription.Id },
            new
            {
                subscription.Id,
                subscription.Name,
                Url = subscription.TargetUrl,
                EventTypes = subscription.Events,
                IsEnabled = subscription.IsActive,
                subscription.CreatedAt
            });
    }

    /// <summary>
    /// Gets a specific webhook subscription.
    /// </summary>
    [HttpGet("subscriptions/{id:int}")]
    public async Task<IActionResult> GetSubscription(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var subscription = await _webhookService.GetSubscriptionAsync(id, cancellationToken);

        if (subscription == null || subscription.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        return Ok(new
        {
            subscription.Id,
            subscription.Name,
            Url = subscription.TargetUrl,
            EventTypes = subscription.Events,
            IsEnabled = subscription.IsActive,
            subscription.CreatedAt,
            LastDeliveryAt = subscription.LastTriggeredAt,
            SuccessfulDeliveries = subscription.SuccessCount,
            FailedDeliveries = subscription.FailureCount,
            subscription.ConsecutiveFailures
        });
    }

    /// <summary>
    /// Updates a webhook subscription.
    /// </summary>
    [HttpPut("subscriptions/{id:int}")]
    public async Task<IActionResult> UpdateSubscription(
        int id,
        [FromBody] UpdateWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var subscription = await _webhookService.GetSubscriptionAsync(id, cancellationToken);

        if (subscription == null || subscription.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        // Validate URL if provided
        if (!string.IsNullOrEmpty(request.Url))
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http"))
            {
                return BadRequest("Invalid webhook URL");
            }
        }

        var updated = await _webhookService.UpdateSubscriptionAsync(
            id,
            request.Name,
            request.Url,
            request.EventTypes,
            null,
            cancellationToken);

        if (updated == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            updated.Id,
            updated.Name,
            Url = updated.TargetUrl,
            EventTypes = updated.Events,
            IsEnabled = updated.IsActive
        });
    }

    /// <summary>
    /// Deletes a webhook subscription.
    /// </summary>
    [HttpDelete("subscriptions/{id:int}")]
    public async Task<IActionResult> DeleteSubscription(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var subscription = await _webhookService.GetSubscriptionAsync(id, cancellationToken);

        if (subscription == null || subscription.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        await _webhookService.DeleteSubscriptionAsync(id, cancellationToken);

        _logger.LogInformation(
            "Deleted webhook subscription {SubscriptionId} for dealer {DealerId}",
            id,
            dealerId.Value);

        return NoContent();
    }

    /// <summary>
    /// Enables a webhook subscription.
    /// </summary>
    [HttpPost("subscriptions/{id:int}/enable")]
    public async Task<IActionResult> EnableSubscription(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var subscription = await _webhookService.GetSubscriptionAsync(id, cancellationToken);

        if (subscription == null || subscription.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        var updated = await _webhookService.UpdateSubscriptionAsync(id, isActive: true, cancellationToken: cancellationToken);

        return Ok(new { updated?.Id, IsEnabled = updated?.IsActive });
    }

    /// <summary>
    /// Disables a webhook subscription.
    /// </summary>
    [HttpPost("subscriptions/{id:int}/disable")]
    public async Task<IActionResult> DisableSubscription(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var subscription = await _webhookService.GetSubscriptionAsync(id, cancellationToken);

        if (subscription == null || subscription.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        var updated = await _webhookService.UpdateSubscriptionAsync(id, isActive: false, cancellationToken: cancellationToken);

        return Ok(new { updated?.Id, IsEnabled = updated?.IsActive });
    }

    /// <summary>
    /// Tests a webhook subscription by sending a test event.
    /// </summary>
    [HttpPost("subscriptions/{id:int}/test")]
    public async Task<IActionResult> TestSubscription(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var subscription = await _webhookService.GetSubscriptionAsync(id, cancellationToken);

        if (subscription == null || subscription.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        var result = await _webhookService.TestSubscriptionAsync(id, cancellationToken);

        return Ok(new
        {
            result.Success,
            result.HttpStatusCode,
            result.ErrorMessage,
            result.DurationMs,
            TriggeredAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gets recent webhook deliveries.
    /// </summary>
    [HttpGet("subscriptions/{id:int}/deliveries")]
    public async Task<IActionResult> GetDeliveries(
        int id,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var subscription = await _webhookService.GetSubscriptionAsync(id, cancellationToken);

        if (subscription == null || subscription.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        var deliveries = await _webhookService.GetDeliveriesAsync(id, limit, cancellationToken);

        return Ok(deliveries.Select(d => new
        {
            d.Id,
            d.WebhookSubscriptionId,
            d.EventType,
            d.Status,
            Attempts = d.AttemptCount,
            LastAttemptAt = d.AttemptedAt,
            d.NextRetryAt,
            d.HttpStatusCode,
            d.CreatedAt
        }));
    }

    /// <summary>
    /// Gets available webhook event types.
    /// </summary>
    [HttpGet("event-types")]
    [AllowAnonymous]
    public IActionResult GetEventTypes()
    {
        var eventTypes = new[]
        {
            new { Type = WebhookEvents.DocumentReceived, Description = "Triggered when a new document is received from a partner" },
            new { Type = WebhookEvents.DocumentValidated, Description = "Triggered when a document passes validation" },
            new { Type = WebhookEvents.DocumentCompleted, Description = "Triggered when a document is successfully processed" },
            new { Type = WebhookEvents.DocumentFailed, Description = "Triggered when document processing fails" },
            new { Type = WebhookEvents.DocumentQuarantined, Description = "Triggered when a document is quarantined" },
            new { Type = WebhookEvents.PricesUpdated, Description = "Triggered when prices are updated" },
            new { Type = WebhookEvents.InventoryUpdated, Description = "Triggered when inventory is updated" },
            new { Type = WebhookEvents.ConnectionActivated, Description = "Triggered when a partner connection is activated" },
            new { Type = WebhookEvents.ConnectionDeactivated, Description = "Triggered when a partner connection is deactivated" },
            new { Type = WebhookEvents.ConnectionError, Description = "Triggered when a connection error occurs" }
        };

        return Ok(eventTypes);
    }

    /// <summary>
    /// Regenerates the secret for a webhook subscription.
    /// </summary>
    [HttpPost("subscriptions/{id:int}/regenerate-secret")]
    public async Task<IActionResult> RegenerateSecret(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var subscription = await _webhookService.GetSubscriptionAsync(id, cancellationToken);

        if (subscription == null || subscription.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        var newSecret = await _webhookService.RegenerateSecretAsync(id, cancellationToken);

        return Ok(new { SubscriptionId = id, Secret = newSecret });
    }

    private int? GetDealerIdFromClaims()
    {
        var dealerIdClaim = User.FindFirst("DealerId")?.Value;
        if (int.TryParse(dealerIdClaim, out var dealerId))
        {
            return dealerId;
        }
        return null;
    }
}

public class CreateWebhookRequest
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public IEnumerable<string> EventTypes { get; set; } = Enumerable.Empty<string>();
}

public class UpdateWebhookRequest
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public IEnumerable<string>? EventTypes { get; set; }
}
