using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Metering.Interfaces;

/// <summary>
/// Repository for usage record persistence.
/// </summary>
public interface IUsageRepository
{
    /// <summary>
    /// Adds a batch of usage records.
    /// </summary>
    Task AddBatchAsync(IEnumerable<UsageRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage records for a dealer within a time range.
    /// </summary>
    Task<IReadOnlyList<UsageRecord>> GetByDealerAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        MetricType? metricType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets non-aggregated records for aggregation.
    /// </summary>
    Task<IReadOnlyList<UsageRecord>> GetPendingAggregationAsync(
        DateTime cutoffTime,
        int limit = 10000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks records as aggregated.
    /// </summary>
    Task MarkAggregatedAsync(IEnumerable<Guid> recordIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old records.
    /// </summary>
    Task<int> DeleteOldRecordsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a usage summary.
    /// </summary>
    Task UpsertSummaryAsync(UsageSummary summary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage summaries for a dealer.
    /// </summary>
    Task<IReadOnlyList<UsageSummary>> GetSummariesAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        PeriodGranularity granularity,
        MetricType? metricType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old summaries.
    /// </summary>
    Task<int> DeleteOldSummariesAsync(
        PeriodGranularity granularity,
        DateTime cutoffDate,
        CancellationToken cancellationToken = default);
}
