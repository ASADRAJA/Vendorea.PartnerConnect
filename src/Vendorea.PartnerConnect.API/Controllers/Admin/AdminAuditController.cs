using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for audit log search and management.
/// </summary>
[ApiController]
[Route("api/admin/audit")]
[Authorize(Policy = "RequireSystemAdmin")]
public class AdminAuditController : ControllerBase
{
    private readonly IAuditLogRepository _auditRepository;
    private readonly ILogger<AdminAuditController> _logger;

    public AdminAuditController(
        IAuditLogRepository auditRepository,
        ILogger<AdminAuditController> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>
    /// Searches audit logs with filters.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchAuditLogs(
        [FromQuery] AuditSearchRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin searching audit logs with filters: {@Request}", request);

        IReadOnlyList<AuditLog> logs;

        // Get logs based on primary filter
        if (request.DealerId.HasValue)
        {
            logs = await _auditRepository.GetByDealerAsync(
                request.DealerId.Value,
                request.Limit,
                cancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.UserId))
        {
            logs = await _auditRepository.GetByUserAsync(
                request.UserId,
                request.Limit,
                cancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.EntityType) && !string.IsNullOrEmpty(request.EntityId))
        {
            logs = await _auditRepository.GetByEntityAsync(
                request.EntityType,
                request.EntityId,
                request.Limit,
                cancellationToken);
        }
        else
        {
            // Default to recent logs - get by a high limit
            logs = await _auditRepository.GetByDealerAsync(0, request.Limit, cancellationToken);
        }

        // Apply additional filters
        var filtered = logs.AsEnumerable();

        if (!string.IsNullOrEmpty(request.Action))
        {
            if (Enum.TryParse<AuditAction>(request.Action, true, out var action))
            {
                filtered = filtered.Where(l => l.Action == action);
            }
        }

        if (request.StartDate.HasValue)
        {
            filtered = filtered.Where(l => l.Timestamp >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            filtered = filtered.Where(l => l.Timestamp <= request.EndDate.Value);
        }

        if (!string.IsNullOrEmpty(request.IpAddress))
        {
            filtered = filtered.Where(l => l.IpAddress == request.IpAddress);
        }

        if (request.SuccessOnly.HasValue)
        {
            filtered = filtered.Where(l => l.IsSuccess == request.SuccessOnly.Value);
        }

        var results = filtered
            .OrderByDescending(l => l.Timestamp)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(MapAuditLogResponse)
            .ToList();

        return Ok(new
        {
            Total = filtered.Count(),
            Skip = request.Skip,
            Take = request.Take,
            Results = results
        });
    }

    /// <summary>
    /// Gets audit log by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAuditLog(Guid id, CancellationToken cancellationToken)
    {
        // Since we don't have a direct GetById, search by entity
        var logs = await _auditRepository.GetByEntityAsync(
            "AuditLog",
            id.ToString(),
            1,
            cancellationToken);

        var log = logs.FirstOrDefault();

        if (log == null)
        {
            return NotFound();
        }

        return Ok(MapAuditLogResponse(log));
    }

    /// <summary>
    /// Gets audit logs for a specific entity.
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId}")]
    public async Task<IActionResult> GetEntityAuditLogs(
        string entityType,
        string entityId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditRepository.GetByEntityAsync(
            entityType,
            entityId,
            limit,
            cancellationToken);

        return Ok(logs
            .OrderByDescending(l => l.Timestamp)
            .Select(MapAuditLogResponse));
    }

    /// <summary>
    /// Gets audit logs for a specific dealer.
    /// </summary>
    [HttpGet("dealer/{dealerId:int}")]
    public async Task<IActionResult> GetDealerAuditLogs(
        int dealerId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditRepository.GetByDealerAsync(
            dealerId,
            limit,
            cancellationToken);

        return Ok(logs
            .OrderByDescending(l => l.Timestamp)
            .Select(MapAuditLogResponse));
    }

    /// <summary>
    /// Gets audit logs for a specific user.
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserAuditLogs(
        string userId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditRepository.GetByUserAsync(
            userId,
            limit,
            cancellationToken);

        return Ok(logs
            .OrderByDescending(l => l.Timestamp)
            .Select(MapAuditLogResponse));
    }

    /// <summary>
    /// Gets audit statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetAuditStats(
        [FromQuery] int hours = 24,
        [FromQuery] int? dealerId = null,
        CancellationToken cancellationToken = default)
    {
        // Get recent logs
        IReadOnlyList<AuditLog> logs;

        if (dealerId.HasValue)
        {
            logs = await _auditRepository.GetByDealerAsync(dealerId.Value, 1000, cancellationToken);
        }
        else
        {
            logs = await _auditRepository.GetByDealerAsync(0, 1000, cancellationToken);
        }

        var since = DateTime.UtcNow.AddHours(-hours);
        var recentLogs = logs.Where(l => l.Timestamp >= since).ToList();

        var stats = new
        {
            Since = since,
            TotalEvents = recentLogs.Count,
            ByAction = recentLogs
                .GroupBy(l => l.Action)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
            ByEntityType = recentLogs
                .GroupBy(l => l.EntityType)
                .ToDictionary(g => g.Key, g => g.Count()),
            SuccessRate = recentLogs.Any()
                ? (double)recentLogs.Count(l => l.IsSuccess) / recentLogs.Count * 100
                : 100.0,
            FailedEvents = recentLogs
                .Where(l => !l.IsSuccess)
                .OrderByDescending(l => l.Timestamp)
                .Take(10)
                .Select(l => new
                {
                    l.Id,
                    l.Action,
                    l.EntityType,
                    l.EntityId,
                    l.ErrorMessage,
                    l.Timestamp
                }),
            TopUsers = recentLogs
                .GroupBy(l => l.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(u => u.Count)
                .Take(10),
            HourlyActivity = recentLogs
                .GroupBy(l => l.Timestamp.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .OrderBy(h => h.Hour)
        };

        return Ok(stats);
    }

    /// <summary>
    /// Gets security-related audit events.
    /// </summary>
    [HttpGet("security")]
    public async Task<IActionResult> GetSecurityEvents(
        [FromQuery] int hours = 24,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        // Get recent logs and filter for security-related actions
        var logs = await _auditRepository.GetByDealerAsync(0, 1000, cancellationToken);

        var since = DateTime.UtcNow.AddHours(-hours);
        var securityActions = new[]
        {
            AuditAction.Login,
            AuditAction.Logout,
            AuditAction.AccessDenied,
            AuditAction.ConfigChange
        };

        var securityEvents = logs
            .Where(l => l.Timestamp >= since && securityActions.Any(a => a == l.Action))
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .Select(MapAuditLogResponse)
            .ToList();

        return Ok(new
        {
            Since = since,
            TotalEvents = securityEvents.Count,
            AccessDeniedCount = securityEvents.Count(e => e.Action == "AccessDenied"),
            Events = securityEvents
        });
    }

    private static AuditLogResponse MapAuditLogResponse(AuditLog log)
    {
        return new AuditLogResponse
        {
            Id = log.Id,
            Action = log.Action.ToString(),
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            UserId = log.UserId,
            UserName = log.UserName,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            RequestPath = log.RequestPath,
            HttpMethod = log.HttpMethod,
            DealerId = log.DealerId,
            IsSuccess = log.IsSuccess,
            ErrorMessage = log.ErrorMessage,
            DurationMs = log.DurationMs,
            Notes = log.Notes,
            Timestamp = log.Timestamp,
            HasChanges = !string.IsNullOrEmpty(log.OldValues) || !string.IsNullOrEmpty(log.NewValues)
        };
    }
}

public class AuditSearchRequest
{
    public int? DealerId { get; set; }
    public string? UserId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? IpAddress { get; set; }
    public bool? SuccessOnly { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
    public int Limit { get; set; } = 500;
}

public class AuditLogResponse
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public int? DealerId { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; }
    public bool HasChanges { get; set; }
}
