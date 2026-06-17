using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

public interface IScheduledJobRepository
{
    Task<IReadOnlyList<ScheduledJob>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ScheduledJob?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ScheduledJob?> GetByKeyAsync(string jobKey, CancellationToken cancellationToken = default);
    Task<ScheduledJob> AddAsync(ScheduledJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(ScheduledJob job, CancellationToken cancellationToken = default);

    /// <summary>Enabled jobs whose next run is due at/before <paramref name="nowUtc"/>.</summary>
    Task<IReadOnlyList<ScheduledJob>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a job for execution: sets ClaimedAt = nowUtc only if it is currently
    /// unclaimed or the existing claim is older than <paramref name="staleClaimBeforeUtc"/>.
    /// Returns true if the claim was taken (i.e., this caller may run the job).
    /// </summary>
    Task<bool> TryClaimAsync(int jobId, DateTime nowUtc, DateTime staleClaimBeforeUtc, CancellationToken cancellationToken = default);

    Task<ScheduledJobRun> AddRunAsync(ScheduledJobRun run, CancellationToken cancellationToken = default);
    Task UpdateRunAsync(ScheduledJobRun run, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledJobRun>> GetRecentRunsAsync(int jobId, int take, CancellationToken cancellationToken = default);
}
