using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Orchestrates the scheduled-jobs framework: admin management (list/get/update/history), cron
/// validation/preview, and execution (used by both the background coordinator and manual "Run now").
/// </summary>
public interface IScheduledJobService
{
    Task<IReadOnlyList<ScheduledJob>> GetJobsAsync(CancellationToken cancellationToken = default);
    Task<ScheduledJob?> GetJobAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledJobRun>> GetRecentRunsAsync(int jobId, int take = 20, CancellationToken cancellationToken = default);

    /// <summary>Updates a job's schedule/enabled/config and recomputes its next run time.</summary>
    Task<ScheduledJob?> UpdateJobAsync(
        int id, string cronExpression, string timeZoneId, bool isEnabled, string? configJson,
        CancellationToken cancellationToken = default);

    /// <summary>Validates a cron expression and returns the next <paramref name="count"/> run times (UTC).</summary>
    CronPreview PreviewCron(string cronExpression, string timeZoneId, int count = 5);

    /// <summary>
    /// Coordinator entry point: runs every due, enabled, claimable job once. Safe to call on a timer.
    /// </summary>
    Task RunDueJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin "Run now": executes a job immediately regardless of schedule.</summary>
    Task<JobExecutionResult> RunNowAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Result of validating a cron expression, with upcoming run times for confirmation.</summary>
public record CronPreview(bool IsValid, string? Error, IReadOnlyList<DateTime> NextRunsUtc);
