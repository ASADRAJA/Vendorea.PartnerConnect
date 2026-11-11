using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Infrastructure.Security;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <inheritdoc />
public class IntegrationEventDeliveryService : IIntegrationEventDeliveryService
{
    /// <summary>The three headers the contract requires. All of them, every time.</summary>
    public const string TimestampHeader = "X-PC-Signature-Timestamp";
    public const string KeyIdHeader = "X-PC-Signature-KeyId";
    public const string SignatureHeader = "X-PC-Signature";

    private readonly HttpClient _http;
    private readonly IIntegrationEventRepository _events;
    private readonly IIntegrationSigningKeyProvider _keys;
    private readonly IntegrationDeliveryOptions _options;
    private readonly ILogger<IntegrationEventDeliveryService> _logger;

    public IntegrationEventDeliveryService(
        HttpClient http,
        IIntegrationEventRepository events,
        IIntegrationSigningKeyProvider keys,
        IOptions<IntegrationDeliveryOptions> options,
        ILogger<IntegrationEventDeliveryService> logger)
    {
        _http = http;
        _events = events;
        _keys = keys;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> DeliverAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var integrationEvent = await _events.GetAsync(eventId, cancellationToken);
        if (integrationEvent is null)
        {
            // Nothing to deliver and nothing to wait for. Reported as delivered so the outbox
            // stops retrying a message whose event no longer exists.
            _logger.LogWarning("Outbox asked to deliver event {EventId}, which does not exist.", eventId);
            return true;
        }

        if (integrationEvent.DeliveryAcknowledgedAt.HasValue)
        {
            // Already acknowledged. The outbox is at-least-once, so seeing a message twice is
            // normal; delivering twice would make the consumer dedupe work it need not.
            return true;
        }

        if (string.IsNullOrWhiteSpace(_options.EndpointUrl))
        {
            await RecordFailureAsync(
                integrationEvent, "No delivery endpoint is configured.", cancellationToken);
            return false;
        }

        var key = _keys.Primary;
        if (key is null)
        {
            // Refused rather than sent unsigned. An unsigned delivery is one the consumer must
            // reject, so sending it would turn a configuration error into a stream of 401s.
            await RecordFailureAsync(
                integrationEvent,
                "No primary signing key is configured; the delivery was not sent unsigned.",
                cancellationToken);
            return false;
        }

        // RFC 3339 UTC to the second, and the same string that is signed and sent. Formatting it
        // twice would risk the two differing by a tick and failing verification.
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ",
            System.Globalization.CultureInfo.InvariantCulture);

        // Signed over the stored bytes, exactly as they will be sent. Re-serialising the envelope
        // would change whitespace or key order and the consumer's recomputation would not match.
        var body = integrationEvent.EnvelopeJson;
        var signature = IntegrationSignature.Compute(key.Value.Secret, timestamp, body);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.EndpointUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            request.Headers.Add(TimestampHeader, timestamp);
            request.Headers.Add(KeyIdHeader, key.Value.KeyId);
            request.Headers.Add(SignatureHeader, signature);

            using var response = await _http.SendAsync(request, cancellationToken);

            integrationEvent.DeliveryAttempts++;
            integrationEvent.LastDeliveryAttemptAt = DateTime.UtcNow;
            integrationEvent.SigningKeyId = key.Value.KeyId;

            if (response.IsSuccessStatusCode)
            {
                // Transport only. The business outcome arrives separately on the processing-result
                // endpoint, and the contract is explicit that a 202 must never be read as
                // successful processing.
                integrationEvent.DeliveryAcknowledgedAt = DateTime.UtcNow;
                integrationEvent.LastDeliveryError = null;
                await _events.SaveAsync(cancellationToken);

                _logger.LogInformation(
                    "Delivered event {EventId} ({EventType}); awaiting a processing result.",
                    eventId, integrationEvent.EventType);

                return true;
            }

            var error = $"HTTP {(int)response.StatusCode}";
            integrationEvent.LastDeliveryError = error;
            await _events.SaveAsync(cancellationToken);

            // 4xx other than 408/429 will not improve on retry, but the decision is the outbox's:
            // it owns the retry budget and the dead-letter. Reporting the failure honestly and
            // letting it decide keeps one component in charge of that.
            _logger.LogWarning(
                "Delivery of event {EventId} was rejected: {Error}", eventId, error);

            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !cancellationToken.IsCancellationRequested)
        {
            await RecordFailureAsync(integrationEvent, ex.Message, cancellationToken);
            return false;
        }
    }

    private async Task RecordFailureAsync(
        Domain.Entities.Integration.IntegrationEvent integrationEvent, string error,
        CancellationToken cancellationToken)
    {
        integrationEvent.DeliveryAttempts++;
        integrationEvent.LastDeliveryAttemptAt = DateTime.UtcNow;
        integrationEvent.LastDeliveryError = error.Length > 2000 ? error[..2000] : error;
        await _events.SaveAsync(cancellationToken);

        _logger.LogWarning(
            "Delivery of event {EventId} failed: {Error}", integrationEvent.EventId, error);
    }
}
