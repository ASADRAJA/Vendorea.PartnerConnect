using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Metering.Interfaces;

/// <summary>
/// Service for recording and querying usage metrics.
/// </summary>
public interface IMeteringService
{
    /// <summary>
    /// Records a usage event.
    /// </summary>
    Task RecordAsync(
        int dealerId,
        MetricType metricType,
        decimal value,
        string unit,
        string? resourceId = null,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a document processed event.
    /// </summary>
    Task RecordDocumentProcessedAsync(
        int dealerId,
        string documentId,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an API call event.
    /// </summary>
    Task RecordApiCallAsync(
        int dealerId,
        string endpoint,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records storage usage.
    /// </summary>
    Task RecordStorageUsedAsync(
        int dealerId,
        long bytes,
        string? resourceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a webhook delivery event.
    /// </summary>
    Task RecordWebhookDeliveryAsync(
        int dealerId,
        string webhookId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage records for a dealer within a time range.
    /// </summary>
    Task<IReadOnlyList<UsageRecord>> GetUsageRecordsAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        MetricType? metricType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage summaries for a dealer.
    /// </summary>
    Task<IReadOnlyList<UsageSummary>> GetUsageSummariesAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        PeriodGranularity granularity,
        MetricType? metricType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a usage report for a dealer.
    /// </summary>
    Task<UsageReport> GenerateUsageReportAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current month-to-date usage for a dealer.
    /// </summary>
    Task<UsageReport> GetCurrentPeriodUsageAsync(
        int dealerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any buffered metrics to storage.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
