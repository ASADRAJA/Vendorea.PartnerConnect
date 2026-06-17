using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class ScheduledJobRepository : IScheduledJobRepository
{
    private readonly PartnerConnectDbContext _context;

    public ScheduledJobRepository(PartnerConnectDbContext context) => _context = context;

    public async Task<IReadOnlyList<ScheduledJob>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.ScheduledJobs.AsNoTracking().OrderBy(j => j.DisplayName).ToListAsync(cancellationToken);

    public async Task<ScheduledJob?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _context.ScheduledJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<ScheduledJob?> GetByKeyAsync(string jobKey, CancellationToken cancellationToken = default) =>
        await _context.ScheduledJobs.FirstOrDefaultAsync(j => j.JobKey == jobKey, cancellationToken);

    public async Task<ScheduledJob> AddAsync(ScheduledJob job, CancellationToken cancellationToken = default)
    {
        _context.ScheduledJobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task UpdateAsync(ScheduledJob job, CancellationToken cancellationToken = default)
    {
        job.UpdatedAt = DateTime.UtcNow;
        _context.ScheduledJobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduledJob>> GetEnabledAsync(CancellationToken cancellationToken = default) =>
        await _context.ScheduledJobs.Where(j => j.IsEnabled).ToListAsync(cancellationToken);

    public async Task<bool> TryClaimAsync(int jobId, DateTime nowUtc, DateTime staleClaimBeforeUtc, CancellationToken cancellationToken = default)
    {
        // Atomic conditional update: claim only if unclaimed or the claim is stale.
        var affected = await _context.ScheduledJobs
            .Where(j => j.Id == jobId && (j.ClaimedAt == null || j.ClaimedAt < staleClaimBeforeUtc))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.ClaimedAt, nowUtc), cancellationToken);
        return affected == 1;
    }

    public async Task<ScheduledJobRun> AddRunAsync(ScheduledJobRun run, CancellationToken cancellationToken = default)
    {
        _context.ScheduledJobRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task UpdateRunAsync(ScheduledJobRun run, CancellationToken cancellationToken = default)
    {
        _context.ScheduledJobRuns.Update(run);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduledJobRun>> GetRecentRunsAsync(int jobId, int take, CancellationToken cancellationToken = default) =>
        await _context.ScheduledJobRuns.AsNoTracking()
            .Where(r => r.ScheduledJobId == jobId)
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}
