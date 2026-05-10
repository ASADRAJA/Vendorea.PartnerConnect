using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Webhooks.Interfaces;

namespace Vendorea.PartnerConnect.Api.Controllers;

/// <summary>
/// Controller for managing webhook subscriptions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IWebhookService webhookService,
        ILogger<WebhooksController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new webhook subscription.
    /// </summary>
    [HttpPost("subscriptions")]
    public async Task<IActionResult> CreateSubscription(
        [FromBody] CreateWebhookSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var subscription = await _webhookService.CreateSubscriptionAsync(
            request.DealerId,
            request.Name,
            request.TargetUrl,
            request.Events,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetSubscription),
            new { id = subscription.Id },
            MapToResponse(subscription));
    }

    /// <summary>
    /// Gets a webhook subscription by ID.
    /// </summary>
    [HttpGet("subscriptions/{id:int}")]
    public async Task<IActionResult> GetSubscription(int id, CancellationToken cancellationToken)
    {
        var subscription = await _webhookService.GetSubscriptionAsync(id, cancellationToken);

        if (subscription == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(subscription));
    }

    /// <summary>
    /// Gets all webhook subscriptions for a dealer.
    /// </summary>
    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions(
        [FromQuery] int dealerId,
        CancellationToken cancellationToken)
    {
        var subscriptions = await _webhookService.GetSubscriptionsByDealerAsync(dealerId, cancellationToken);
        return Ok(subscriptions.Select(MapToResponse));
    }

    /// <summary>
    /// Updates a webhook subscription.
    /// </summary>
    [HttpPut("subscriptions/{id:int}")]
    public async Task<IActionResult> UpdateSubscription(
        int id,
        [FromBody] UpdateWebhookSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var subscription = await _webhookService.UpdateSubscriptionAsync(
            id,
            request.Name,
            request.TargetUrl,
            request.Events,
            request.IsActive,
            cancellationToken);

        if (subscription == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(subscription));
    }

    /// <summary>
    /// Deletes a webhook subscription.
    /// </summary>
    [HttpDelete("subscriptions/{id:int}")]
    public async Task<IActionResult> DeleteSubscription(int id, CancellationToken cancellationToken)
    {
        var deleted = await _webhookService.DeleteSubscriptionAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Regenerates the secret for a subscription.
    /// </summary>
    [HttpPost("subscriptions/{id:int}/regenerate-secret")]
    public async Task<IActionResult> RegenerateSecret(int id, CancellationToken cancellationToken)
    {
        try
        {
            var newSecret = await _webhookService.RegenerateSecretAsync(id, cancellationToken);
            return Ok(new { secret = newSecret });
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Tests a webhook subscription.
    /// </summary>
    [HttpPost("subscriptions/{id:int}/test")]
    public async Task<IActionResult> TestSubscription(int id, CancellationToken cancellationToken)
    {
        var result = await _webhookService.TestSubscriptionAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets recent deliveries for a subscription.
    /// </summary>
    [HttpGet("subscriptions/{id:int}/deliveries")]
    public async Task<IActionResult> GetDeliveries(
        int id,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var deliveries = await _webhookService.GetDeliveriesAsync(id, limit, cancellationToken);
        return Ok(deliveries.Select(d => new WebhookDeliveryResponse
        {
            Id = d.Id,
            EventType = d.EventType,
            Status = d.Status.ToString(),
            TargetUrl = d.TargetUrl,
            HttpStatusCode = d.HttpStatusCode,
            ErrorMessage = d.ErrorMessage,
            AttemptCount = d.AttemptCount,
            CreatedAt = d.CreatedAt,
            CompletedAt = d.CompletedAt,
            DurationMs = d.DurationMs
        }));
    }

    /// <summary>
    /// Retries a failed delivery.
    /// </summary>
    [HttpPost("deliveries/{id:guid}/retry")]
    public async Task<IActionResult> RetryDelivery(Guid id, CancellationToken cancellationToken)
    {
        var retried = await _webhookService.RetryDeliveryAsync(id, cancellationToken);

        if (!retried)
        {
            return BadRequest("Delivery not found or not in failed status");
        }

        return Ok(new { message = "Delivery queued for retry" });
    }

    private static WebhookSubscriptionResponse MapToResponse(Domain.Entities.WebhookSubscription subscription)
    {
        return new WebhookSubscriptionResponse
        {
            Id = subscription.Id,
            DealerId = subscription.DealerId,
            Name = subscription.Name,
            TargetUrl = subscription.TargetUrl,
            Events = subscription.Events.ToList(),
            IsActive = subscription.IsActive,
            IsSuspended = subscription.IsSuspended,
            SuspensionReason = subscription.SuspensionReason,
            ConsecutiveFailures = subscription.ConsecutiveFailures,
            CreatedAt = subscription.CreatedAt,
            UpdatedAt = subscription.UpdatedAt,
            LastTriggeredAt = subscription.LastTriggeredAt
        };
    }
}

public record CreateWebhookSubscriptionRequest
{
    public required int DealerId { get; init; }
    public required string Name { get; init; }
    public required string TargetUrl { get; init; }
    public required List<string> Events { get; init; }
}

public record UpdateWebhookSubscriptionRequest
{
    public string? Name { get; init; }
    public string? TargetUrl { get; init; }
    public List<string>? Events { get; init; }
    public bool? IsActive { get; init; }
}

public record WebhookSubscriptionResponse
{
    public int Id { get; init; }
    public int DealerId { get; init; }
    public required string Name { get; init; }
    public required string TargetUrl { get; init; }
    public required List<string> Events { get; init; }
    public bool IsActive { get; init; }
    public bool IsSuspended { get; init; }
    public string? SuspensionReason { get; init; }
    public int ConsecutiveFailures { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastTriggeredAt { get; init; }
}

public record WebhookDeliveryResponse
{
    public Guid Id { get; init; }
    public required string EventType { get; init; }
    public required string Status { get; init; }
    public required string TargetUrl { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int AttemptCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? DurationMs { get; init; }
}
