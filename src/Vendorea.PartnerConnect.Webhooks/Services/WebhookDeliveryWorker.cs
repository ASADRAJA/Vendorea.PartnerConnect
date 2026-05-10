using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Webhooks.Services;

/// <summary>
/// Background worker for delivering webhooks.
/// </summary>
public class WebhookDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly int _maxRetries = 5;
    private readonly int _batchSize = 50;

    public WebhookDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook delivery worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingDeliveries(stoppingToken);
                await ProcessRetryDeliveries(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in webhook delivery worker");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Webhook delivery worker stopped");
    }

    private async Task ProcessPendingDeliveries(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var deliveryRepository = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryRepository>();
        var subscriptionRepository = scope.ServiceProvider.GetRequiredService<IWebhookSubscriptionRepository>();

        var deliveries = await deliveryRepository.GetPendingAsync(_batchSize, cancellationToken);

        foreach (var delivery in deliveries)
        {
            await DeliverWebhook(delivery, deliveryRepository, subscriptionRepository, cancellationToken);
        }
    }

    private async Task ProcessRetryDeliveries(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var deliveryRepository = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryRepository>();
        var subscriptionRepository = scope.ServiceProvider.GetRequiredService<IWebhookSubscriptionRepository>();

        var deliveries = await deliveryRepository.GetRetryDueAsync(_batchSize, cancellationToken);

        foreach (var delivery in deliveries)
        {
            await DeliverWebhook(delivery, deliveryRepository, subscriptionRepository, cancellationToken);
        }
    }

    private async Task DeliverWebhook(
        WebhookDelivery delivery,
        IWebhookDeliveryRepository deliveryRepository,
        IWebhookSubscriptionRepository subscriptionRepository,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Delivering webhook {DeliveryId} to {TargetUrl}", delivery.Id, delivery.TargetUrl);

            using var httpClient = _httpClientFactory.CreateClient("WebhookDelivery");
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json");

            // Add webhook headers
            var request = new HttpRequestMessage(HttpMethod.Post, delivery.TargetUrl)
            {
                Content = content
            };

            request.Headers.Add("X-Webhook-Event", delivery.EventType);
            request.Headers.Add("X-Webhook-Delivery-Id", delivery.Id.ToString());
            request.Headers.Add("X-Webhook-Timestamp", delivery.CreatedAt.ToString("O"));

            if (!string.IsNullOrEmpty(delivery.Signature))
            {
                request.Headers.Add("X-Webhook-Signature", delivery.Signature);
            }

            if (!string.IsNullOrEmpty(delivery.CorrelationId))
            {
                request.Headers.Add("X-Correlation-Id", delivery.CorrelationId);
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            delivery.HttpStatusCode = (int)response.StatusCode;
            delivery.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            delivery.AttemptCount++;

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            delivery.ResponseBody = responseBody.Length > 2000 ? responseBody[..2000] : responseBody;

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = WebhookDeliveryStatus.Delivered;
                delivery.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Webhook {DeliveryId} delivered successfully in {Duration}ms",
                    delivery.Id, stopwatch.ElapsedMilliseconds);

                // Reset consecutive failures on subscription
                await ResetSubscriptionFailures(delivery.WebhookSubscriptionId, subscriptionRepository, cancellationToken);
            }
            else
            {
                delivery.ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                HandleDeliveryFailure(delivery);

                _logger.LogWarning(
                    "Webhook {DeliveryId} failed with status {StatusCode}",
                    delivery.Id, response.StatusCode);

                // Increment consecutive failures on subscription
                await IncrementSubscriptionFailures(delivery.WebhookSubscriptionId, subscriptionRepository, cancellationToken);
            }
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            delivery.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            delivery.AttemptCount++;
            delivery.ErrorMessage = ex.Message;
            HandleDeliveryFailure(delivery);

            _logger.LogError(ex, "Error delivering webhook {DeliveryId}", delivery.Id);

            // Increment consecutive failures on subscription
            await IncrementSubscriptionFailures(delivery.WebhookSubscriptionId, subscriptionRepository, cancellationToken);
        }

        await deliveryRepository.UpdateAsync(delivery, cancellationToken);
    }

    private void HandleDeliveryFailure(WebhookDelivery delivery)
    {
        if (delivery.AttemptCount < _maxRetries)
        {
            // Exponential backoff: 30s, 60s, 120s, 240s, 480s
            var delaySeconds = Math.Pow(2, delivery.AttemptCount - 1) * 30;
            delivery.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
            delivery.Status = WebhookDeliveryStatus.Retry;
        }
        else
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
        }
    }

    private async Task ResetSubscriptionFailures(
        int subscriptionId,
        IWebhookSubscriptionRepository subscriptionRepository,
        CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
            if (subscription != null && subscription.ConsecutiveFailures > 0)
            {
                subscription.ConsecutiveFailures = 0;
                subscription.UpdatedAt = DateTime.UtcNow;
                await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset subscription failures for {SubscriptionId}", subscriptionId);
        }
    }

    private async Task IncrementSubscriptionFailures(
        int subscriptionId,
        IWebhookSubscriptionRepository subscriptionRepository,
        CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);
            if (subscription != null)
            {
                subscription.ConsecutiveFailures++;
                subscription.LastFailureAt = DateTime.UtcNow;
                subscription.UpdatedAt = DateTime.UtcNow;

                // Auto-suspend after too many failures
                if (subscription.ConsecutiveFailures >= 10 && !subscription.IsSuspended)
                {
                    subscription.IsSuspended = true;
                    subscription.SuspendedAt = DateTime.UtcNow;
                    subscription.SuspensionReason = $"Auto-suspended after {subscription.ConsecutiveFailures} consecutive failures";

                    _logger.LogWarning(
                        "Webhook subscription {SubscriptionId} auto-suspended after {Failures} failures",
                        subscriptionId, subscription.ConsecutiveFailures);
                }

                await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment subscription failures for {SubscriptionId}", subscriptionId);
        }
    }
}
