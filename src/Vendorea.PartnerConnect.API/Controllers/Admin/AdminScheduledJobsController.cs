using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for the generic cron-jobs framework — backs the "Cron Jobs" tab. Lets staff
/// view jobs, edit their schedule/enabled/config, preview upcoming run times, trigger a run now,
/// and see run history.
/// </summary>
[ApiController]
[Route("api/admin/scheduled-jobs")]
public class AdminScheduledJobsController : ControllerBase
{
    private readonly IScheduledJobService _service;
    private readonly ILogger<AdminScheduledJobsController> _logger;

    public AdminScheduledJobsController(IScheduledJobService service, ILogger<AdminScheduledJobsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetJobs(CancellationToken cancellationToken)
    {
        var jobs = await _service.GetJobsAsync(cancellationToken);
        return Ok(jobs.Select(MapToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetJob(int id, CancellationToken cancellationToken)
    {
        var job = await _service.GetJobAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(MapToDto(job));
    }

    [HttpGet("{id:int}/runs")]
    public async Task<IActionResult> GetRuns(int id, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var job = await _service.GetJobAsync(id, cancellationToken);
        if (job is null)
            return NotFound();

        var runs = await _service.GetRecentRunsAsync(id, Math.Clamp(take, 1, 100), cancellationToken);
        return Ok(runs.Select(MapRunToDto).ToList());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateJob(int id, [FromBody] UpdateScheduledJobRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _service.UpdateJobAsync(
                id, request.CronExpression ?? string.Empty, request.TimeZoneId ?? "UTC",
                request.IsEnabled, request.ConfigJson, cancellationToken);
            return job is null ? NotFound() : Ok(MapToDto(job));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/run")]
    public async Task<IActionResult> RunNow(int id, CancellationToken cancellationToken)
    {
        var result = await _service.RunNowAsync(id, cancellationToken);
        return Ok(new { success = result.Success, detail = result.Detail, error = result.ErrorMessage });
    }

    [HttpPost("preview-cron")]
    public IActionResult PreviewCron([FromBody] CronPreviewRequest request)
    {
        var preview = _service.PreviewCron(request.CronExpression ?? string.Empty, request.TimeZoneId ?? "UTC", request.Count ?? 5);
        return Ok(new CronPreviewResponse
        {
            IsValid = preview.IsValid,
            Error = preview.Error,
            NextRunsUtc = preview.NextRunsUtc.ToList()
        });
    }

    private static ScheduledJobDto MapToDto(ScheduledJob j) => new()
    {
        Id = j.Id,
        JobKey = j.JobKey,
        DisplayName = j.DisplayName,
        Description = j.Description,
        CronExpression = j.CronExpression,
        TimeZoneId = j.TimeZoneId,
        IsEnabled = j.IsEnabled,
        ConfigJson = j.ConfigJson,
        NextDueAt = j.NextDueAt,
        LastRunAt = j.LastRunAt,
        LastRunStatus = j.LastRunStatus?.ToString(),
        LastRunDetail = j.LastRunDetail
    };

    private static ScheduledJobRunDto MapRunToDto(ScheduledJobRun r) => new()
    {
        Id = r.Id,
        StartedAt = r.StartedAt,
        CompletedAt = r.CompletedAt,
        Status = r.Status.ToString(),
        TriggeredBy = r.TriggeredBy,
        Detail = r.Detail,
        ErrorMessage = r.ErrorMessage
    };
}

public class ScheduledJobDto
{
    public int Id { get; set; }
    public string JobKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? ConfigJson { get; set; }
    public DateTime? NextDueAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? LastRunStatus { get; set; }
    public string? LastRunDetail { get; set; }
}

public class ScheduledJobRunDto
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string TriggeredBy { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? ErrorMessage { get; set; }
}

public class UpdateScheduledJobRequest
{
    public string? CronExpression { get; set; }
    public string? TimeZoneId { get; set; }
    public bool IsEnabled { get; set; }
    public string? ConfigJson { get; set; }
}

public class CronPreviewRequest
{
    public string? CronExpression { get; set; }
    public string? TimeZoneId { get; set; }
    public int? Count { get; set; }
}

public class CronPreviewResponse
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public List<DateTime> NextRunsUtc { get; set; } = new();
}
