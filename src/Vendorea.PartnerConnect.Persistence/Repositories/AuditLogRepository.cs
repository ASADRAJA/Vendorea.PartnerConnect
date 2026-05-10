using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository for audit log operations.
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly PartnerConnectDbContext _context;

    public AuditLogRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogs.AddAsync(auditLog, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLog>> GetByEntityAsync(
        string entityType,
        string entityId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLog>> GetByDealerAsync(
        int dealerId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.DealerId == dealerId)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLog>> GetByUserAsync(
        string userId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuditLogSearchResult> SearchAsync(
        AuditLogSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(criteria.EntityType))
        {
            query = query.Where(a => a.EntityType == criteria.EntityType);
        }

        if (!string.IsNullOrEmpty(criteria.EntityId))
        {
            query = query.Where(a => a.EntityId == criteria.EntityId);
        }

        if (!string.IsNullOrEmpty(criteria.UserId))
        {
            query = query.Where(a => a.UserId == criteria.UserId);
        }

        if (criteria.DealerId.HasValue)
        {
            query = query.Where(a => a.DealerId == criteria.DealerId);
        }

        if (criteria.Action.HasValue)
        {
            query = query.Where(a => a.Action == criteria.Action);
        }

        if (criteria.StartDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= criteria.StartDate);
        }

        if (criteria.EndDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= criteria.EndDate);
        }

        if (!string.IsNullOrEmpty(criteria.CorrelationId))
        {
            query = query.Where(a => a.CorrelationId == criteria.CorrelationId);
        }

        if (criteria.IsSuccess.HasValue)
        {
            query = query.Where(a => a.IsSuccess == criteria.IsSuccess);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return new AuditLogSearchResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = criteria.Page,
            PageSize = criteria.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<AuditLogStatistics> GetStatisticsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= startDate);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= endDate);
        }

        var totalLogs = await query.CountAsync(cancellationToken);
        var createActions = await query.CountAsync(a => a.Action == AuditAction.Create, cancellationToken);
        var updateActions = await query.CountAsync(a => a.Action == AuditAction.Update, cancellationToken);
        var deleteActions = await query.CountAsync(a => a.Action == AuditAction.Delete, cancellationToken);
        var failedOperations = await query.CountAsync(a => !a.IsSuccess, cancellationToken);

        var byEntityType = await query
            .GroupBy(a => a.EntityType)
            .Select(g => new { EntityType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EntityType, x => x.Count, cancellationToken);

        var byUser = await query
            .Where(a => a.UserId != null)
            .GroupBy(a => a.UserId!)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .Take(20) // Limit to top 20 users
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        return new AuditLogStatistics
        {
            TotalLogs = totalLogs,
            CreateActions = createActions,
            UpdateActions = updateActions,
            DeleteActions = deleteActions,
            ByEntityType = byEntityType,
            ByUser = byUser,
            FailedOperations = failedOperations
        };
    }

    /// <inheritdoc />
    public async Task<int> CleanupOldLogsAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - olderThan;

        var logsToDelete = await _context.AuditLogs
            .Where(a => a.Timestamp < cutoff)
            .ToListAsync(cancellationToken);

        _context.AuditLogs.RemoveRange(logsToDelete);
        await _context.SaveChangesAsync(cancellationToken);

        return logsToDelete.Count;
    }
}
