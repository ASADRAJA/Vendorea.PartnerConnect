using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for managing content synchronization job records.
/// </summary>
public interface IContentSyncJobRepository
{
    /// <summary>
    /// Gets a content sync job by its ID.
    /// </summary>
    Task<ContentSyncJob?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all content sync jobs for a specific dealer.
    /// </summary>
    Task<IReadOnlyList<ContentSyncJob>> GetByDealerIdAsync(
        int dealerId,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content sync jobs by status.
    /// </summary>
    Task<IReadOnlyList<ContentSyncJob>> GetByStatusAsync(
        ContentSyncStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent content sync job for a dealer-partner combination.
    /// </summary>
    Task<ContentSyncJob?> GetLatestByDealerPartnerAsync(
        int dealerId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled jobs that are ready to run.
    /// </summary>
    Task<IReadOnlyList<ContentSyncJob>> GetScheduledJobsAsync(
        DateTime asOfTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets running jobs that may be stale (started but not completed within timeout).
    /// </summary>
    Task<IReadOnlyList<ContentSyncJob>> GetStaleRunningJobsAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content sync jobs within a date range.
    /// </summary>
    Task<IReadOnlyList<ContentSyncJob>> GetByDateRangeAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new content sync job.
    /// </summary>
    Task<ContentSyncJob> AddAsync(ContentSyncJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing content sync job.
    /// </summary>
    Task UpdateAsync(ContentSyncJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregate statistics for a dealer's content syncs.
    /// </summary>
    Task<ContentSyncStatistics> GetStatisticsAsync(
        int dealerId,
        DateTime? since = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for content sync jobs.
/// </summary>
public record ContentSyncStatistics(
    int TotalJobs,
    int TotalProductsProcessed,
    int TotalProductsUpdated,
    int TotalImagesDownloaded,
    int TotalErrors,
    int CompletedJobs,
    int FailedJobs,
    DateTime? LastSyncAt);
