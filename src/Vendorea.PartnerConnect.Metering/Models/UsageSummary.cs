namespace Vendorea.PartnerConnect.Metering.Models;

/// <summary>
/// Aggregated usage summary for a time period.
/// </summary>
public class UsageSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The organization ID this summary is for (for billing rollup).
    /// </summary>
    public int? OrganizationId { get; set; }

    /// <summary>
    /// The dealer/tenant ID this summary is for.
    /// </summary>
    public int DealerId { get; set; }

    /// <summary>
    /// Alias for DealerId, for new multi-tenant code.
    /// </summary>
    public int TenantId { get => DealerId; set => DealerId = value; }

    /// <summary>
    /// The type of metric being summarized.
    /// </summary>
    public MetricType MetricType { get; set; }

    /// <summary>
    /// Start of the period.
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End of the period.
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Granularity of the period (Hourly, Daily, Monthly).
    /// </summary>
    public PeriodGranularity Granularity { get; set; }

    /// <summary>
    /// Total value for the period.
    /// </summary>
    public decimal TotalValue { get; set; }

    /// <summary>
    /// Number of records aggregated.
    /// </summary>
    public int RecordCount { get; set; }

    /// <summary>
    /// Minimum value in the period.
    /// </summary>
    public decimal MinValue { get; set; }

    /// <summary>
    /// Maximum value in the period.
    /// </summary>
    public decimal MaxValue { get; set; }

    /// <summary>
    /// Average value for the period.
    /// </summary>
    public decimal AverageValue { get; set; }

    /// <summary>
    /// Unit of measurement.
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// When this summary was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Granularity of usage summaries.
/// </summary>
public enum PeriodGranularity
{
    /// <summary>
    /// Hourly aggregation.
    /// </summary>
    Hourly,

    /// <summary>
    /// Daily aggregation.
    /// </summary>
    Daily,

    /// <summary>
    /// Weekly aggregation.
    /// </summary>
    Weekly,

    /// <summary>
    /// Monthly aggregation.
    /// </summary>
    Monthly
}

/// <summary>
/// Usage report for a dealer over a time period.
/// </summary>
public record UsageReport
{
    public int DealerId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public IReadOnlyList<MetricSummary> Metrics { get; init; } = Array.Empty<MetricSummary>();
    public decimal EstimatedCost { get; init; }
}

/// <summary>
/// Summary of a specific metric.
/// </summary>
public record MetricSummary
{
    public MetricType MetricType { get; init; }
    public decimal TotalValue { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
    public decimal TotalCost { get; init; }
    public IReadOnlyList<DailyUsage> DailyBreakdown { get; init; } = Array.Empty<DailyUsage>();
}

/// <summary>
/// Daily usage breakdown.
/// </summary>
public record DailyUsage
{
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
}
