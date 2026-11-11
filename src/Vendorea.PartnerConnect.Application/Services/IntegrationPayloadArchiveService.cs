using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class IntegrationPayloadArchiveService : IIntegrationPayloadArchiveService
{
    private readonly IIntegrationPayloadArchiveRepository _archive;
    private readonly PayloadArchiveOptions _options;
    private readonly ILogger<IntegrationPayloadArchiveService> _logger;

    public IntegrationPayloadArchiveService(
        IIntegrationPayloadArchiveRepository archive,
        IOptions<PayloadArchiveOptions> options,
        ILogger<IntegrationPayloadArchiveService> logger)
    {
        _archive = archive;
        _options = options.Value;
        _logger = logger;
    }

    public async Task CaptureAsync(
        IntegrationConnection connection, IntegrationPayloadKind kind,
        string? externalReference, string rawPayload, string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrEmpty(rawPayload))
        {
            return;
        }

        try
        {
            if (rawPayload.Length > _options.MaxPayloadBytes)
            {
                // Noted, not stored. One pathological response should not fill the table, and
                // silence here would make the gap look like a capture that never happened.
                _logger.LogWarning(
                    "Skipped archiving a {Kind} payload of {Bytes} bytes for connection {ConnectionId}: over the cap.",
                    kind, rawPayload.Length, connection.Id);
                return;
            }

            // Redaction happens before anything else touches the payload, so there is no window in
            // which an unredacted copy exists anywhere but in memory.
            var redacted = PayloadRedactor.Redact(rawPayload);
            var hash = Hash(redacted.Content);

            if (!string.IsNullOrEmpty(externalReference))
            {
                var previous = await _archive.GetLatestHashAsync(
                    connection.Id, kind, externalReference, cancellationToken);

                if (previous == hash)
                {
                    // Polling re-reads an overlap window every cycle on purpose. Without this the
                    // archive would fill with identical copies of unchanged orders much faster
                    // than it filled with anything worth reading.
                    return;
                }
            }

            await _archive.AddAsync(new IntegrationPayloadArchive
            {
                Id = Guid.NewGuid(),
                IntegrationConnectionId = connection.Id,
                TenantId = connection.TenantId,
                PayloadKind = kind,
                ExternalReference = externalReference,
                CorrelationId = correlationId,
                ContentHash = hash,
                Content = redacted.Content,
                SizeBytes = Encoding.UTF8.GetByteCount(redacted.Content),
                RedactedPaths = redacted.RedactedPaths.Count == 0
                    ? null
                    : JsonSerializer.Serialize(redacted.RedactedPaths),
                CapturedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_options.Retention)
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Deliberately swallowed. This is a diagnostic aid observing an ingestion; failing
            // the ingestion because the observation failed would be the wrong trade every time.
            _logger.LogWarning(
                ex, "Could not archive a {Kind} payload for connection {ConnectionId}.",
                kind, connection.Id);
        }
    }

    private static string Hash(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
