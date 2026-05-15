using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Billing.Interfaces;
using Vendorea.PartnerConnect.Metering.Interfaces;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Public API v1 controller for usage and billing information.
/// </summary>
[ApiController]
[Route("api/v1/usage")]
[AllowAnonymous] // TODO: Restore [Authorize(AuthenticationSchemes = "ApiKey")] in production
public class PublicUsageController : ControllerBase
{
    private readonly IMeteringService _meteringService;
    private readonly IBillingService _billingService;
    private readonly ILogger<PublicUsageController> _logger;

    public PublicUsageController(
        IMeteringService meteringService,
        IBillingService billingService,
        ILogger<PublicUsageController> logger)
    {
        _meteringService = meteringService;
        _billingService = billingService;
        _logger = logger;
    }

    /// <summary>
    /// Gets current period usage for the authenticated dealer.
    /// </summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentUsage(CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var usage = await _meteringService.GetCurrentPeriodUsageAsync(dealerId.Value, cancellationToken);

        return Ok(new
        {
            usage.DealerId,
            usage.StartDate,
            usage.EndDate,
            usage.EstimatedCost,
            Metrics = usage.Metrics.Select(m => new
            {
                m.MetricType,
                m.TotalValue,
                m.Unit,
                m.TotalCost
            })
        });
    }

    /// <summary>
    /// Gets usage history for the authenticated dealer.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetUsageHistory(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string granularity = "daily",
        CancellationToken cancellationToken = default)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var end = endDate ?? DateTime.UtcNow;
        var start = startDate ?? end.AddDays(-30);

        if (!Enum.TryParse<PeriodGranularity>(granularity, true, out var periodGranularity))
        {
            periodGranularity = PeriodGranularity.Daily;
        }

        var summaries = await _meteringService.GetUsageSummariesAsync(
            dealerId.Value,
            start,
            end,
            periodGranularity,
            null,
            cancellationToken);

        var grouped = summaries
            .GroupBy(s => s.PeriodStart)
            .Select(g => new
            {
                Period = g.Key,
                Documents = g.Where(s => s.MetricType == MetricType.DocumentProcessed).Sum(s => (long)s.TotalValue),
                ApiCalls = g.Where(s => s.MetricType == MetricType.ApiCall).Sum(s => (long)s.TotalValue),
                StorageBytes = g.Where(s => s.MetricType == MetricType.StorageUsed).Sum(s => (long)s.TotalValue),
                Webhooks = g.Where(s => s.MetricType == MetricType.WebhookDelivery).Sum(s => (long)s.TotalValue)
            })
            .OrderBy(g => g.Period)
            .ToList();

        return Ok(new
        {
            DealerId = dealerId.Value,
            StartDate = start,
            EndDate = end,
            Granularity = periodGranularity.ToString(),
            History = grouped
        });
    }

    /// <summary>
    /// Gets a usage report for the authenticated dealer.
    /// </summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetUsageReport(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var end = endDate ?? DateTime.UtcNow;
        var start = startDate ?? end.AddDays(-30);

        var report = await _meteringService.GenerateUsageReportAsync(
            dealerId.Value,
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
                m.TotalCost,
                DailyBreakdown = m.DailyBreakdown.Select(d => new
                {
                    d.Date,
                    d.Value
                })
            })
        });
    }

    /// <summary>
    /// Gets billing summary for the authenticated dealer.
    /// </summary>
    [HttpGet("billing")]
    public async Task<IActionResult> GetBillingSummary(CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var subscription = await _billingService.GetSubscriptionAsync(dealerId.Value, cancellationToken);

        if (subscription == null)
        {
            return Ok(new
            {
                HasSubscription = false,
                Message = "No active subscription found"
            });
        }

        return Ok(new
        {
            HasSubscription = true,
            Subscription = new
            {
                PlanCode = subscription.BillingPlan?.Code,
                PlanName = subscription.BillingPlan?.Name,
                Status = subscription.Status.ToString(),
                BillingInterval = subscription.BillingInterval.ToString(),
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd,
                subscription.IsActive,
                subscription.IsTrialing,
                subscription.TrialEndAt
            }
        });
    }

    /// <summary>
    /// Gets usage by metric type.
    /// </summary>
    [HttpGet("metrics/{metricType}")]
    public async Task<IActionResult> GetMetricUsage(
        string metricType,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        if (!Enum.TryParse<MetricType>(metricType, true, out var metric))
        {
            return BadRequest($"Invalid metric type. Valid types: {string.Join(", ", Enum.GetNames<MetricType>())}");
        }

        var end = endDate ?? DateTime.UtcNow;
        var start = startDate ?? end.AddDays(-30);

        var records = await _meteringService.GetUsageRecordsAsync(
            dealerId.Value,
            start,
            end,
            metric,
            cancellationToken);

        var summaries = await _meteringService.GetUsageSummariesAsync(
            dealerId.Value,
            start,
            end,
            PeriodGranularity.Daily,
            metric,
            cancellationToken);

        return Ok(new
        {
            MetricType = metric.ToString(),
            StartDate = start,
            EndDate = end,
            TotalValue = summaries.Sum(s => s.TotalValue),
            TotalCount = summaries.Sum(s => s.RecordCount),
            DailySummary = summaries
                .OrderBy(s => s.PeriodStart)
                .Select(s => new
                {
                    Date = s.PeriodStart,
                    Value = s.TotalValue,
                    Count = s.RecordCount
                }),
            RecentRecords = records
                .OrderByDescending(r => r.Timestamp)
                .Take(100)
                .Select(r => new
                {
                    r.Timestamp,
                    r.Value,
                    r.Unit,
                    r.ResourceId
                })
        });
    }

    private int? GetDealerIdFromClaims()
    {
        var dealerIdClaim = User.FindFirst("DealerId")?.Value;
        if (int.TryParse(dealerIdClaim, out var dealerId))
        {
            return dealerId;
        }
        return null;
    }
}
