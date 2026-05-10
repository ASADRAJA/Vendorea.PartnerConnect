using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class ContentSyncJobRepository : IContentSyncJobRepository
{
    private readonly PartnerConnectDbContext _context;

    public ContentSyncJobRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<ContentSyncJob?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.ContentSyncJobs
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ContentSyncJob>> GetByDealerIdAsync(
        int dealerId,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ContentSyncJobs
            .Where(j => j.DealerId == dealerId)
            .OrderByDescending(j => j.ScheduledAt)
            .AsQueryable();

        if (skip.HasValue)
        {
            query = query.Skip(skip.Value);
        }

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContentSyncJob>> GetByStatusAsync(
        ContentSyncStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _context.ContentSyncJobs
            .Where(j => j.Status == status)
            .OrderBy(j => j.ScheduledAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContentSyncJob?> GetLatestByDealerPartnerAsync(
        int dealerId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ContentSyncJobs
            .Where(j => j.DealerId == dealerId &&
                        j.TradingPartnerId == tradingPartnerId)
            .OrderByDescending(j => j.ScheduledAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContentSyncJob>> GetScheduledJobsAsync(
        DateTime asOfTime,
        CancellationToken cancellationToken = default)
    {
        return await _context.ContentSyncJobs
            .Where(j => j.Status == ContentSyncStatus.Scheduled &&
                        j.ScheduledAt <= asOfTime)
            .OrderBy(j => j.ScheduledAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContentSyncJob>> GetStaleRunningJobsAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow - timeout;

        return await _context.ContentSyncJobs
            .Where(j => j.Status == ContentSyncStatus.Running &&
                        j.StartedAt.HasValue &&
                        j.StartedAt.Value < cutoffTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContentSyncJob>> GetByDateRangeAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.ContentSyncJobs
            .Where(j => j.DealerId == dealerId &&
                        j.ScheduledAt >= startDate &&
                        j.ScheduledAt <= endDate)
            .OrderByDescending(j => j.ScheduledAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContentSyncJob> AddAsync(ContentSyncJob job, CancellationToken cancellationToken = default)
    {
        _context.ContentSyncJobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task UpdateAsync(ContentSyncJob job, CancellationToken cancellationToken = default)
    {
        _context.ContentSyncJobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContentSyncStatistics> GetStatisticsAsync(
        int dealerId,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ContentSyncJobs
            .Where(j => j.DealerId == dealerId);

        if (since.HasValue)
        {
            query = query.Where(j => j.ScheduledAt >= since.Value);
        }

        var jobs = await query.ToListAsync(cancellationToken);

        return new ContentSyncStatistics(
            TotalJobs: jobs.Count,
            TotalProductsProcessed: jobs.Sum(j => j.ProcessedProducts),
            TotalProductsUpdated: jobs.Sum(j => j.UpdatedProducts),
            TotalImagesDownloaded: jobs.Sum(j => j.NewImagesDownloaded),
            TotalErrors: jobs.Sum(j => j.ErrorProducts),
            CompletedJobs: jobs.Count(j => j.Status == ContentSyncStatus.Completed),
            FailedJobs: jobs.Count(j => j.Status == ContentSyncStatus.Failed),
            LastSyncAt: jobs
                .Where(j => j.Status == ContentSyncStatus.Completed)
                .OrderByDescending(j => j.CompletedAt)
                .Select(j => j.CompletedAt)
                .FirstOrDefault()
        );
    }
}
