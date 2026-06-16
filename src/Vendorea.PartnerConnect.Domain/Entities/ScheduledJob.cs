namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// A configurable recurring background job, scheduled by a cron expression and managed from the
/// admin portal's "Cron Jobs" tab. Each job is bound to a handler by <see cref="JobKey"/>; the
/// background coordinator runs due jobs and records each run as a <see cref="ScheduledJobRun"/>.
/// This is the generic framework — the SPR inventory importer is the first job to use it.
/// </summary>
public class ScheduledJob
{
    public int Id { get; set; }

    /// <summary>
    /// Stable key that binds this job to its handler (e.g. "spr-inventory"). Unique.
    /// </summary>
    public string JobKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Standard cron expression (evaluated in <see cref="TimeZoneId"/>). Default: once daily.</summary>
    public string CronExpression { get; set; } = "0 6 * * *";

    /// <summary>IANA/Windows time zone the cron is evaluated in (e.g. "America/New_York"). Default UTC.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>When false, the coordinator skips this job (kept for history/config).</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Job-specific configuration as JSON (e.g. SPR inventory FTP host/user/pass/filename).</summary>
    public string? ConfigJson { get; set; }

    /// <summary>Next scheduled fire time (UTC), computed from the cron after each run/save.</summary>
    public DateTime? NextDueAt { get; set; }

    public DateTime? LastRunAt { get; set; }

    /// <summary>Outcome of the most recent run (Succeeded/Failed/Running) for at-a-glance status.</summary>
    public JobRunStatus? LastRunStatus { get; set; }

    public string? LastRunDetail { get; set; }

    /// <summary>
    /// Lease for overlap protection: set when the coordinator claims the job to run it, cleared on
    /// completion. A stale claim (older than the lease window) can be reclaimed after a crash.
    /// </summary>
    public DateTime? ClaimedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<ScheduledJobRun> Runs { get; set; } = new List<ScheduledJobRun>();
}

/// <summary>Outcome of a single job execution.</summary>
public enum JobRunStatus
{
    Running,
    Succeeded,
    Failed
}
