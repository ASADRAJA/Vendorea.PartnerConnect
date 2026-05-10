using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Metering.Interfaces;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository for usage record persistence.
/// </summary>
public class UsageRepository : IUsageRepository
{
    private readonly PartnerConnectDbContext _context;

    public UsageRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddBatchAsync(IEnumerable<UsageRecord> records, CancellationToken cancellationToken = default)
    {
        await _context.UsageRecords.AddRangeAsync(records, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageRecord>> GetByDealerAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        MetricType? metricType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.UsageRecords
            .Where(u => u.DealerId == dealerId
                && u.Timestamp >= startDate
                && u.Timestamp < endDate);

        if (metricType.HasValue)
        {
            query = query.Where(u => u.MetricType == metricType.Value);
        }

        return await query
            .OrderByDescending(u => u.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageRecord>> GetPendingAggregationAsync(
        DateTime cutoffTime,
        int limit = 10000,
        CancellationToken cancellationToken = default)
    {
        return await _context.UsageRecords
            .Where(u => !u.IsAggregated && u.Timestamp < cutoffTime)
            .OrderBy(u => u.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkAggregatedAsync(IEnumerable<Guid> recordIds, CancellationToken cancellationToken = default)
    {
        var ids = recordIds.ToList();
        if (ids.Count == 0) return;

        await _context.UsageRecords
            .Where(u => ids.Contains(u.Id))
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.IsAggregated, true),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> DeleteOldRecordsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await _context.UsageRecords
            .Where(u => u.IsAggregated && u.Timestamp < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertSummaryAsync(UsageSummary summary, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UsageSummaries
            .FirstOrDefaultAsync(
                s => s.DealerId == summary.DealerId
                    && s.MetricType == summary.MetricType
                    && s.Granularity == summary.Granularity
                    && s.PeriodStart == summary.PeriodStart,
                cancellationToken);

        if (existing != null)
        {
            // Update existing summary
            existing.TotalValue += summary.TotalValue;
            existing.RecordCount += summary.RecordCount;
            existing.MinValue = Math.Min(existing.MinValue, summary.MinValue);
            existing.MaxValue = Math.Max(existing.MaxValue, summary.MaxValue);
            existing.AverageValue = existing.RecordCount > 0
                ? existing.TotalValue / existing.RecordCount
                : 0;
        }
        else
        {
            await _context.UsageSummaries.AddAsync(summary, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsageSummary>> GetSummariesAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        PeriodGranularity granularity,
        MetricType? metricType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.UsageSummaries
            .Where(s => s.PeriodStart >= startDate
                && s.PeriodEnd <= endDate
                && s.Granularity == granularity);

        if (dealerId > 0)
        {
            query = query.Where(s => s.DealerId == dealerId);
        }

        if (metricType.HasValue)
        {
            query = query.Where(s => s.MetricType == metricType.Value);
        }

        return await query
            .OrderBy(s => s.PeriodStart)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> DeleteOldSummariesAsync(
        PeriodGranularity granularity,
        DateTime cutoffDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.UsageSummaries
            .Where(s => s.Granularity == granularity && s.PeriodEnd < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
