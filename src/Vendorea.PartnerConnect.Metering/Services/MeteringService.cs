using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Metering.Interfaces;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Metering.Services;

/// <summary>
/// Implementation of the metering service with buffering.
/// </summary>
public class MeteringService : IMeteringService, IDisposable
{
    private readonly IUsageRepository _repository;
    private readonly MeteringConfiguration _config;
    private readonly ILogger<MeteringService> _logger;
    private readonly ConcurrentQueue<UsageRecord> _buffer = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly Timer _flushTimer;
    private bool _disposed;

    public MeteringService(
        IUsageRepository repository,
        IOptions<MeteringConfiguration> config,
        ILogger<MeteringService> logger)
    {
        _repository = repository;
        _config = config.Value;
        _logger = logger;

        // Setup automatic flush timer
        _flushTimer = new Timer(
            async _ => await FlushAsync(default),
            null,
            TimeSpan.FromSeconds(_config.FlushIntervalSeconds),
            TimeSpan.FromSeconds(_config.FlushIntervalSeconds));
    }

    /// <inheritdoc />
    public Task RecordAsync(
        int dealerId,
        MetricType metricType,
        decimal value,
        string unit,
        string? resourceId = null,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            return Task.CompletedTask;
        }

        var record = new UsageRecord
        {
            DealerId = dealerId,
            MetricType = metricType,
            Value = value,
            Unit = unit,
            ResourceId = resourceId,
            Metadata = metadata,
            Timestamp = DateTime.UtcNow
        };

        _buffer.Enqueue(record);

        // Auto-flush if buffer is full
        if (_buffer.Count >= _config.MaxBufferSize)
        {
            _ = FlushAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordDocumentProcessedAsync(
        int dealerId,
        string documentId,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return RecordAsync(
            dealerId,
            MetricType.DocumentProcessed,
            1,
            "documents",
            documentId,
            metadata,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordApiCallAsync(
        int dealerId,
        string endpoint,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return RecordAsync(
            dealerId,
            MetricType.ApiCall,
            1,
            "calls",
            endpoint,
            metadata,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordStorageUsedAsync(
        int dealerId,
        long bytes,
        string? resourceId = null,
        CancellationToken cancellationToken = default)
    {
        return RecordAsync(
            dealerId,
            MetricType.StorageUsed,
            bytes,
            "bytes",
            resourceId,
            null,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task RecordWebhookDeliveryAsync(
        int dealerId,
        string webhookId,
        CancellationToken cancellationToken = default)
    {
        return RecordAsync(
            dealerId,
            MetricType.WebhookDelivery,
            1,
            "deliveries",
            webhookId,
            null,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UsageRecord>> GetUsageRecordsAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        MetricType? metricType = null,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByDealerAsync(dealerId, startDate, endDate, metricType, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UsageSummary>> GetUsageSummariesAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        PeriodGranularity granularity,
        MetricType? metricType = null,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetSummariesAsync(dealerId, startDate, endDate, granularity, metricType, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UsageReport> GenerateUsageReportAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var summaries = await _repository.GetSummariesAsync(
            dealerId,
            startDate,
            endDate,
            PeriodGranularity.Daily,
            null,
            cancellationToken);

        var metricGroups = summaries
            .GroupBy(s => s.MetricType)
            .Select(g => CreateMetricSummary(g.Key, g.ToList()))
            .ToList();

        var totalCost = metricGroups.Sum(m => m.TotalCost);

        return new UsageReport
        {
            DealerId = dealerId,
            StartDate = startDate,
            EndDate = endDate,
            Metrics = metricGroups,
            EstimatedCost = totalCost
        };
    }

    /// <inheritdoc />
    public async Task<UsageReport> GetCurrentPeriodUsageAsync(
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return await GenerateUsageReportAsync(dealerId, startOfMonth, now, cancellationToken);
    }

    /// <inheritdoc />
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!await _flushLock.WaitAsync(0, cancellationToken))
        {
            // Another flush is in progress
            return;
        }

        try
        {
            var records = new List<UsageRecord>();

            while (_buffer.TryDequeue(out var record))
            {
                records.Add(record);
            }

            if (records.Count > 0)
            {
                await _repository.AddBatchAsync(records, cancellationToken);
                _logger.LogDebug("Flushed {Count} usage records", records.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing usage records");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private MetricSummary CreateMetricSummary(MetricType metricType, List<UsageSummary> summaries)
    {
        var totalValue = summaries.Sum(s => s.TotalValue);
        var unit = summaries.FirstOrDefault()?.Unit ?? "units";

        // Get pricing if configured
        var metricKey = metricType.ToString();
        _config.Pricing.TryGetValue(metricKey, out var pricing);

        var unitCost = pricing?.UnitCost ?? 0m;
        var freeTierLimit = pricing?.FreeTierLimit ?? 0m;
        var billableValue = Math.Max(0, totalValue - freeTierLimit);
        var totalCost = billableValue * unitCost;

        var dailyBreakdown = summaries
            .OrderBy(s => s.PeriodStart)
            .Select(s => new DailyUsage
            {
                Date = s.PeriodStart.Date,
                Value = s.TotalValue
            })
            .ToList();

        return new MetricSummary
        {
            MetricType = metricType,
            TotalValue = totalValue,
            Unit = unit,
            UnitCost = unitCost,
            TotalCost = totalCost,
            DailyBreakdown = dailyBreakdown
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _flushTimer?.Dispose();
        _flushLock?.Dispose();
        _disposed = true;
    }
}
