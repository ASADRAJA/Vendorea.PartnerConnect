using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Metering.Interfaces;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Metering.Services;

/// <summary>
/// Background service for aggregating usage records into summaries.
/// </summary>
public class UsageAggregationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MeteringConfiguration _config;
    private readonly ILogger<UsageAggregationService> _logger;
    private readonly TimeSpan _aggregationInterval = TimeSpan.FromMinutes(5);

    public UsageAggregationService(
        IServiceScopeFactory scopeFactory,
        IOptions<MeteringConfiguration> config,
        ILogger<UsageAggregationService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Metering is disabled, aggregation service will not run");
            return;
        }

        _logger.LogInformation("Usage aggregation service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AggregateRecordsAsync(stoppingToken);
                await CleanupOldDataAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in usage aggregation service");
            }

            await Task.Delay(_aggregationInterval, stoppingToken);
        }

        _logger.LogInformation("Usage aggregation service stopped");
    }

    private async Task AggregateRecordsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUsageRepository>();

        // Get records older than 1 hour that haven't been aggregated
        var cutoffTime = DateTime.UtcNow.AddHours(-1);
        var records = await repository.GetPendingAggregationAsync(cutoffTime, 10000, cancellationToken);

        if (records.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Aggregating {Count} usage records", records.Count);

        // Group by dealer, metric type, and hour
        var groups = records
            .GroupBy(r => new
            {
                r.DealerId,
                r.MetricType,
                Hour = new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0, DateTimeKind.Utc)
            });

        foreach (var group in groups)
        {
            var groupRecords = group.ToList();

            var summary = new UsageSummary
            {
                DealerId = group.Key.DealerId,
                MetricType = group.Key.MetricType,
                PeriodStart = group.Key.Hour,
                PeriodEnd = group.Key.Hour.AddHours(1),
                Granularity = PeriodGranularity.Hourly,
                TotalValue = groupRecords.Sum(r => r.Value),
                RecordCount = groupRecords.Count,
                MinValue = groupRecords.Min(r => r.Value),
                MaxValue = groupRecords.Max(r => r.Value),
                AverageValue = groupRecords.Average(r => r.Value),
                Unit = groupRecords.First().Unit
            };

            await repository.UpsertSummaryAsync(summary, cancellationToken);
        }

        // Mark records as aggregated
        await repository.MarkAggregatedAsync(records.Select(r => r.Id), cancellationToken);

        _logger.LogInformation("Aggregated {Count} records into hourly summaries", records.Count);

        // Create daily summaries from hourly
        await CreateDailySummariesAsync(repository, records, cancellationToken);
    }

    private async Task CreateDailySummariesAsync(
        IUsageRepository repository,
        IReadOnlyList<UsageRecord> records,
        CancellationToken cancellationToken)
    {
        // Identify affected dates
        var affectedDates = records
            .Select(r => r.Timestamp.Date)
            .Distinct()
            .Where(d => d < DateTime.UtcNow.Date) // Only complete days
            .ToList();

        foreach (var date in affectedDates)
        {
            // Get hourly summaries for this date
            var hourlySummaries = await repository.GetSummariesAsync(
                0, // All dealers
                date,
                date.AddDays(1),
                PeriodGranularity.Hourly,
                null,
                cancellationToken);

            // Group by dealer and metric type
            var dailyGroups = hourlySummaries
                .GroupBy(s => new { s.DealerId, s.MetricType });

            foreach (var group in dailyGroups)
            {
                var groupSummaries = group.ToList();

                var dailySummary = new UsageSummary
                {
                    DealerId = group.Key.DealerId,
                    MetricType = group.Key.MetricType,
                    PeriodStart = date,
                    PeriodEnd = date.AddDays(1),
                    Granularity = PeriodGranularity.Daily,
                    TotalValue = groupSummaries.Sum(s => s.TotalValue),
                    RecordCount = groupSummaries.Sum(s => s.RecordCount),
                    MinValue = groupSummaries.Min(s => s.MinValue),
                    MaxValue = groupSummaries.Max(s => s.MaxValue),
                    AverageValue = groupSummaries.Average(s => s.AverageValue),
                    Unit = groupSummaries.First().Unit
                };

                await repository.UpsertSummaryAsync(dailySummary, cancellationToken);
            }
        }
    }

    private async Task CleanupOldDataAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUsageRepository>();

        // Delete old raw records
        var rawCutoff = DateTime.UtcNow.AddDays(-_config.RawRecordRetentionDays);
        var rawDeleted = await repository.DeleteOldRecordsAsync(rawCutoff, cancellationToken);

        if (rawDeleted > 0)
        {
            _logger.LogInformation("Deleted {Count} old raw usage records", rawDeleted);
        }

        // Delete old hourly summaries
        var hourlyCutoff = DateTime.UtcNow.AddDays(-_config.DailySummaryRetentionDays);
        var hourlyDeleted = await repository.DeleteOldSummariesAsync(
            PeriodGranularity.Hourly,
            hourlyCutoff,
            cancellationToken);

        if (hourlyDeleted > 0)
        {
            _logger.LogInformation("Deleted {Count} old hourly summaries", hourlyDeleted);
        }

        // Delete old daily summaries
        var dailyCutoff = DateTime.UtcNow.AddDays(-_config.MonthlySummaryRetentionDays);
        var dailyDeleted = await repository.DeleteOldSummariesAsync(
            PeriodGranularity.Daily,
            dailyCutoff,
            cancellationToken);

        if (dailyDeleted > 0)
        {
            _logger.LogInformation("Deleted {Count} old daily summaries", dailyDeleted);
        }
    }
}
