using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers;

/// <summary>
/// Controller for accessing audit logs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        IAuditLogRepository auditLogRepository,
        ILogger<AuditController> logger)
    {
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// Searches audit logs with filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? userId = null,
        [FromQuery] int? dealerId = null,
        [FromQuery] AuditAction? action = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var criteria = new AuditLogSearchCriteria
        {
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            DealerId = dealerId,
            Action = action,
            StartDate = startDate,
            EndDate = endDate,
            CorrelationId = correlationId,
            Page = page,
            PageSize = Math.Min(pageSize, 100)
        };

        var result = await _auditLogRepository.SearchAsync(criteria, cancellationToken);

        return Ok(new
        {
            result.Items,
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.TotalPages
        });
    }

    /// <summary>
    /// Gets audit logs for a specific entity.
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId}")]
    public async Task<IActionResult> GetByEntity(
        string entityType,
        string entityId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogRepository.GetByEntityAsync(
            entityType,
            entityId,
            Math.Min(limit, 100),
            cancellationToken);

        return Ok(logs);
    }

    /// <summary>
    /// Gets audit logs for a specific dealer.
    /// </summary>
    [HttpGet("dealer/{dealerId:int}")]
    public async Task<IActionResult> GetByDealer(
        int dealerId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogRepository.GetByDealerAsync(
            dealerId,
            Math.Min(limit, 100),
            cancellationToken);

        return Ok(logs);
    }

    /// <summary>
    /// Gets audit logs for a specific user.
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUser(
        string userId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogRepository.GetByUserAsync(
            userId,
            Math.Min(limit, 100),
            cancellationToken);

        return Ok(logs);
    }

    /// <summary>
    /// Gets audit log statistics.
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var statistics = await _auditLogRepository.GetStatisticsAsync(
            startDate,
            endDate,
            cancellationToken);

        return Ok(statistics);
    }

    /// <summary>
    /// Cleans up old audit logs.
    /// </summary>
    [HttpDelete("cleanup")]
    public async Task<IActionResult> Cleanup(
        [FromQuery] int daysOld = 90,
        CancellationToken cancellationToken = default)
    {
        if (daysOld < 30)
        {
            return BadRequest("Minimum retention period is 30 days");
        }

        var olderThan = TimeSpan.FromDays(daysOld);
        var count = await _auditLogRepository.CleanupOldLogsAsync(olderThan, cancellationToken);

        _logger.LogInformation("Cleaned up {Count} audit logs older than {Days} days", count, daysOld);

        return Ok(new { deletedCount = count });
    }
}
