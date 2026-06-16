using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <inheritdoc />
public class ScheduledJobService : IScheduledJobService
{
    /// <summary>A claim older than this is considered stale (crashed run) and can be reclaimed.</summary>
    private static readonly TimeSpan ClaimLease = TimeSpan.FromHours(2);

    private readonly IScheduledJobRepository _repository;
    private readonly IReadOnlyDictionary<string, IScheduledJobHandler> _handlers;
    private readonly ILogger<ScheduledJobService> _logger;

    public ScheduledJobService(
        IScheduledJobRepository repository,
        IEnumerable<IScheduledJobHandler> handlers,
        ILogger<ScheduledJobService> logger)
    {
        _repository = repository;
        _handlers = handlers.ToDictionary(h => h.JobKey, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public Task<IReadOnlyList<ScheduledJob>> GetJobsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<ScheduledJob?> GetJobAsync(int id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<ScheduledJobRun>> GetRecentRunsAsync(int jobId, int take = 20, CancellationToken cancellationToken = default) =>
        _repository.GetRecentRunsAsync(jobId, take, cancellationToken);

    public async Task<ScheduledJob?> UpdateJobAsync(
        int id, string cronExpression, string timeZoneId, bool isEnabled, string? configJson,
        CancellationToken cancellationToken = default)
    {
        var (ok, error) = CronSchedule.Validate(cronExpression);
        if (!ok)
            throw new ArgumentException($"Invalid cron expression: {error}");

        var job = await _repository.GetByIdAsync(id, cancellationToken);
        if (job is null)
            return null;

        job.CronExpression = cronExpression.Trim();
        job.TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId.Trim();
        job.IsEnabled = isEnabled;
        job.ConfigJson = configJson;
        job.NextDueAt = isEnabled ? CronSchedule.ComputeNext(job.CronExpression, job.TimeZoneId, DateTime.UtcNow) : null;

        await _repository.UpdateAsync(job, cancellationToken);
        return job;
    }

    public CronPreview PreviewCron(string cronExpression, string timeZoneId, int count = 5)
    {
        var (ok, error) = CronSchedule.Validate(cronExpression);
        if (!ok)
            return new CronPreview(false, error, Array.Empty<DateTime>());

        var tz = string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId;
        var next = CronSchedule.Preview(cronExpression, tz, count, DateTime.UtcNow);
        return new CronPreview(true, null, next);
    }

    public async Task RunDueJobsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var jobs = await _repository.GetEnabledAsync(cancellationToken);

        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // First time we see a job (or after enabling): set its next run, don't fire immediately.
            if (job.NextDueAt is null)
            {
                job.NextDueAt = SafeComputeNext(job, now);
                await _repository.UpdateAsync(job, cancellationToken);
                continue;
            }

            if (job.NextDueAt > now)
                continue;

            // Claim atomically so a restart/overlap can't double-run.
            if (!await _repository.TryClaimAsync(job.Id, now, now - ClaimLease, cancellationToken))
                continue;

            try
            {
                await ExecuteAsync(job, "Schedule", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled job {JobKey} threw outside the handler", job.JobKey);
            }
            finally
            {
                job.NextDueAt = SafeComputeNext(job, DateTime.UtcNow);
                job.ClaimedAt = null;
                await _repository.UpdateAsync(job, cancellationToken);
            }
        }
    }

    public async Task<JobExecutionResult> RunNowAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(id, cancellationToken);
        if (job is null)
            return JobExecutionResult.Fail("Job not found");

        var now = DateTime.UtcNow;
        if (!await _repository.TryClaimAsync(job.Id, now, now - ClaimLease, cancellationToken))
            return JobExecutionResult.Fail("Job is already running");

        try
        {
            return await ExecuteAsync(job, "Manual", cancellationToken);
        }
        finally
        {
            job.NextDueAt = job.IsEnabled ? SafeComputeNext(job, DateTime.UtcNow) : null;
            job.ClaimedAt = null;
            await _repository.UpdateAsync(job, cancellationToken);
        }
    }

    private async Task<JobExecutionResult> ExecuteAsync(ScheduledJob job, string triggeredBy, CancellationToken cancellationToken)
    {
        var run = await _repository.AddRunAsync(new ScheduledJobRun
        {
            ScheduledJobId = job.Id,
            JobKey = job.JobKey,
            StartedAt = DateTime.UtcNow,
            Status = JobRunStatus.Running,
            TriggeredBy = triggeredBy
        }, cancellationToken);

        JobExecutionResult result;
        if (!_handlers.TryGetValue(job.JobKey, out var handler))
        {
            result = JobExecutionResult.Fail($"No handler registered for job key '{job.JobKey}'");
            _logger.LogWarning("No handler registered for scheduled job {JobKey}", job.JobKey);
        }
        else
        {
            _logger.LogInformation("Running scheduled job {JobKey} ({Trigger})", job.JobKey, triggeredBy);
            try
            {
                result = await handler.ExecuteAsync(job, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled job {JobKey} failed", job.JobKey);
                result = JobExecutionResult.Fail(ex.Message);
            }
        }

        run.CompletedAt = DateTime.UtcNow;
        run.Status = result.Success ? JobRunStatus.Succeeded : JobRunStatus.Failed;
        run.Detail = Truncate(result.Detail, 2000);
        run.ErrorMessage = Truncate(result.ErrorMessage, 4000);
        await _repository.UpdateRunAsync(run, cancellationToken);

        job.LastRunAt = run.CompletedAt;
        job.LastRunStatus = run.Status;
        job.LastRunDetail = Truncate(result.Detail ?? result.ErrorMessage, 2000);

        return result;
    }

    private DateTime? SafeComputeNext(ScheduledJob job, DateTime fromUtc)
    {
        try
        {
            return CronSchedule.ComputeNext(job.CronExpression, job.TimeZoneId, fromUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not compute next run for job {JobKey} (cron '{Cron}')", job.JobKey, job.CronExpression);
            return null;
        }
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
