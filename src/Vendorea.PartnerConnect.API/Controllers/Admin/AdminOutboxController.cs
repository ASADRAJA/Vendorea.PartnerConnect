using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin/ops surface for the outbound delivery outbox (including PC → Merchant360 callbacks).
/// Lets an operator inspect and manually retry/replay dead-lettered (Failed) messages.
/// </summary>
[ApiController]
[Route("api/admin/outbox")]
[AllowAnonymous] // TODO: Restore [Authorize(Policy = "RequireSystemAdmin")] in production
public class AdminOutboxController : ControllerBase
{
    private readonly IOutboxService _outboxService;
    private readonly ILogger<AdminOutboxController> _logger;

    public AdminOutboxController(
        IOutboxService outboxService,
        ILogger<AdminOutboxController> logger)
    {
        _outboxService = outboxService;
        _logger = logger;
    }

    /// <summary>
    /// Outbox delivery statistics (pending / processing / retry / failed / delivered-24h).
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        return Ok(await _outboxService.GetStatisticsAsync(cancellationToken));
    }

    /// <summary>
    /// Lists dead-lettered (Failed) messages, newest first.
    /// </summary>
    [HttpGet("failed")]
    public async Task<IActionResult> GetFailed(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var messages = await _outboxService.GetFailedMessagesAsync(
            Math.Max(0, skip), Math.Clamp(take, 1, 200), cancellationToken);

        var items = messages.Select(m => new OutboxMessageDto
        {
            Id = m.Id,
            MessageType = m.MessageType,
            CorrelationId = m.CorrelationId,
            RetryCount = m.RetryCount,
            MaxRetries = m.MaxRetries,
            LastError = m.LastError,
            CreatedAt = m.CreatedAt,
            ProcessedAt = m.ProcessedAt,
            RelatedEntityType = m.RelatedEntityType,
            RelatedEntityId = m.RelatedEntityId
        }).ToList();

        return Ok(items);
    }

    /// <summary>
    /// Manually retries (replays) a single Failed/Cancelled message.
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var requeued = await _outboxService.RequeueAsync(id, cancellationToken);
        if (!requeued)
        {
            return NotFound(new { error = "Message not found or not in a replayable (Failed/Cancelled) state" });
        }

        _logger.LogInformation("Operator requeued outbox message {MessageId}", id);
        return Ok(new { id, status = "Pending" });
    }

    /// <summary>
    /// Manually retries (replays) all currently Failed messages (bounded).
    /// </summary>
    [HttpPost("retry-failed")]
    public async Task<IActionResult> RetryAllFailed(
        [FromQuery] int max = 500,
        CancellationToken cancellationToken = default)
    {
        var requeued = await _outboxService.RequeueAllFailedAsync(Math.Clamp(max, 1, 1000), cancellationToken);
        _logger.LogInformation("Operator requeued {Count} failed outbox messages", requeued);
        return Ok(new { requeued });
    }
}

/// <summary>
/// Admin view of an outbox message (excludes the raw payload).
/// </summary>
public class OutboxMessageDto
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
}
