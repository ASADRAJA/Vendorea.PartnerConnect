using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// A unit of work runnable by the scheduled-jobs framework. Implementations register in DI and are
/// matched to a <see cref="ScheduledJob"/> by <see cref="JobKey"/>. The SPR inventory importer is
/// the first handler.
/// </summary>
public interface IScheduledJobHandler
{
    /// <summary>Stable key matching <see cref="ScheduledJob.JobKey"/> (e.g. "spr-inventory").</summary>
    string JobKey { get; }

    Task<JobExecutionResult> ExecuteAsync(ScheduledJob job, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a single handler execution.</summary>
public record JobExecutionResult(bool Success, string? Detail = null, string? ErrorMessage = null)
{
    public static JobExecutionResult Ok(string? detail = null) => new(true, detail, null);
    public static JobExecutionResult Fail(string errorMessage, string? detail = null) => new(false, detail, errorMessage);
}
