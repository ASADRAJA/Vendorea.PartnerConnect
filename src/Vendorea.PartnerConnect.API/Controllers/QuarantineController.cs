using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers;

/// <summary>
/// API endpoints for managing quarantined documents.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class QuarantineController : ControllerBase
{
    private readonly IQuarantineService _quarantineService;
    private readonly IDocumentStateService _documentStateService;
    private readonly ILogger<QuarantineController> _logger;

    public QuarantineController(
        IQuarantineService quarantineService,
        IDocumentStateService documentStateService,
        ILogger<QuarantineController> logger)
    {
        _quarantineService = quarantineService;
        _documentStateService = documentStateService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all unresolved quarantined documents.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<QuarantinedDocumentDto>>> GetUnresolved(
        [FromQuery] int? limit = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await _quarantineService.GetUnresolvedAsync(limit, cancellationToken);
        return Ok(items.Select(MapToDto));
    }

    /// <summary>
    /// Gets quarantined documents for a specific connection.
    /// </summary>
    [HttpGet("partner/{tradingPartnerId}")]
    public async Task<ActionResult<IEnumerable<QuarantinedDocumentDto>>> GetByTradingPartner(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        var items = await _quarantineService.GetByTradingPartnerAsync(tradingPartnerId, cancellationToken);
        return Ok(items.Select(MapToDto));
    }

    /// <summary>
    /// Gets a specific quarantine entry by document ID.
    /// </summary>
    [HttpGet("document/{documentId}")]
    public async Task<ActionResult<QuarantinedDocumentDto>> GetByDocumentId(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        var item = await _quarantineService.GetByDocumentIdAsync(documentId, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }
        return Ok(MapToDto(item));
    }

    /// <summary>
    /// Gets quarantine statistics.
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<QuarantineStatisticsDto>> GetStatistics(
        [FromQuery] int? tradingPartnerId = null,
        CancellationToken cancellationToken = default)
    {
        var stats = await _quarantineService.GetStatisticsAsync(tradingPartnerId, cancellationToken);
        return Ok(new QuarantineStatisticsDto
        {
            TotalQuarantined = stats.TotalQuarantined,
            UnresolvedCount = stats.UnresolvedCount,
            ResolvedCount = stats.ResolvedCount,
            ReprocessedCount = stats.ReprocessedCount,
            DiscardedCount = stats.DiscardedCount,
            ByReason = stats.ByReason.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value),
            OldestUnresolved = stats.OldestUnresolved
        });
    }

    /// <summary>
    /// Marks a quarantine entry as reviewed.
    /// </summary>
    [HttpPost("{quarantineId}/review")]
    public async Task<ActionResult> MarkReviewed(
        int quarantineId,
        [FromBody] ReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _quarantineService.MarkReviewedAsync(quarantineId, request.ReviewedBy, cancellationToken);
            return Ok(new { message = "Marked as reviewed" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Attempts to reprocess a quarantined document.
    /// </summary>
    [HttpPost("{quarantineId}/reprocess")]
    public async Task<ActionResult> Reprocess(
        int quarantineId,
        [FromBody] ReprocessRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _quarantineService.ReprocessAsync(
                quarantineId,
                request.PerformedBy,
                cancellationToken);

            if (success)
            {
                return Ok(new { message = "Document queued for reprocessing" });
            }
            else
            {
                return BadRequest(new { error = "Cannot reprocess - max retries exceeded" });
            }
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Discards a quarantined document.
    /// </summary>
    [HttpPost("{quarantineId}/discard")]
    public async Task<ActionResult> Discard(
        int quarantineId,
        [FromBody] DiscardRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _quarantineService.DiscardAsync(
                quarantineId,
                request.PerformedBy,
                request.Reason,
                cancellationToken);

            return Ok(new { message = "Document discarded" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Resolves a quarantine entry with a custom resolution.
    /// </summary>
    [HttpPost("{quarantineId}/resolve")]
    public async Task<ActionResult> Resolve(
        int quarantineId,
        [FromBody] ResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Enum.TryParse<QuarantineResolution>(request.Resolution, true, out var resolution))
            {
                return BadRequest(new { error = $"Invalid resolution: {request.Resolution}" });
            }

            await _quarantineService.ResolveAsync(
                quarantineId,
                resolution,
                request.ResolvedBy,
                request.Notes,
                cancellationToken);

            return Ok(new { message = "Quarantine resolved" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets the state transition history for a document.
    /// </summary>
    [HttpGet("document/{documentId}/history")]
    public async Task<ActionResult<IEnumerable<StateHistoryDto>>> GetDocumentHistory(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        var history = await _documentStateService.GetHistoryAsync(documentId, cancellationToken);
        return Ok(history.Select(h => new StateHistoryDto
        {
            Id = h.Id,
            FromState = h.FromState.ToString(),
            ToState = h.ToState.ToString(),
            Trigger = h.Trigger.ToString(),
            Reason = h.Reason,
            PerformedBy = h.PerformedBy,
            OccurredAt = h.OccurredAt
        }));
    }

    private static QuarantinedDocumentDto MapToDto(QuarantinedDocument q)
    {
        return new QuarantinedDocumentDto
        {
            Id = q.Id,
            DocumentId = q.PartnerDocumentId,
            ConnectionId = q.TradingPartnerId,
            QuarantinedFromState = q.QuarantinedFromState.ToString(),
            Reason = q.Reason.ToString(),
            ErrorCode = q.ErrorCode,
            ErrorMessage = q.ErrorMessage,
            RetryCount = q.RetryCount,
            MaxRetries = q.MaxRetries,
            CanRetry = q.CanRetry,
            QuarantinedAt = q.QuarantinedAt,
            ReviewedAt = q.ReviewedAt,
            ReviewedBy = q.ReviewedBy,
            Resolution = q.Resolution?.ToString(),
            ResolvedAt = q.ResolvedAt,
            ResolvedBy = q.ResolvedBy,
            FileName = q.PartnerDocument?.FileName,
            DocumentType = q.PartnerDocument?.DocumentType.ToString()
        };
    }
}

#region DTOs

public class QuarantinedDocumentDto
{
    public int Id { get; init; }
    public int DocumentId { get; init; }
    public int ConnectionId { get; init; }
    public string QuarantinedFromState { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetries { get; init; }
    public bool CanRetry { get; init; }
    public DateTime QuarantinedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewedBy { get; init; }
    public string? Resolution { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
    public string? FileName { get; init; }
    public string? DocumentType { get; init; }
}

public class QuarantineStatisticsDto
{
    public int TotalQuarantined { get; init; }
    public int UnresolvedCount { get; init; }
    public int ResolvedCount { get; init; }
    public int ReprocessedCount { get; init; }
    public int DiscardedCount { get; init; }
    public Dictionary<string, int> ByReason { get; init; } = new();
    public DateTime? OldestUnresolved { get; init; }
}

public class StateHistoryDto
{
    public int Id { get; init; }
    public string FromState { get; init; } = string.Empty;
    public string ToState { get; init; } = string.Empty;
    public string Trigger { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? PerformedBy { get; init; }
    public DateTime OccurredAt { get; init; }
}

public class ReviewRequest
{
    public string ReviewedBy { get; init; } = string.Empty;
}

public class ReprocessRequest
{
    public string PerformedBy { get; init; } = string.Empty;
}

public class DiscardRequest
{
    public string PerformedBy { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public class ResolveRequest
{
    public string Resolution { get; init; } = string.Empty;
    public string ResolvedBy { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

#endregion
