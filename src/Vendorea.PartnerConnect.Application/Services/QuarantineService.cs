using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for managing quarantined documents.
/// </summary>
public class QuarantineService : IQuarantineService
{
    private readonly IQuarantinedDocumentRepository _repository;
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly ILogger<QuarantineService> _logger;

    public QuarantineService(
        IQuarantinedDocumentRepository repository,
        IPartnerDocumentRepository documentRepository,
        ILogger<QuarantineService> logger)
    {
        _repository = repository;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QuarantinedDocument> QuarantineDocumentAsync(
        PartnerDocument document,
        QuarantineReason reason,
        string? errorCode = null,
        string? errorMessage = null,
        string? errorDetails = null,
        CancellationToken cancellationToken = default)
    {
        // Check if already quarantined
        var existing = await _repository.GetByDocumentIdAsync(document.Id, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning(
                "Document {DocumentId} is already quarantined (QuarantineId: {QuarantineId})",
                document.Id, existing.Id);
            existing.RetryCount++;
            await _repository.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var quarantine = QuarantinedDocument.Create(
            document.Id,
            document.DealerPartnerConnectionId,
            document.State,
            reason,
            errorCode,
            errorMessage,
            errorDetails);

        await _repository.AddAsync(quarantine, cancellationToken);

        _logger.LogInformation(
            "Document {DocumentId} quarantined for reason: {Reason}",
            document.Id, reason);

        return quarantine;
    }

    /// <inheritdoc />
    public async Task<QuarantinedDocument?> GetByDocumentIdAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByDocumentIdAsync(documentId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuarantinedDocument>> GetByConnectionIdAsync(
        int connectionId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByConnectionIdAsync(connectionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuarantinedDocument>> GetUnresolvedAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetUnresolvedAsync(limit, cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkReviewedAsync(
        int quarantineId,
        string reviewedBy,
        CancellationToken cancellationToken = default)
    {
        var quarantine = await _repository.GetByIdAsync(quarantineId, cancellationToken);
        if (quarantine == null)
        {
            throw new InvalidOperationException($"Quarantine entry {quarantineId} not found");
        }

        quarantine.MarkReviewed(reviewedBy);
        await _repository.UpdateAsync(quarantine, cancellationToken);

        _logger.LogInformation(
            "Quarantine entry {QuarantineId} marked as reviewed by {ReviewedBy}",
            quarantineId, reviewedBy);
    }

    /// <inheritdoc />
    public async Task ResolveAsync(
        int quarantineId,
        QuarantineResolution resolution,
        string resolvedBy,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var quarantine = await _repository.GetByIdAsync(quarantineId, cancellationToken);
        if (quarantine == null)
        {
            throw new InvalidOperationException($"Quarantine entry {quarantineId} not found");
        }

        quarantine.Resolve(resolution, resolvedBy, notes);
        await _repository.UpdateAsync(quarantine, cancellationToken);

        _logger.LogInformation(
            "Quarantine entry {QuarantineId} resolved with resolution: {Resolution} by {ResolvedBy}",
            quarantineId, resolution, resolvedBy);
    }

    /// <inheritdoc />
    public async Task<bool> ReprocessAsync(
        int quarantineId,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        var quarantine = await _repository.GetByIdAsync(quarantineId, cancellationToken);
        if (quarantine == null)
        {
            throw new InvalidOperationException($"Quarantine entry {quarantineId} not found");
        }

        if (!quarantine.CanRetry)
        {
            _logger.LogWarning(
                "Quarantine entry {QuarantineId} has exceeded max retries ({RetryCount}/{MaxRetries})",
                quarantineId, quarantine.RetryCount, quarantine.MaxRetries);
            return false;
        }

        var document = await _documentRepository.GetByIdAsync(quarantine.PartnerDocumentId, cancellationToken);
        if (document == null)
        {
            throw new InvalidOperationException($"Document {quarantine.PartnerDocumentId} not found");
        }

        // Reset document state for reprocessing
        document.State = DocumentState.Received;
        document.ErrorDetails = null;
        document.LastErrorCode = null;
        document.LastStateChangeAt = DateTime.UtcNow;
        await _documentRepository.UpdateAsync(document, cancellationToken);

        // Update quarantine entry
        quarantine.RecordRetry();
        quarantine.Resolve(QuarantineResolution.Reprocessed, performedBy, "Reprocessing initiated");
        await _repository.UpdateAsync(quarantine, cancellationToken);

        _logger.LogInformation(
            "Document {DocumentId} queued for reprocessing from quarantine by {PerformedBy}",
            quarantine.PartnerDocumentId, performedBy);

        return true;
    }

    /// <inheritdoc />
    public async Task DiscardAsync(
        int quarantineId,
        string performedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var quarantine = await _repository.GetByIdAsync(quarantineId, cancellationToken);
        if (quarantine == null)
        {
            throw new InvalidOperationException($"Quarantine entry {quarantineId} not found");
        }

        var document = await _documentRepository.GetByIdAsync(quarantine.PartnerDocumentId, cancellationToken);
        if (document != null)
        {
            document.State = DocumentState.Cancelled;
            document.LastStateChangeAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(document, cancellationToken);
        }

        quarantine.Resolve(QuarantineResolution.Discarded, performedBy, reason);
        await _repository.UpdateAsync(quarantine, cancellationToken);

        _logger.LogInformation(
            "Quarantine entry {QuarantineId} discarded by {PerformedBy}. Reason: {Reason}",
            quarantineId, performedBy, reason ?? "No reason provided");
    }

    /// <inheritdoc />
    public async Task<QuarantineStatistics> GetStatisticsAsync(
        int? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetStatisticsAsync(connectionId, cancellationToken);
    }
}
