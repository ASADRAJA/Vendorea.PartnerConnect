namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>History of a single execution of a <see cref="ScheduledJob"/>.</summary>
public class ScheduledJobRun
{
    public int Id { get; set; }

    public int ScheduledJobId { get; set; }

    /// <summary>Denormalized job key for convenient querying/display.</summary>
    public string JobKey { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public JobRunStatus Status { get; set; } = JobRunStatus.Running;

    /// <summary>"Schedule" or "Manual".</summary>
    public string TriggeredBy { get; set; } = "Schedule";

    /// <summary>Short human-readable summary/stats of the run.</summary>
    public string? Detail { get; set; }

    public string? ErrorMessage { get; set; }

    public ScheduledJob? ScheduledJob { get; set; }
}
