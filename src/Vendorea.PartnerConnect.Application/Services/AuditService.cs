using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for manual audit logging.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs an audit entry.
    /// </summary>
    Task LogAsync(
        AuditAction action,
        string entityType,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        int? dealerId = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an operation execution.
    /// </summary>
    Task LogOperationAsync(
        string operationName,
        int? dealerId = null,
        string? notes = null,
        bool isSuccess = true,
        string? errorMessage = null,
        int? durationMs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an access denied event.
    /// </summary>
    Task LogAccessDeniedAsync(
        string resource,
        int? dealerId = null,
        string? notes = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of audit service for manual audit logging.
/// </summary>
public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IAuditLogRepository auditLogRepository,
        IHttpContextAccessor? httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _auditLogRepository = auditLogRepository;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogAsync(
        AuditAction action,
        string entityType,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        int? dealerId = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor?.HttpContext;

        var auditLog = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserId = httpContext?.User?.Identity?.Name ?? "System",
            UserName = httpContext?.User?.Identity?.Name ?? "System",
            IpAddress = GetClientIpAddress(httpContext),
            UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
            RequestPath = httpContext?.Request?.Path.ToString(),
            HttpMethod = httpContext?.Request?.Method,
            CorrelationId = httpContext?.TraceIdentifier,
            DealerId = dealerId,
            Notes = notes,
            OldValues = oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for {EntityType}/{EntityId}", entityType, entityId);
        }
    }

    /// <inheritdoc />
    public async Task LogOperationAsync(
        string operationName,
        int? dealerId = null,
        string? notes = null,
        bool isSuccess = true,
        string? errorMessage = null,
        int? durationMs = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor?.HttpContext;

        var auditLog = new AuditLog
        {
            Action = AuditAction.Execute,
            EntityType = "Operation",
            EntityId = operationName,
            UserId = httpContext?.User?.Identity?.Name ?? "System",
            UserName = httpContext?.User?.Identity?.Name ?? "System",
            IpAddress = GetClientIpAddress(httpContext),
            UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
            RequestPath = httpContext?.Request?.Path.ToString(),
            HttpMethod = httpContext?.Request?.Method,
            CorrelationId = httpContext?.TraceIdentifier,
            DealerId = dealerId,
            Notes = notes,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            DurationMs = durationMs,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for operation {OperationName}", operationName);
        }
    }

    /// <inheritdoc />
    public async Task LogAccessDeniedAsync(
        string resource,
        int? dealerId = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor?.HttpContext;

        var auditLog = new AuditLog
        {
            Action = AuditAction.AccessDenied,
            EntityType = "Resource",
            EntityId = resource,
            UserId = httpContext?.User?.Identity?.Name ?? "Anonymous",
            UserName = httpContext?.User?.Identity?.Name ?? "Anonymous",
            IpAddress = GetClientIpAddress(httpContext),
            UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
            RequestPath = httpContext?.Request?.Path.ToString(),
            HttpMethod = httpContext?.Request?.Method,
            CorrelationId = httpContext?.TraceIdentifier,
            DealerId = dealerId,
            Notes = notes,
            IsSuccess = false,
            ErrorMessage = "Access denied",
            Timestamp = DateTime.UtcNow
        };

        try
        {
            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write access denied audit log for {Resource}", resource);
        }
    }

    private static string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null) return null;

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
