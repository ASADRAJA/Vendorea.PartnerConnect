using Cronos;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <summary>
/// Thin wrapper over Cronos for evaluating cron expressions in a given time zone. Supports both
/// 5-field (standard) and 6-field (with seconds) expressions.
/// </summary>
public static class CronSchedule
{
    public static (bool Ok, string? Error) Validate(string cronExpression)
    {
        try
        {
            Parse(cronExpression);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Next occurrence strictly after <paramref name="fromUtc"/>, as UTC; null if none.</summary>
    public static DateTime? ComputeNext(string cronExpression, string timeZoneId, DateTime fromUtc)
    {
        var expr = Parse(cronExpression);
        var tz = ResolveTimeZone(timeZoneId);
        var from = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
        return expr.GetNextOccurrence(from, tz);
    }

    /// <summary>The next <paramref name="count"/> occurrences after <paramref name="fromUtc"/> (UTC).</summary>
    public static IReadOnlyList<DateTime> Preview(string cronExpression, string timeZoneId, int count, DateTime fromUtc)
    {
        var expr = Parse(cronExpression);
        var tz = ResolveTimeZone(timeZoneId);
        var results = new List<DateTime>();
        var cursor = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
        {
            var next = expr.GetNextOccurrence(cursor, tz);
            if (next is null) break;
            results.Add(next.Value);
            cursor = next.Value;
        }
        return results;
    }

    private static CronExpression Parse(string cronExpression)
    {
        var fields = (cronExpression ?? string.Empty)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length == 6
            ? CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds)
            : CronExpression.Parse(cronExpression, CronFormat.Standard);
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase)
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
}
