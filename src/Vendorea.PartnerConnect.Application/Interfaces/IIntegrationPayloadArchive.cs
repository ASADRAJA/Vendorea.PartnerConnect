using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Interfaces;

public interface IIntegrationPayloadArchiveRepository
{
    /// <summary>The hash of the most recent capture for a reference, if there is one.</summary>
    Task<string?> GetLatestHashAsync(
        Guid connectionId, IntegrationPayloadKind kind, string externalReference,
        CancellationToken cancellationToken = default);

    Task AddAsync(IntegrationPayloadArchive archive, CancellationToken cancellationToken = default);

    /// <summary>Captures for one reference, newest first — the diagnostic read.</summary>
    Task<IReadOnlyList<IntegrationPayloadArchive>> GetForReferenceAsync(
        int tenantId, string externalReference, int limit, CancellationToken cancellationToken = default);

    /// <summary>Deletes expired rows in bounded batches, returning how many went.</summary>
    Task<int> PurgeExpiredAsync(
        DateTime asOfUtc, int batchSize, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

/// <summary>Captures provider payloads, redacted and expiring.</summary>
public interface IIntegrationPayloadArchiveService
{
    /// <summary>
    /// Redacts a payload and stores it, unless it is identical to the last one captured.
    /// </summary>
    /// <remarks>
    /// Never throws. A failure to archive must not fail the ingestion it was observing — the
    /// archive is a diagnostic aid, and losing a diagnostic aid is not worth losing an order for.
    /// </remarks>
    Task CaptureAsync(
        IntegrationConnection connection, IntegrationPayloadKind kind,
        string? externalReference, string rawPayload, string? correlationId = null,
        CancellationToken cancellationToken = default);
}
