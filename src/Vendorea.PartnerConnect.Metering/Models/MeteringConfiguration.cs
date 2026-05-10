namespace Vendorea.PartnerConnect.Metering.Models;

/// <summary>
/// Configuration for the metering system.
/// </summary>
public class MeteringConfiguration
{
    public const string SectionName = "Metering";

    /// <summary>
    /// Whether metering is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Interval for flushing metrics buffer (seconds).
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum buffer size before auto-flush.
    /// </summary>
    public int MaxBufferSize { get; set; } = 1000;

    /// <summary>
    /// Days to retain raw usage records.
    /// </summary>
    public int RawRecordRetentionDays { get; set; } = 7;

    /// <summary>
    /// Days to retain daily summaries.
    /// </summary>
    public int DailySummaryRetentionDays { get; set; } = 90;

    /// <summary>
    /// Days to retain monthly summaries.
    /// </summary>
    public int MonthlySummaryRetentionDays { get; set; } = 365;

    /// <summary>
    /// Pricing configuration for each metric type.
    /// </summary>
    public Dictionary<string, MetricPricing> Pricing { get; set; } = new();
}

/// <summary>
/// Pricing configuration for a metric type.
/// </summary>
public class MetricPricing
{
    /// <summary>
    /// Cost per unit.
    /// </summary>
    public decimal UnitCost { get; set; }

    /// <summary>
    /// Free tier limit (units included free).
    /// </summary>
    public decimal FreeTierLimit { get; set; }

    /// <summary>
    /// Description for billing.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
