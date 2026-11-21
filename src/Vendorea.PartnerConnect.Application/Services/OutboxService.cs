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
    public Guid EnqueueInCurrentTransaction<T>(
        string messageType,
        T payload,
        string? destination = null,
        string? correlationId = null,
        int priority = 0) where T : class
    {
        var message = new OutboxMessage
        {
            MessageType = messageType,
            Payload = JsonSerializer.Serialize(payload, _jsonOptions),
            Destination = destination,
            CorrelationId = correlationId,
            Priority = priority
        };

        _repository.Stage(message);

        _logger.LogDebug(
            "Staged outbox message {MessageId} of type {MessageType} in the caller's transaction",
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
        catch (PermanentDeliveryException pex)
        {
            // Permanent (4xx validation-style) failure: do not churn through retries.
            // Mark terminally Failed; it remains available for manual replay via the admin surface.
            message.LastError = pex.Message;
            message.Status = OutboxMessageStatus.Failed;
            message.ProcessedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(message, cancellationToken);

            _logger.LogWarning(
                "Outbox message {MessageId} permanently failed (no retry): {Error}",
                message.Id, pex.Message);
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
/// Thrown by a message processor when delivery failed permanently (a non-retryable client error,
/// e.g. an HTTP 4xx other than 408/429). The outbox marks the message Failed without retrying.
/// </summary>
public class PermanentDeliveryException : Exception
{
    public PermanentDeliveryException(string message) : base(message) { }
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
    private readonly IIntegrationEventDeliveryService? _integrationEvents;
    private readonly Func<ISprOutboundOrderService?>? _outboundOrders;
    private readonly SprSimulationOptions _simulation;
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
        Microsoft.Extensions.Options.IOptions<SprSimulationOptions> simulationOptions,
        ILogger<DefaultOutboxMessageProcessor> logger,
        IIntegrationEventDeliveryService? integrationEvents = null,
        Func<ISprOutboundOrderService?>? outboundOrders = null)
    {
        _httpClientFactory = httpClientFactory;
        _merchant360Client = merchant360Client;
        _integrationEvents = integrationEvents;
        _outboundOrders = outboundOrders;
        _simulation = simulationOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// In simulation capture mode, Merchant360 callbacks are not sent over HTTP — the payload stays
    /// on the (already-persisted) outbox message for inspection. Returns true if the delivery was
    /// short-circuited.
    /// </summary>
    private bool CaptureInsteadOfDeliver(OutboxMessage message)
    {
        if (!_simulation.CaptureCallbacks)
            return false;

        _logger.LogInformation(
            "[SPR-SIM] Capture mode: NOT delivering {MessageType} outbox message {MessageId} to Merchant360 (payload retained for inspection)",
            message.MessageType, message.Id);
        return true;
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

            case SalesOrderEventPublisher.OutboxMessageType:
                await DeliverIntegrationEventAsync(message, cancellationToken);
                break;

            case OrderAutoProcessingMessageTypes.Transmit:
                await TransmitAutomaticOrderAsync(message, cancellationToken);
                break;

            default:
                _logger.LogWarning(
                    "Unknown message type {MessageType} for outbox message {MessageId}",
                    message.MessageType, message.Id);
                break;
        }
    }

    /// <summary>
    /// Delivers a published integration event, signed, to Merchant360.
    /// </summary>
    /// <remarks>
    /// The message carries only the event id; the envelope lives on the event row, because the
    /// signature is computed over those exact stored bytes. Throwing on failure is deliberate —
    /// the outbox owns retry and dead-lettering, and a delivery that reported success it did not
    /// have would retire the message with the event undelivered.
    /// </remarks>
    private async Task DeliverIntegrationEventAsync(
        OutboxMessage message, CancellationToken cancellationToken)
    {
        if (CaptureInsteadOfDeliver(message)) return;

        if (_integrationEvents is null)
        {
            throw new InvalidOperationException(
                "No integration-event delivery service is registered; the event cannot be delivered.");
        }

        var payload = JsonSerializer.Deserialize<IntegrationEventDeliveryMessage>(
            message.Payload, _deserializeOptions);

        if (payload is null || payload.EventId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Outbox message {message.Id} carries no integration event id.");
        }

        if (!await _integrationEvents.DeliverAsync(payload.EventId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Delivery of integration event {payload.EventId} failed; the outbox will retry.");
        }
    }

    private async Task DeliverMerchant360OrderStatusAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (CaptureInsteadOfDeliver(message)) return;

        var payload = JsonSerializer.Deserialize<Merchant360OrderStatusOutboxPayload>(message.Payload, _deserializeOptions)
            ?? throw new InvalidOperationException("Invalid Merchant360 order status payload");

        var result = await _merchant360Client.PushOrderStatusUpdateAsync(
            payload.MerchantId, payload.Request, cancellationToken);

        // Non-success: permanent (4xx) -> terminal Failed (no retry); transient -> retry with backoff.
        if (result is { Success: false })
        {
            ThrowDeliveryFailure("Merchant360 order status push", result.HttpStatusCode, result.ErrorMessage);
        }
    }

    private async Task DeliverMerchant360ShipmentAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (CaptureInsteadOfDeliver(message)) return;

        var payload = JsonSerializer.Deserialize<Merchant360ShipmentOutboxPayload>(message.Payload, _deserializeOptions)
            ?? throw new InvalidOperationException("Invalid Merchant360 shipment payload");

        var result = await _merchant360Client.PushShipmentUpdateAsync(
            payload.MerchantId, payload.Request, cancellationToken);

        if (result is { Success: false })
        {
            ThrowDeliveryFailure("Merchant360 shipment push", result.HttpStatusCode, result.ErrorMessage);
        }
    }

    private async Task DeliverMerchant360InvoiceAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (CaptureInsteadOfDeliver(message)) return;

        var payload = JsonSerializer.Deserialize<Merchant360InvoiceOutboxPayload>(message.Payload, _deserializeOptions)
            ?? throw new InvalidOperationException("Invalid Merchant360 invoice payload");

        var result = await _merchant360Client.PushInvoiceUpdateAsync(
            payload.MerchantId, payload.Request, cancellationToken);

        if (result is { Success: false })
        {
            ThrowDeliveryFailure("Merchant360 invoice push", result.HttpStatusCode, result.ErrorMessage);
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
            ThrowDeliveryFailure("Merchant360 inventory snapshot notification", result.HttpStatusCode, result.ErrorMessage);
        }
    }

    /// <summary>
    /// Throws a permanent (no-retry) or transient (retry) delivery failure based on the HTTP status.
    /// 4xx (except 408 Request Timeout / 429 Too Many Requests) is treated as permanent.
    /// </summary>
    private static void ThrowDeliveryFailure(string context, int? statusCode, string? error)
    {
        var message = $"{context} failed (HTTP {statusCode?.ToString() ?? "n/a"}): {error ?? "unsuccessful response"}";
        if (IsPermanentFailure(statusCode))
        {
            throw new PermanentDeliveryException(message);
        }
        throw new InvalidOperationException(message);
    }

    private static bool IsPermanentFailure(int? statusCode) =>
        statusCode is >= 400 and < 500 and not 408 and not 429;

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

    /// <summary>
    /// Dispatches an automatically-processed order to the supplier.
    /// </summary>
    /// <remarks>
    /// Whether this throws is the whole design. The outbox retries a message whose handler throws
    /// and retires one that returns, so the two kinds of failure have to be told apart:
    ///
    /// A bad order — generation or strict XSD validation failed, the order vanished, or it has
    /// moved to a status that cannot be transmitted — will fail identically on every retry. It is
    /// logged at Error and the message is retired, because burning the retry budget changes
    /// nothing and buries the one line that says what is wrong. The order keeps its Acknowledged
    /// status and an admin can act on it.
    ///
    /// An unreachable supplier is transient and throws, so the outbox backs off and tries again.
    /// This is the case that matters: it is what stops a temporary SFTP outage from silently
    /// dropping orders, which is the risk Automatic introduces.
    /// </remarks>
    private async Task TransmitAutomaticOrderAsync(
        OutboxMessage message, CancellationToken cancellationToken)
    {
        if (CaptureInsteadOfDeliver(message)) return;

        // Resolved here rather than injected, because the outbound order service needs the SPR XML
        // document service, which needs the outbox - so taking it in the constructor is a circular
        // dependency that fails at startup, not at use. Deferring the resolution to the one handler
        // that needs it breaks the cycle without the processor knowing about the graph.
        var outboundOrders = _outboundOrders?.Invoke();

        if (outboundOrders is null)
        {
            // Not registered in this host. Throwing would retry forever against a process that
            // can never serve it, so this is retired with a warning instead.
            _logger.LogWarning(
                "No outbound order service is registered; cannot transmit automatic order for outbox message {MessageId}",
                message.Id);
            return;
        }

        var payload = JsonSerializer.Deserialize<OrderAutoTransmitPayload>(
            message.Payload, _deserializeOptions);

        if (payload is null || payload.OrderId == 0)
        {
            _logger.LogError(
                "Outbox message {MessageId} carries no order id; cannot transmit", message.Id);
            return;
        }

        // Never forced. Automatic dispatch must obey the same duplicate guard as the button; a
        // retried outbox message on an order SPR has already acknowledged must not send it again.
        var result = await outboundOrders.TransmitOrderAsync(
            payload.OrderId, force: false, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Transmitted order {OrderId} automatically (document {DocumentId})",
                payload.OrderId, result.DocumentId);
            return;
        }

        if (result.NotFound || result.InvalidState || result.ValidationFailed)
        {
            _logger.LogError(
                "Automatic transmission of order {OrderId} failed permanently and will not be retried: {Error} {Errors}",
                payload.OrderId, result.ErrorMessage, string.Join("; ", result.Errors));
            return;
        }

        throw new InvalidOperationException(
            $"Automatic transmission of order {payload.OrderId} failed: {result.ErrorMessage}");
    }
}
