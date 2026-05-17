using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for audit log operations.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Adds a new audit log entry.
    /// </summary>
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit logs for a specific entity.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetByEntityAsync(
        string entityType,
        string entityId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit logs for a specific dealer.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetByDealerAsync(
        int dealerId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent audit logs (all dealers).
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit logs for a specific user.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetByUserAsync(
        string userId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches audit logs with filters.
    /// </summary>
    Task<AuditLogSearchResult> SearchAsync(
        AuditLogSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit log statistics.
    /// </summary>
    Task<AuditLogStatistics> GetStatisticsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old audit logs.
    /// </summary>
    Task<int> CleanupOldLogsAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Search criteria for audit logs.
/// </summary>
public record AuditLogSearchCriteria
{
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? UserId { get; init; }
    public int? DealerId { get; init; }
    public AuditAction? Action { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? CorrelationId { get; init; }
    public bool? IsSuccess { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// Result of an audit log search.
/// </summary>
public record AuditLogSearchResult
{
    public IReadOnlyList<AuditLog> Items { get; init; } = Array.Empty<AuditLog>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Statistics about audit logs.
/// </summary>
public record AuditLogStatistics
{
    public int TotalLogs { get; init; }
    public int CreateActions { get; init; }
    public int UpdateActions { get; init; }
    public int DeleteActions { get; init; }
    public Dictionary<string, int> ByEntityType { get; init; } = new();
    public Dictionary<string, int> ByUser { get; init; } = new();
    public int FailedOperations { get; init; }
}
