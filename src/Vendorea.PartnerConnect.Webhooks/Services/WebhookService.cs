using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Webhooks.Interfaces;

namespace Vendorea.PartnerConnect.Webhooks.Services;

/// <summary>
/// Service for managing webhook subscriptions and triggering events.
/// </summary>
public class WebhookService : IWebhookService
{
    private readonly IWebhookSubscriptionRepository _subscriptionRepository;
    private readonly IWebhookDeliveryRepository _deliveryRepository;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<WebhookService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public WebhookService(
        IWebhookSubscriptionRepository subscriptionRepository,
        IWebhookDeliveryRepository deliveryRepository,
        IOutboxService outboxService,
        ILogger<WebhookService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _deliveryRepository = deliveryRepository;
        _outboxService = outboxService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WebhookSubscription> CreateSubscriptionAsync(
        int dealerId,
        string name,
        string targetUrl,
        IEnumerable<string> events,
        CancellationToken cancellationToken = default)
    {
        var subscription = new WebhookSubscription
        {
            DealerId = dealerId,
            Name = name,
            TargetUrl = targetUrl,
            Events = events.ToList(),
            Secret = GenerateSecret(),
            IsActive = true
        };

        await _subscriptionRepository.AddAsync(subscription, cancellationToken);

        _logger.LogInformation(
            "Created webhook subscription {SubscriptionId} for dealer {DealerId}",
            subscription.Id, dealerId);

        return subscription;
    }

    /// <inheritdoc />
    public async Task<WebhookSubscription?> UpdateSubscriptionAsync(
        int subscriptionId,
        string? name = null,
        string? targetUrl = null,
        IEnumerable<string>? events = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null)
        {
            return null;
        }

        if (name != null) subscription.Name = name;
        if (targetUrl != null) subscription.TargetUrl = targetUrl;
        if (events != null) subscription.Events = events.ToList();
        if (isActive.HasValue) subscription.IsActive = isActive.Value;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        _logger.LogInformation("Updated webhook subscription {SubscriptionId}", subscriptionId);

        return subscription;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default)
    {
        var deleted = await _subscriptionRepository.DeleteAsync(subscriptionId, cancellationToken);

        if (deleted)
        {
            _logger.LogInformation("Deleted webhook subscription {SubscriptionId}", subscriptionId);
        }

        return deleted;
    }

    /// <inheritdoc />
    public Task<WebhookSubscription?> GetSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default)
    {
        return _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WebhookSubscription>> GetSubscriptionsByDealerAsync(
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        return _subscriptionRepository.GetByDealerIdAsync(dealerId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WebhookSubscription>> GetSubscriptionsForEventAsync(
        int dealerId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        return _subscriptionRepository.GetActiveForEventAsync(dealerId, eventType, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> TriggerEventAsync<T>(
        int dealerId,
        string eventType,
        T payload,
        string? correlationId = null,
        int? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken cancellationToken = default) where T : class
    {
        // Find all active subscriptions for this event
        var subscriptions = await _subscriptionRepository.GetActiveForEventAsync(dealerId, eventType, cancellationToken);

        if (subscriptions.Count == 0)
        {
            _logger.LogDebug(
                "No webhook subscriptions for event {EventType} for dealer {DealerId}",
                eventType, dealerId);
            return 0;
        }

        var webhookPayload = new WebhookPayload<T>
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            DealerId = dealerId,
            CorrelationId = correlationId,
            Data = payload
        };

        var payloadJson = JsonSerializer.Serialize(webhookPayload, _jsonOptions);

        // Create delivery records and enqueue to outbox
        foreach (var subscription in subscriptions)
        {
            var delivery = new WebhookDelivery
            {
                WebhookSubscriptionId = subscription.Id,
                EventType = eventType,
                Payload = payloadJson,
                TargetUrl = subscription.TargetUrl,
                CorrelationId = correlationId,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType,
                Signature = GenerateSignature(payloadJson, subscription.Secret)
            };

            await _deliveryRepository.AddAsync(delivery, cancellationToken);

            // Enqueue to outbox for reliable delivery
            await _outboxService.EnqueueWebhookAsync(
                subscription.TargetUrl,
                new WebhookOutboxPayload
                {
                    DeliveryId = delivery.Id,
                    SubscriptionId = subscription.Id,
                    Payload = payloadJson,
                    Signature = delivery.Signature
                },
                correlationId,
                cancellationToken);

            // Update subscription stats
            subscription.LastTriggeredAt = DateTime.UtcNow;
            await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
        }

        _logger.LogInformation(
            "Triggered {EventType} webhook for dealer {DealerId} to {Count} subscriptions",
            eventType, dealerId, subscriptions.Count);

        return subscriptions.Count;
    }

    /// <inheritdoc />
    public async Task<string> RegenerateSecretAsync(int subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null)
        {
            throw new InvalidOperationException($"Subscription {subscriptionId} not found");
        }

        subscription.Secret = GenerateSecret();
        subscription.UpdatedAt = DateTime.UtcNow;
        await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        _logger.LogInformation("Regenerated secret for webhook subscription {SubscriptionId}", subscriptionId);

        return subscription.Secret;
    }

    /// <inheritdoc />
    public async Task<WebhookTestResult> TestSubscriptionAsync(
        int subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription == null)
        {
            return new WebhookTestResult
            {
                Success = false,
                ErrorMessage = "Subscription not found"
            };
        }

        var testPayload = new WebhookPayload<object>
        {
            EventType = "webhook.test",
            Timestamp = DateTime.UtcNow,
            DealerId = subscription.DealerId,
            CorrelationId = Guid.NewGuid().ToString(),
            Data = new { message = "Test webhook delivery" }
        };

        var payloadJson = JsonSerializer.Serialize(testPayload, _jsonOptions);
        var signature = GenerateSignature(payloadJson, subscription.Secret);

        // Use the webhook delivery service directly for immediate test
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Webhook-Signature", signature);
        content.Headers.Add("X-Webhook-Event", "webhook.test");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await httpClient.PostAsync(subscription.TargetUrl, content, cancellationToken);
            stopwatch.Stop();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            return new WebhookTestResult
            {
                Success = response.IsSuccessStatusCode,
                HttpStatusCode = (int)response.StatusCode,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                ResponseBody = responseBody.Length > 500 ? responseBody[..500] : responseBody,
                ErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new WebhookTestResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WebhookDelivery>> GetDeliveriesAsync(
        int subscriptionId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return _deliveryRepository.GetBySubscriptionIdAsync(subscriptionId, limit, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RetryDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery == null || delivery.Status != WebhookDeliveryStatus.Failed)
        {
            return false;
        }

        delivery.Status = WebhookDeliveryStatus.Pending;
        delivery.AttemptCount = 0;
        delivery.NextRetryAt = null;
        await _deliveryRepository.UpdateAsync(delivery, cancellationToken);

        // Re-enqueue to outbox
        var subscription = await _subscriptionRepository.GetByIdAsync(delivery.WebhookSubscriptionId, cancellationToken);
        if (subscription != null)
        {
            await _outboxService.EnqueueWebhookAsync(
                delivery.TargetUrl,
                new WebhookOutboxPayload
                {
                    DeliveryId = delivery.Id,
                    SubscriptionId = subscription.Id,
                    Payload = delivery.Payload,
                    Signature = delivery.Signature
                },
                delivery.CorrelationId,
                cancellationToken);
        }

        _logger.LogInformation("Retrying webhook delivery {DeliveryId}", deliveryId);
        return true;
    }

    private static string GenerateSecret()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Standard webhook payload wrapper.
/// </summary>
public record WebhookPayload<T>
{
    public string EventType { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public int DealerId { get; init; }
    public string? CorrelationId { get; init; }
    public T? Data { get; init; }
}

/// <summary>
/// Payload for outbox webhook delivery.
/// </summary>
public record WebhookOutboxPayload
{
    public Guid DeliveryId { get; init; }
    public int SubscriptionId { get; init; }
    public string Payload { get; init; } = string.Empty;
    public string? Signature { get; init; }
}
