using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Metering.Interfaces;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Metering.Meters;

/// <summary>
/// Meter for tracking API call events.
/// Records API requests made by dealers.
/// </summary>
public class ApiCallMeter : IApiCallMeter
{
    private readonly IMeteringService _meteringService;
    private readonly ILogger<ApiCallMeter> _logger;

    public ApiCallMeter(
        IMeteringService meteringService,
        ILogger<ApiCallMeter> logger)
    {
        _meteringService = meteringService;
        _logger = logger;
    }

    /// <summary>
    /// Records an API call event.
    /// </summary>
    public async Task RecordAsync(
        int dealerId,
        string endpoint,
        string httpMethod,
        int statusCode,
        long durationMs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = BuildMetadata(httpMethod, statusCode, durationMs);

            await _meteringService.RecordApiCallAsync(
                dealerId,
                endpoint,
                metadata,
                cancellationToken);

            _logger.LogDebug(
                "Recorded API call: Dealer={DealerId}, Endpoint={Endpoint}, Method={Method}, Status={Status}, Duration={Duration}ms",
                dealerId, endpoint, httpMethod, statusCode, durationMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record API call metric for dealer {DealerId}", dealerId);
        }
    }

    /// <summary>
    /// Records an API call from request context.
    /// </summary>
    public async Task RecordFromContextAsync(
        int dealerId,
        ApiCallContext context,
        CancellationToken cancellationToken = default)
    {
        await RecordAsync(
            dealerId,
            context.Endpoint,
            context.HttpMethod,
            context.StatusCode,
            context.DurationMs,
            cancellationToken);
    }

    /// <summary>
    /// Gets API call statistics for a dealer.
    /// </summary>
    public async Task<ApiCallStats> GetStatsAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var records = await _meteringService.GetUsageRecordsAsync(
            dealerId,
            startDate,
            endDate,
            MetricType.ApiCall,
            cancellationToken);

        var totalCalls = records.Count;
        var successCalls = records.Count(r => IsSuccess(r.Metadata));
        var errorCalls = totalCalls - successCalls;
        var avgDuration = records.Any() ? records.Average(r => ExtractDuration(r.Metadata)) : 0;

        return new ApiCallStats
        {
            DealerId = dealerId,
            StartDate = startDate,
            EndDate = endDate,
            TotalCalls = totalCalls,
            SuccessfulCalls = successCalls,
            FailedCalls = errorCalls,
            AverageDurationMs = avgDuration,
            ByEndpoint = records
                .GroupBy(r => r.ResourceId ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count()),
            ByHttpMethod = records
                .GroupBy(r => ExtractHttpMethod(r.Metadata))
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <summary>
    /// Gets rate limit status for a dealer.
    /// </summary>
    public async Task<RateLimitStatus> GetRateLimitStatusAsync(
        int dealerId,
        int windowMinutes,
        int maxRequests,
        CancellationToken cancellationToken = default)
    {
        var windowStart = DateTime.UtcNow.AddMinutes(-windowMinutes);
        var records = await _meteringService.GetUsageRecordsAsync(
            dealerId,
            windowStart,
            DateTime.UtcNow,
            MetricType.ApiCall,
            cancellationToken);

        var currentCount = records.Count;
        var remaining = Math.Max(0, maxRequests - currentCount);

        return new RateLimitStatus
        {
            DealerId = dealerId,
            WindowMinutes = windowMinutes,
            MaxRequests = maxRequests,
            CurrentCount = currentCount,
            RemainingRequests = remaining,
            IsLimited = currentCount >= maxRequests,
            WindowResetAt = windowStart.AddMinutes(windowMinutes)
        };
    }

    private static string BuildMetadata(string httpMethod, int statusCode, long durationMs)
    {
        return $"{{\"method\":\"{httpMethod}\",\"statusCode\":{statusCode},\"durationMs\":{durationMs},\"success\":{(statusCode < 400 ? "true" : "false")}}}";
    }

    private static bool IsSuccess(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return true;
        }

        return metadata.Contains("\"success\":true");
    }

    private static double ExtractDuration(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return 0;
        }

        var match = System.Text.RegularExpressions.Regex.Match(metadata, "\"durationMs\":(\\d+)");
        return match.Success && double.TryParse(match.Groups[1].Value, out var duration) ? duration : 0;
    }

    private static string ExtractHttpMethod(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return "Unknown";
        }

        var match = System.Text.RegularExpressions.Regex.Match(metadata, "\"method\":\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }
}

/// <summary>
/// Interface for API call metering.
/// </summary>
public interface IApiCallMeter
{
    Task RecordAsync(
        int dealerId,
        string endpoint,
        string httpMethod,
        int statusCode,
        long durationMs,
        CancellationToken cancellationToken = default);

    Task RecordFromContextAsync(
        int dealerId,
        ApiCallContext context,
        CancellationToken cancellationToken = default);

    Task<ApiCallStats> GetStatsAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<RateLimitStatus> GetRateLimitStatusAsync(
        int dealerId,
        int windowMinutes,
        int maxRequests,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for an API call.
/// </summary>
public class ApiCallContext
{
    public string Endpoint { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>
/// Statistics for API calls.
/// </summary>
public class ApiCallStats
{
    public int DealerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalCalls { get; set; }
    public int SuccessfulCalls { get; set; }
    public int FailedCalls { get; set; }
    public double AverageDurationMs { get; set; }
    public Dictionary<string, int> ByEndpoint { get; set; } = new();
    public Dictionary<string, int> ByHttpMethod { get; set; } = new();
}

/// <summary>
/// Rate limit status for a dealer.
/// </summary>
public class RateLimitStatus
{
    public int DealerId { get; set; }
    public int WindowMinutes { get; set; }
    public int MaxRequests { get; set; }
    public int CurrentCount { get; set; }
    public int RemainingRequests { get; set; }
    public bool IsLimited { get; set; }
    public DateTime WindowResetAt { get; set; }
}
