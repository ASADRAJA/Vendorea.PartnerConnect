using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Webhooks.Services;

/// <summary>
/// Service for delivering webhooks via HTTP.
/// Handles the actual HTTP POST to webhook endpoints with retry logic.
/// </summary>
public class WebhookDeliveryService : IWebhookDeliveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebhookDeliveryRepository _deliveryRepository;
    private readonly IWebhookSubscriptionRepository _subscriptionRepository;
    private readonly ILogger<WebhookDeliveryService> _logger;
    private readonly WebhookDeliveryOptions _options;

    public WebhookDeliveryService(
        IHttpClientFactory httpClientFactory,
        IWebhookDeliveryRepository deliveryRepository,
        IWebhookSubscriptionRepository subscriptionRepository,
        ILogger<WebhookDeliveryService> logger,
        WebhookDeliveryOptions? options = null)
    {
        _httpClientFactory = httpClientFactory;
        _deliveryRepository = deliveryRepository;
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
        _options = options ?? new WebhookDeliveryOptions();
    }

    /// <inheritdoc />
    public async Task<WebhookDeliveryResult> DeliverAsync(
        WebhookDelivery delivery,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogDebug(
                "Delivering webhook {DeliveryId} to {TargetUrl}",
                delivery.Id, delivery.TargetUrl);

            using var httpClient = _httpClientFactory.CreateClient("WebhookDelivery");
            httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            var request = CreateRequest(delivery);
            var response = await httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncatedBody = responseBody.Length > _options.MaxResponseBodyLength
                ? responseBody[.._options.MaxResponseBodyLength]
                : responseBody;

            // Update delivery record
            delivery.HttpStatusCode = (int)response.StatusCode;
            delivery.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            delivery.AttemptCount++;
            delivery.ResponseBody = truncatedBody;

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = WebhookDeliveryStatus.Delivered;
                delivery.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Webhook {DeliveryId} delivered successfully in {Duration}ms",
                    delivery.Id, stopwatch.ElapsedMilliseconds);

                await ResetSubscriptionFailuresAsync(delivery.WebhookSubscriptionId, cancellationToken);

                return WebhookDeliveryResult.Succeeded(
                    delivery.Id,
                    (int)response.StatusCode,
                    (int)stopwatch.ElapsedMilliseconds,
                    truncatedBody);
            }
            else
            {
                var errorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                delivery.ErrorMessage = errorMessage;
                HandleDeliveryFailure(delivery);

                _logger.LogWarning(
                    "Webhook {DeliveryId} failed with status {StatusCode}",
                    delivery.Id, response.StatusCode);

                await IncrementSubscriptionFailuresAsync(delivery.WebhookSubscriptionId, cancellationToken);

                return WebhookDeliveryResult.Failed(
                    delivery.Id,
                    errorMessage,
                    (int)response.StatusCode,
                    (int)stopwatch.ElapsedMilliseconds,
                    delivery.AttemptCount < _options.MaxRetries);
            }
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return HandleException(delivery, ex, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HandleException(delivery, ex, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            await _deliveryRepository.UpdateAsync(delivery, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebhookDeliveryResult>> DeliverBatchAsync(
        IEnumerable<WebhookDelivery> deliveries,
        CancellationToken cancellationToken = default)
    {
        var results = new List<WebhookDeliveryResult>();

        foreach (var delivery in deliveries)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var result = await DeliverAsync(delivery, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<WebhookDeliveryResult> DeliverImmediateAsync(
        string targetUrl,
        string payload,
        string eventType,
        string? signature = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var deliveryId = Guid.NewGuid();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Immediate webhook delivery to {TargetUrl}", targetUrl);

            using var httpClient = _httpClientFactory.CreateClient("WebhookDelivery");
            httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            var request = new HttpRequestMessage(HttpMethod.Post, targetUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("X-Webhook-Event", eventType);
            request.Headers.Add("X-Webhook-Delivery-Id", deliveryId.ToString());
            request.Headers.Add("X-Webhook-Timestamp", DateTime.UtcNow.ToString("O"));

            if (!string.IsNullOrEmpty(signature))
            {
                request.Headers.Add("X-Webhook-Signature", signature);
            }

            if (!string.IsNullOrEmpty(correlationId))
            {
                request.Headers.Add("X-Correlation-Id", correlationId);
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncatedBody = responseBody.Length > _options.MaxResponseBodyLength
                ? responseBody[.._options.MaxResponseBodyLength]
                : responseBody;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Immediate webhook delivered successfully in {Duration}ms",
                    stopwatch.ElapsedMilliseconds);

                return WebhookDeliveryResult.Succeeded(
                    deliveryId,
                    (int)response.StatusCode,
                    (int)stopwatch.ElapsedMilliseconds,
                    truncatedBody);
            }
            else
            {
                var errorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";

                _logger.LogWarning(
                    "Immediate webhook failed with status {StatusCode}",
                    response.StatusCode);

                return WebhookDeliveryResult.Failed(
                    deliveryId,
                    errorMessage,
                    (int)response.StatusCode,
                    (int)stopwatch.ElapsedMilliseconds,
                    willRetry: false);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Error in immediate webhook delivery");

            return WebhookDeliveryResult.Failed(
                deliveryId,
                ex.Message,
                httpStatusCode: null,
                (int)stopwatch.ElapsedMilliseconds,
                willRetry: false);
        }
    }

    /// <inheritdoc />
    public async Task<int> ProcessPendingDeliveriesAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var deliveries = await _deliveryRepository.GetPendingAsync(batchSize, cancellationToken);
        var results = await DeliverBatchAsync(deliveries, cancellationToken);
        return results.Count(r => r.Success);
    }

    /// <inheritdoc />
    public async Task<int> ProcessRetryDeliveriesAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var deliveries = await _deliveryRepository.GetRetryDueAsync(batchSize, cancellationToken);
        var results = await DeliverBatchAsync(deliveries, cancellationToken);
        return results.Count(r => r.Success);
    }

    /// <inheritdoc />
    public async Task<WebhookDeliveryStats> GetDeliveryStatsAsync(
        int? dealerId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-7);
        var end = endDate ?? DateTime.UtcNow;

        var deliveries = dealerId.HasValue
            ? await _deliveryRepository.GetByDealerIdAsync(dealerId.Value, start, end, cancellationToken)
            : await _deliveryRepository.GetAllInRangeAsync(start, end, cancellationToken);

        return new WebhookDeliveryStats
        {
            StartDate = start,
            EndDate = end,
            TotalDeliveries = deliveries.Count,
            SuccessfulDeliveries = deliveries.Count(d => d.Status == WebhookDeliveryStatus.Delivered),
            FailedDeliveries = deliveries.Count(d => d.Status == WebhookDeliveryStatus.Failed),
            PendingDeliveries = deliveries.Count(d => d.Status == WebhookDeliveryStatus.Pending),
            RetryingDeliveries = deliveries.Count(d => d.Status == WebhookDeliveryStatus.Retry),
            AverageDurationMs = deliveries.Any(d => d.DurationMs.HasValue && d.DurationMs > 0)
                ? deliveries.Where(d => d.DurationMs.HasValue && d.DurationMs > 0).Average(d => d.DurationMs!.Value)
                : 0,
            ByEventType = deliveries
                .GroupBy(d => d.EventType)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByStatus = deliveries
                .GroupBy(d => d.Status.ToString())
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private HttpRequestMessage CreateRequest(WebhookDelivery delivery)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, delivery.TargetUrl)
        {
            Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Webhook-Event", delivery.EventType);
        request.Headers.Add("X-Webhook-Delivery-Id", delivery.Id.ToString());
        request.Headers.Add("X-Webhook-Timestamp", delivery.CreatedAt.ToString("O"));
        request.Headers.Add("X-Webhook-Attempt", delivery.AttemptCount.ToString());

        if (!string.IsNullOrEmpty(delivery.Signature))
        {
            request.Headers.Add("X-Webhook-Signature", delivery.Signature);
        }

        if (!string.IsNullOrEmpty(delivery.CorrelationId))
        {
            request.Headers.Add("X-Correlation-Id", delivery.CorrelationId);
        }

        return request;
    }

    private void HandleDeliveryFailure(WebhookDelivery delivery)
    {
        if (delivery.AttemptCount < _options.MaxRetries)
        {
            // Exponential backoff with configured base delay
            var delaySeconds = Math.Pow(2, delivery.AttemptCount - 1) * _options.BaseRetryDelaySeconds;
            delivery.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
            delivery.Status = WebhookDeliveryStatus.Retry;
        }
        else
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
        }
    }

    private WebhookDeliveryResult HandleException(
        WebhookDelivery delivery,
        Exception ex,
        long elapsedMs)
    {
        delivery.DurationMs = (int)elapsedMs;
        delivery.AttemptCount++;
        delivery.ErrorMessage = ex.Message;
        HandleDeliveryFailure(delivery);

        _logger.LogError(ex, "Error delivering webhook {DeliveryId}", delivery.Id);

        // Fire and forget - increment failures
        _ = IncrementSubscriptionFailuresAsync(delivery.WebhookSubscriptionId, CancellationToken.None);

        return WebhookDeliveryResult.Failed(
            delivery.Id,
            ex.Message,
            httpStatusCode: null,
            (int)elapsedMs,
            delivery.AttemptCount < _options.MaxRetries);
    }

    private async Task ResetSubscriptionFailuresAsync(
        int subscriptionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
            if (subscription != null && subscription.ConsecutiveFailures > 0)
            {
                subscription.ConsecutiveFailures = 0;
                subscription.UpdatedAt = DateTime.UtcNow;
                await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset subscription failures for {SubscriptionId}", subscriptionId);
        }
    }

    private async Task IncrementSubscriptionFailuresAsync(
        int subscriptionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
            if (subscription != null)
            {
                subscription.ConsecutiveFailures++;
                subscription.LastFailureAt = DateTime.UtcNow;
                subscription.UpdatedAt = DateTime.UtcNow;

                // Auto-suspend after too many failures
                if (subscription.ConsecutiveFailures >= _options.SuspendAfterFailures && !subscription.IsSuspended)
                {
                    subscription.IsSuspended = true;
                    subscription.SuspendedAt = DateTime.UtcNow;
                    subscription.SuspensionReason = $"Auto-suspended after {subscription.ConsecutiveFailures} consecutive failures";

                    _logger.LogWarning(
                        "Webhook subscription {SubscriptionId} auto-suspended after {Failures} failures",
                        subscriptionId, subscription.ConsecutiveFailures);
                }

                await _subscriptionRepository.UpdateAsync(subscription, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment subscription failures for {SubscriptionId}", subscriptionId);
        }
    }
}

/// <summary>
/// Interface for webhook delivery service.
/// </summary>
public interface IWebhookDeliveryService
{
    /// <summary>
    /// Delivers a single webhook.
    /// </summary>
    Task<WebhookDeliveryResult> DeliverAsync(
        WebhookDelivery delivery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers multiple webhooks.
    /// </summary>
    Task<IReadOnlyList<WebhookDeliveryResult>> DeliverBatchAsync(
        IEnumerable<WebhookDelivery> deliveries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers a webhook immediately without persistence.
    /// </summary>
    Task<WebhookDeliveryResult> DeliverImmediateAsync(
        string targetUrl,
        string payload,
        string eventType,
        string? signature = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes pending deliveries.
    /// </summary>
    Task<int> ProcessPendingDeliveriesAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes deliveries due for retry.
    /// </summary>
    Task<int> ProcessRetryDeliveriesAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets delivery statistics.
    /// </summary>
    Task<WebhookDeliveryStats> GetDeliveryStatsAsync(
        int? dealerId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a webhook delivery attempt.
/// </summary>
public record WebhookDeliveryResult
{
    public Guid DeliveryId { get; init; }
    public bool Success { get; init; }
    public int? HttpStatusCode { get; init; }
    public int DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ResponseBody { get; init; }
    public bool WillRetry { get; init; }

    public static WebhookDeliveryResult Succeeded(
        Guid deliveryId,
        int httpStatusCode,
        int durationMs,
        string? responseBody = null)
    {
        return new WebhookDeliveryResult
        {
            DeliveryId = deliveryId,
            Success = true,
            HttpStatusCode = httpStatusCode,
            DurationMs = durationMs,
            ResponseBody = responseBody,
            WillRetry = false
        };
    }

    public static WebhookDeliveryResult Failed(
        Guid deliveryId,
        string errorMessage,
        int? httpStatusCode,
        int durationMs,
        bool willRetry)
    {
        return new WebhookDeliveryResult
        {
            DeliveryId = deliveryId,
            Success = false,
            HttpStatusCode = httpStatusCode,
            DurationMs = durationMs,
            ErrorMessage = errorMessage,
            WillRetry = willRetry
        };
    }
}

/// <summary>
/// Statistics for webhook deliveries.
/// </summary>
public record WebhookDeliveryStats
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int TotalDeliveries { get; init; }
    public int SuccessfulDeliveries { get; init; }
    public int FailedDeliveries { get; init; }
    public int PendingDeliveries { get; init; }
    public int RetryingDeliveries { get; init; }
    public double AverageDurationMs { get; init; }
    public double SuccessRate => TotalDeliveries > 0
        ? (double)SuccessfulDeliveries / TotalDeliveries * 100
        : 0;
    public Dictionary<string, int> ByEventType { get; init; } = new();
    public Dictionary<string, int> ByStatus { get; init; } = new();
}

/// <summary>
/// Options for webhook delivery.
/// </summary>
public class WebhookDeliveryOptions
{
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 5;
    public int BaseRetryDelaySeconds { get; set; } = 30;
    public int MaxResponseBodyLength { get; set; } = 2000;
    public int SuspendAfterFailures { get; set; } = 10;
}
