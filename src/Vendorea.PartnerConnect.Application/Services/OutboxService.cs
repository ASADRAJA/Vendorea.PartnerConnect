using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for managing outbox messages.
/// </summary>
public class OutboxService : IOutboxService
{
    private readonly IOutboxRepository _repository;
    private readonly IOutboxMessageProcessor _messageProcessor;
    private readonly ILogger<OutboxService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public OutboxService(
        IOutboxRepository repository,
        IOutboxMessageProcessor messageProcessor,
        ILogger<OutboxService> logger)
    {
        _repository = repository;
        _messageProcessor = messageProcessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> EnqueueAsync<T>(
        string messageType,
        T payload,
        string? destination = null,
        string? correlationId = null,
        int priority = 0,
        CancellationToken cancellationToken = default) where T : class
    {
        var message = new OutboxMessage
        {
            MessageType = messageType,
            Payload = JsonSerializer.Serialize(payload, _jsonOptions),
            Destination = destination,
            CorrelationId = correlationId,
            Priority = priority
        };

        await _repository.AddAsync(message, cancellationToken);

        _logger.LogDebug(
            "Enqueued outbox message {MessageId} of type {MessageType}",
            message.Id, messageType);

        return message.Id;
    }

    /// <inheritdoc />
    public async Task<Guid> EnqueueWebhookAsync(
        string webhookUrl,
        object payload,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        return await EnqueueAsync(
            "WebhookDelivery",
            payload,
            destination: webhookUrl,
            correlationId: correlationId,
            priority: 5,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid> EnqueueDocumentStateChangeAsync(
        int documentId,
        string previousState,
        string newState,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new DocumentStateChangePayload
        {
            DocumentId = documentId,
            PreviousState = previousState,
            NewState = newState,
            OccurredAt = DateTime.UtcNow
        };

        var message = new OutboxMessage
        {
            MessageType = "DocumentStateChanged",
            Payload = JsonSerializer.Serialize(payload, _jsonOptions),
            CorrelationId = correlationId,
            RelatedEntityId = documentId,
            RelatedEntityType = "PartnerDocument",
            Priority = 10
        };

        await _repository.AddAsync(message, cancellationToken);

        _logger.LogDebug(
            "Enqueued document state change message {MessageId} for document {DocumentId}",
            message.Id, documentId);

        return message.Id;
    }

    /// <inheritdoc />
    public async Task<int> ProcessPendingAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        var messages = await _repository.GetPendingAsync(batchSize, cancellationToken);
        if (messages.Count == 0)
        {
            return 0;
        }

        _logger.LogDebug("Processing {Count} pending outbox messages", messages.Count);

        var processed = 0;
        foreach (var message in messages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessMessageAsync(message, cancellationToken);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing outbox message {MessageId}: {Error}",
                    message.Id, ex.Message);
            }
        }

        return processed;
    }

    /// <inheritdoc />
    public async Task<int> ProcessRetriesAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        var messages = await _repository.GetRetryDueAsync(batchSize, cancellationToken);
        if (messages.Count == 0)
        {
            return 0;
        }

        _logger.LogDebug("Processing {Count} retry outbox messages", messages.Count);

        var processed = 0;
        foreach (var message in messages)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessMessageAsync(message, cancellationToken);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing retry outbox message {MessageId}: {Error}",
                    message.Id, ex.Message);
            }
        }

        return processed;
    }

    private async Task ProcessMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        message.MarkProcessing();
        await _repository.UpdateAsync(message, cancellationToken);

        try
        {
            await _messageProcessor.ProcessAsync(message, cancellationToken);
            message.MarkDelivered();
            await _repository.UpdateAsync(message, cancellationToken);

            _logger.LogDebug(
                "Successfully delivered outbox message {MessageId}",
                message.Id);
        }
        catch (Exception ex)
        {
            message.LastError = ex.Message;
            message.ScheduleRetry();
            await _repository.UpdateAsync(message, cancellationToken);

            if (message.Status == OutboxMessageStatus.Failed)
            {
                _logger.LogWarning(
                    "Outbox message {MessageId} failed after {RetryCount} retries: {Error}",
                    message.Id, message.RetryCount, ex.Message);
            }
            else
            {
                _logger.LogDebug(
                    "Outbox message {MessageId} scheduled for retry {RetryCount} at {NextRetryAt}",
                    message.Id, message.RetryCount, message.NextRetryAt);
            }
        }
    }

    /// <inheritdoc />
    public async Task<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetStatisticsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> GetFailedMessagesAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByStatusAsync(OutboxMessageStatus.Failed, skip, take, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await _repository.GetByIdAsync(messageId, cancellationToken);
        if (message == null)
        {
            return false;
        }

        // Only dead-lettered (Failed) or Cancelled messages can be manually replayed.
        if (message.Status is not (OutboxMessageStatus.Failed or OutboxMessageStatus.Cancelled))
        {
            return false;
        }

        message.Requeue();
        await _repository.UpdateAsync(message, cancellationToken);

        _logger.LogInformation(
            "Requeued outbox message {MessageId} ({MessageType}) for delivery",
            message.Id, message.MessageType);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> RequeueAllFailedAsync(int maxToRequeue = 500, CancellationToken cancellationToken = default)
    {
        var failed = await _repository.GetByStatusAsync(OutboxMessageStatus.Failed, 0, maxToRequeue, cancellationToken);
        if (failed.Count == 0)
        {
            return 0;
        }

        foreach (var message in failed)
        {
            message.Requeue();
        }
        await _repository.UpdateRangeAsync(failed, cancellationToken);

        _logger.LogInformation("Requeued {Count} failed outbox messages for delivery", failed.Count);
        return failed.Count;
    }

    /// <inheritdoc />
    public async Task<int> CleanupAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.CleanupDeliveredAsync(olderThan, cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} delivered outbox messages", deleted);
        }

        return deleted;
    }
}

/// <summary>
/// Payload for document state change messages.
/// </summary>
public record DocumentStateChangePayload
{
    public int DocumentId { get; init; }
    public string PreviousState { get; init; } = string.Empty;
    public string NewState { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Interface for processing outbox messages.
/// </summary>
public interface IOutboxMessageProcessor
{
    /// <summary>
    /// Processes an outbox message.
    /// </summary>
    Task ProcessAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of outbox message processor.
/// </summary>
public class DefaultOutboxMessageProcessor : IOutboxMessageProcessor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMerchant360Client _merchant360Client;
    private readonly ILogger<DefaultOutboxMessageProcessor> _logger;

    // Deserialization mirror of OutboxService's camelCase serialization.
    private static readonly JsonSerializerOptions _deserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public DefaultOutboxMessageProcessor(
        IHttpClientFactory httpClientFactory,
        IMerchant360Client merchant360Client,
        ILogger<DefaultOutboxMessageProcessor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _merchant360Client = merchant360Client;
        _logger = logger;
    }

    public async Task ProcessAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        switch (message.MessageType)
        {
            case "WebhookDelivery":
                await DeliverWebhookAsync(message, cancellationToken);
                break;

            case "DocumentStateChanged":
                // Document state changes can trigger webhooks or other actions
                await HandleDocumentStateChangeAsync(message, cancellationToken);
                break;

            case Merchant360OutboxMessageTypes.OrderStatus:
                await DeliverMerchant360OrderStatusAsync(message, cancellationToken);
                break;

            case Merchant360OutboxMessageTypes.Shipment:
                await DeliverMerchant360ShipmentAsync(message, cancellationToken);
                break;

            case Merchant360OutboxMessageTypes.Invoice:
                await DeliverMerchant360InvoiceAsync(message, cancellationToken);
                break;

            case Merchant360OutboxMessageTypes.InventorySnapshot:
                await DeliverMerchant360InventorySnapshotAsync(message, cancellationToken);
                break;

            default:
                _logger.LogWarning(
                    "Unknown message type {MessageType} for outbox message {MessageId}",
                    message.MessageType, message.Id);
                break;
        }
    }

    private async Task DeliverMerchant360OrderStatusAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<Merchant360OrderStatusOutboxPayload>(message.Payload, _deserializeOptions)
            ?? throw new InvalidOperationException("Invalid Merchant360 order status payload");

        var result = await _merchant360Client.PushOrderStatusUpdateAsync(
            payload.MerchantId, payload.Request, cancellationToken);

        // Throw on a non-success result so the outbox schedules a retry with backoff.
        if (result is { Success: false })
        {
            throw new InvalidOperationException(
                $"Merchant360 order status push failed: {result.ErrorMessage ?? "unsuccessful response"}");
        }
    }

    private async Task DeliverMerchant360ShipmentAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<Merchant360ShipmentOutboxPayload>(message.Payload, _deserializeOptions)
            ?? throw new InvalidOperationException("Invalid Merchant360 shipment payload");

        var result = await _merchant360Client.PushShipmentUpdateAsync(
            payload.MerchantId, payload.Request, cancellationToken);

        if (result is { Success: false })
        {
            throw new InvalidOperationException(
                $"Merchant360 shipment push failed: {result.ErrorMessage ?? "unsuccessful response"}");
        }
    }

    private async Task DeliverMerchant360InvoiceAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<Merchant360InvoiceOutboxPayload>(message.Payload, _deserializeOptions)
            ?? throw new InvalidOperationException("Invalid Merchant360 invoice payload");

        var result = await _merchant360Client.PushInvoiceUpdateAsync(
            payload.MerchantId, payload.Request, cancellationToken);

        if (result is { Success: false })
        {
            throw new InvalidOperationException(
                $"Merchant360 invoice push failed: {result.ErrorMessage ?? "unsuccessful response"}");
        }
    }

    private async Task DeliverMerchant360InventorySnapshotAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<Merchant360InventorySnapshotOutboxPayload>(message.Payload, _deserializeOptions)
            ?? throw new InvalidOperationException("Invalid Merchant360 inventory snapshot payload");

        var result = await _merchant360Client.PushInventorySnapshotNotificationAsync(
            payload.MerchantId, payload.Request, cancellationToken);

        if (result is { Success: false })
        {
            throw new InvalidOperationException(
                $"Merchant360 inventory snapshot notification failed: {result.ErrorMessage ?? "unsuccessful response"}");
        }
    }

    private async Task DeliverWebhookAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message.Destination))
        {
            throw new InvalidOperationException("Webhook message missing destination URL");
        }

        var client = _httpClientFactory.CreateClient("Webhook");
        var content = new StringContent(
            message.Payload,
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(message.Destination, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private Task HandleDocumentStateChangeAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        // This could trigger webhook subscriptions, send notifications, etc.
        // For now, just log the event
        _logger.LogDebug(
            "Document state change processed for message {MessageId}",
            message.Id);

        return Task.CompletedTask;
    }
}
