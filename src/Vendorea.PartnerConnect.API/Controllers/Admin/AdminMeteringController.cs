using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Metering.Interfaces;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for usage reports and metering data.
/// </summary>
[ApiController]
[Route("api/admin/metering")]
public class AdminMeteringController : ControllerBase
{
    private readonly IMeteringService _meteringService;
    private readonly ILogger<AdminMeteringController> _logger;

    public AdminMeteringController(
        IMeteringService meteringService,
        ILogger<AdminMeteringController> logger)
    {
        _meteringService = meteringService;
        _logger = logger;
    }

    /// <summary>
    /// Gets usage summary for a specific dealer.
    /// </summary>
    [HttpGet("dealer/{dealerId:int}/summary")]
    public async Task<IActionResult> GetDealerUsageSummary(
        int dealerId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string granularity = "daily",
        CancellationToken cancellationToken = default)
    {
        var end = endDate ?? DateTime.UtcNow;
        var start = startDate ?? end.AddDays(-30);

        if (!Enum.TryParse<PeriodGranularity>(granularity, true, out var periodGranularity))
        {
            periodGranularity = PeriodGranularity.Daily;
        }

        var summaries = await _meteringService.GetUsageSummariesAsync(
            dealerId,
            start,
            end,
            periodGranularity,
            null,
            cancellationToken);

        return Ok(new
        {
            DealerId = dealerId,
            StartDate = start,
            EndDate = end,
            Granularity = periodGranularity.ToString(),
            Summaries = summaries.Select(s => new
            {
                s.MetricType,
                s.PeriodStart,
                s.PeriodEnd,
                s.TotalValue,
                s.RecordCount,
                s.Unit
            })
        });
    }

    /// <summary>
    /// Gets usage report for a dealer.
    /// </summary>
    [HttpGet("dealer/{dealerId:int}/report")]
    public async Task<IActionResult> GetDealerUsageReport(
        int dealerId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var end = endDate ?? DateTime.UtcNow;
        var start = startDate ?? end.AddDays(-30);

        var report = await _meteringService.GenerateUsageReportAsync(
            dealerId,
            start,
            end,
            cancellationToken);

        return Ok(new
        {
            report.DealerId,
            report.StartDate,
            report.EndDate,
            report.EstimatedCost,
            Metrics = report.Metrics.Select(m => new
            {
                m.MetricType,
                m.TotalValue,
                m.Unit,
                m.UnitCost,
                m.TotalCost
            })
        });
    }

    /// <summary>
    /// Gets current period usage for a dealer.
    /// </summary>
    [HttpGet("dealer/{dealerId:int}/current")]
    public async Task<IActionResult> GetCurrentPeriodUsage(
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        var report = await _meteringService.GetCurrentPeriodUsageAsync(dealerId, cancellationToken);

        return Ok(new
        {
            report.DealerId,
            report.StartDate,
            report.EndDate,
            report.EstimatedCost,
            Metrics = report.Metrics.Select(m => new
            {
                m.MetricType,
                m.TotalValue,
                m.Unit,
                m.TotalCost
            })
        });
    }

    /// <summary>
    /// Gets usage records for a dealer.
    /// </summary>
    [HttpGet("dealer/{dealerId:int}/records")]
    public async Task<IActionResult> GetUsageRecords(
        int dealerId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? metricType = null,
        CancellationToken cancellationToken = default)
    {
        var end = endDate ?? DateTime.UtcNow;
        var start = startDate ?? end.AddDays(-7);

        MetricType? metric = null;
        if (!string.IsNullOrEmpty(metricType) && Enum.TryParse<MetricType>(metricType, true, out var parsedMetric))
        {
            metric = parsedMetric;
        }

        var records = await _meteringService.GetUsageRecordsAsync(
            dealerId,
            start,
            end,
            metric,
            cancellationToken);

        return Ok(new
        {
            DealerId = dealerId,
            StartDate = start,
            EndDate = end,
            TotalRecords = records.Count,
            Records = records.Select(r => new
            {
                r.Id,
                r.MetricType,
                r.Value,
                r.Unit,
                r.ResourceId,
                r.Timestamp
            })
        });
    }

    /// <summary>
    /// Gets available metric types.
    /// </summary>
    [HttpGet("metric-types")]
    public IActionResult GetMetricTypes()
    {
        var metricTypes = Enum.GetValues<MetricType>()
            .Select(m => new { Value = m.ToString(), Description = GetMetricDescription(m) })
            .ToList();

        return Ok(metricTypes);
    }

    /// <summary>
    /// Flushes any buffered metrics.
    /// </summary>
    [HttpPost("flush")]
    public async Task<IActionResult> FlushMetrics(CancellationToken cancellationToken)
    {
        await _meteringService.FlushAsync(cancellationToken);
        return Ok(new { Message = "Metrics flushed successfully", Timestamp = DateTime.UtcNow });
    }

    private static string GetMetricDescription(MetricType metricType)
    {
        return metricType switch
        {
            MetricType.DocumentProcessed => "Number of documents processed",
            MetricType.ApiCall => "Number of API calls made",
            MetricType.StorageUsed => "Storage space used in bytes",
            MetricType.WebhookDelivery => "Number of webhooks delivered",
            _ => metricType.ToString()
        };
    }
}
