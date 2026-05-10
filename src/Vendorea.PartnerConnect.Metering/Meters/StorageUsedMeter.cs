using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Metering.Interfaces;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Metering.Meters;

/// <summary>
/// Meter for tracking storage usage.
/// Records storage consumption by dealers for documents and files.
/// </summary>
public class StorageUsedMeter : IStorageUsedMeter
{
    private readonly IMeteringService _meteringService;
    private readonly ILogger<StorageUsedMeter> _logger;

    public StorageUsedMeter(
        IMeteringService meteringService,
        ILogger<StorageUsedMeter> logger)
    {
        _meteringService = meteringService;
        _logger = logger;
    }

    /// <summary>
    /// Records storage usage when a file is stored.
    /// </summary>
    public async Task RecordStoredAsync(
        int dealerId,
        string resourceId,
        long bytes,
        string storageType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = BuildMetadata(storageType, "stored");

            await _meteringService.RecordStorageUsedAsync(
                dealerId,
                bytes,
                resourceId,
                cancellationToken);

            _logger.LogDebug(
                "Recorded storage used: Dealer={DealerId}, Resource={ResourceId}, Bytes={Bytes}, Type={Type}",
                dealerId, resourceId, bytes, storageType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record storage metric for dealer {DealerId}", dealerId);
        }
    }

    /// <summary>
    /// Records storage freed when a file is deleted.
    /// </summary>
    public async Task RecordDeletedAsync(
        int dealerId,
        string resourceId,
        long bytes,
        string storageType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = BuildMetadata(storageType, "deleted");

            // Record negative bytes to indicate deletion
            await _meteringService.RecordAsync(
                dealerId,
                MetricType.StorageUsed,
                -bytes, // Negative for deletions
                "bytes",
                resourceId,
                metadata,
                cancellationToken);

            _logger.LogDebug(
                "Recorded storage freed: Dealer={DealerId}, Resource={ResourceId}, Bytes={Bytes}, Type={Type}",
                dealerId, resourceId, bytes, storageType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record storage deletion metric for dealer {DealerId}", dealerId);
        }
    }

    /// <summary>
    /// Records a batch of storage operations.
    /// </summary>
    public async Task RecordBatchAsync(
        int dealerId,
        IEnumerable<StorageEvent> events,
        CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            if (evt.Operation == StorageOperation.Store)
            {
                await RecordStoredAsync(dealerId, evt.ResourceId, evt.Bytes, evt.StorageType, cancellationToken);
            }
            else
            {
                await RecordDeletedAsync(dealerId, evt.ResourceId, evt.Bytes, evt.StorageType, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Gets current storage usage for a dealer.
    /// </summary>
    public async Task<StorageUsageStats> GetCurrentUsageAsync(
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        // Get all storage records to calculate current usage
        var records = await _meteringService.GetUsageRecordsAsync(
            dealerId,
            DateTime.MinValue,
            DateTime.UtcNow,
            MetricType.StorageUsed,
            cancellationToken);

        var totalBytes = (long)records.Sum(r => r.Value);

        return new StorageUsageStats
        {
            DealerId = dealerId,
            TotalBytesUsed = Math.Max(0, totalBytes), // Can't be negative
            TotalMegabytesUsed = Math.Max(0, totalBytes / (1024.0 * 1024.0)),
            TotalGigabytesUsed = Math.Max(0, totalBytes / (1024.0 * 1024.0 * 1024.0)),
            FileCount = records.Count(r => r.Value > 0),
            ByStorageType = records
                .GroupBy(r => ExtractStorageType(r.Metadata))
                .ToDictionary(
                    g => g.Key,
                    g => (long)Math.Max(0, g.Sum(r => r.Value)))
        };
    }

    /// <summary>
    /// Gets storage usage history for a dealer.
    /// </summary>
    public async Task<IReadOnlyList<StorageUsagePoint>> GetUsageHistoryAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        PeriodGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var summaries = await _meteringService.GetUsageSummariesAsync(
            dealerId,
            startDate,
            endDate,
            granularity,
            MetricType.StorageUsed,
            cancellationToken);

        var points = new List<StorageUsagePoint>();
        long runningTotal = 0;

        foreach (var summary in summaries.OrderBy(s => s.PeriodStart))
        {
            runningTotal += (long)summary.TotalValue;
            points.Add(new StorageUsagePoint
            {
                Timestamp = summary.PeriodStart,
                BytesUsed = Math.Max(0, runningTotal),
                DeltaBytes = (long)summary.TotalValue
            });
        }

        return points;
    }

    /// <summary>
    /// Checks if dealer is within storage quota.
    /// </summary>
    public async Task<StorageQuotaStatus> CheckQuotaAsync(
        int dealerId,
        long quotaBytes,
        CancellationToken cancellationToken = default)
    {
        var usage = await GetCurrentUsageAsync(dealerId, cancellationToken);

        var percentUsed = quotaBytes > 0 ? (usage.TotalBytesUsed * 100.0 / quotaBytes) : 0;

        return new StorageQuotaStatus
        {
            DealerId = dealerId,
            QuotaBytes = quotaBytes,
            UsedBytes = usage.TotalBytesUsed,
            RemainingBytes = Math.Max(0, quotaBytes - usage.TotalBytesUsed),
            PercentUsed = percentUsed,
            IsOverQuota = usage.TotalBytesUsed > quotaBytes,
            IsNearQuota = percentUsed >= 80 && percentUsed < 100
        };
    }

    private static string BuildMetadata(string storageType, string operation)
    {
        return $"{{\"storageType\":\"{storageType}\",\"operation\":\"{operation}\"}}";
    }

    private static string ExtractStorageType(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return "Unknown";
        }

        var match = System.Text.RegularExpressions.Regex.Match(metadata, "\"storageType\":\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }
}

/// <summary>
/// Interface for storage usage metering.
/// </summary>
public interface IStorageUsedMeter
{
    Task RecordStoredAsync(
        int dealerId,
        string resourceId,
        long bytes,
        string storageType,
        CancellationToken cancellationToken = default);

    Task RecordDeletedAsync(
        int dealerId,
        string resourceId,
        long bytes,
        string storageType,
        CancellationToken cancellationToken = default);

    Task RecordBatchAsync(
        int dealerId,
        IEnumerable<StorageEvent> events,
        CancellationToken cancellationToken = default);

    Task<StorageUsageStats> GetCurrentUsageAsync(
        int dealerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StorageUsagePoint>> GetUsageHistoryAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        PeriodGranularity granularity,
        CancellationToken cancellationToken = default);

    Task<StorageQuotaStatus> CheckQuotaAsync(
        int dealerId,
        long quotaBytes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Storage event for batch recording.
/// </summary>
public class StorageEvent
{
    public string ResourceId { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public string StorageType { get; set; } = string.Empty;
    public StorageOperation Operation { get; set; }
}

/// <summary>
/// Storage operation type.
/// </summary>
public enum StorageOperation
{
    Store,
    Delete
}

/// <summary>
/// Current storage usage statistics.
/// </summary>
public class StorageUsageStats
{
    public int DealerId { get; set; }
    public long TotalBytesUsed { get; set; }
    public double TotalMegabytesUsed { get; set; }
    public double TotalGigabytesUsed { get; set; }
    public int FileCount { get; set; }
    public Dictionary<string, long> ByStorageType { get; set; } = new();
}

/// <summary>
/// Point in storage usage history.
/// </summary>
public class StorageUsagePoint
{
    public DateTime Timestamp { get; set; }
    public long BytesUsed { get; set; }
    public long DeltaBytes { get; set; }
}

/// <summary>
/// Storage quota status for a dealer.
/// </summary>
public class StorageQuotaStatus
{
    public int DealerId { get; set; }
    public long QuotaBytes { get; set; }
    public long UsedBytes { get; set; }
    public long RemainingBytes { get; set; }
    public double PercentUsed { get; set; }
    public bool IsOverQuota { get; set; }
    public bool IsNearQuota { get; set; }
}
