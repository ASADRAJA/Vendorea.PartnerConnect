using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Metering.Interfaces;
using Vendorea.PartnerConnect.Metering.Models;

namespace Vendorea.PartnerConnect.Metering.Meters;

/// <summary>
/// Meter for tracking document processing events.
/// Records when documents are processed through the system.
/// </summary>
public class DocumentProcessedMeter : IDocumentProcessedMeter
{
    private readonly IMeteringService _meteringService;
    private readonly ILogger<DocumentProcessedMeter> _logger;

    public DocumentProcessedMeter(
        IMeteringService meteringService,
        ILogger<DocumentProcessedMeter> logger)
    {
        _meteringService = meteringService;
        _logger = logger;
    }

    /// <summary>
    /// Records a document processed event.
    /// </summary>
    public async Task RecordAsync(
        int dealerId,
        string documentId,
        string documentType,
        int recordCount,
        long? fileSizeBytes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Record the document processed event
            await _meteringService.RecordDocumentProcessedAsync(
                dealerId,
                documentId,
                BuildMetadata(documentType, recordCount, fileSizeBytes),
                cancellationToken);

            _logger.LogDebug(
                "Recorded document processed: Dealer={DealerId}, Doc={DocumentId}, Type={DocumentType}, Records={RecordCount}",
                dealerId, documentId, documentType, recordCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record document processed metric for dealer {DealerId}", dealerId);
        }
    }

    /// <summary>
    /// Records multiple documents processed in a batch.
    /// </summary>
    public async Task RecordBatchAsync(
        int dealerId,
        IEnumerable<DocumentProcessedEvent> events,
        CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            await RecordAsync(
                dealerId,
                evt.DocumentId,
                evt.DocumentType,
                evt.RecordCount,
                evt.FileSizeBytes,
                cancellationToken);
        }
    }

    /// <summary>
    /// Records a document processing failure.
    /// </summary>
    public async Task RecordFailureAsync(
        int dealerId,
        string documentId,
        string documentType,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = $"{{\"documentType\":\"{documentType}\",\"status\":\"failed\",\"error\":\"{EscapeJson(errorMessage)}\"}}";

            await _meteringService.RecordAsync(
                dealerId,
                MetricType.DocumentProcessed,
                0, // Failed documents count as 0
                "documents",
                documentId,
                metadata,
                cancellationToken);

            _logger.LogDebug(
                "Recorded document failure: Dealer={DealerId}, Doc={DocumentId}, Error={Error}",
                dealerId, documentId, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record document failure metric for dealer {DealerId}", dealerId);
        }
    }

    /// <summary>
    /// Gets document processing statistics for a dealer.
    /// </summary>
    public async Task<DocumentProcessingStats> GetStatsAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var records = await _meteringService.GetUsageRecordsAsync(
            dealerId,
            startDate,
            endDate,
            MetricType.DocumentProcessed,
            cancellationToken);

        return new DocumentProcessingStats
        {
            DealerId = dealerId,
            StartDate = startDate,
            EndDate = endDate,
            TotalDocuments = records.Count,
            TotalRecordsProcessed = (long)records.Sum(r => r.Value),
            ByDocumentType = records
                .GroupBy(r => ExtractDocumentType(r.Metadata))
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private static string BuildMetadata(string documentType, int recordCount, long? fileSizeBytes)
    {
        var parts = new List<string>
        {
            $"\"documentType\":\"{documentType}\"",
            $"\"recordCount\":{recordCount}",
            "\"status\":\"success\""
        };

        if (fileSizeBytes.HasValue)
        {
            parts.Add($"\"fileSizeBytes\":{fileSizeBytes.Value}");
        }

        return "{" + string.Join(",", parts) + "}";
    }

    private static string ExtractDocumentType(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return "Unknown";
        }

        // Simple extraction - in production use proper JSON parsing
        var match = System.Text.RegularExpressions.Regex.Match(metadata, "\"documentType\":\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}

/// <summary>
/// Interface for document processed metering.
/// </summary>
public interface IDocumentProcessedMeter
{
    Task RecordAsync(
        int dealerId,
        string documentId,
        string documentType,
        int recordCount,
        long? fileSizeBytes = null,
        CancellationToken cancellationToken = default);

    Task RecordBatchAsync(
        int dealerId,
        IEnumerable<DocumentProcessedEvent> events,
        CancellationToken cancellationToken = default);

    Task RecordFailureAsync(
        int dealerId,
        string documentId,
        string documentType,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task<DocumentProcessingStats> GetStatsAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Event representing a document processed.
/// </summary>
public class DocumentProcessedEvent
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public long? FileSizeBytes { get; set; }
}

/// <summary>
/// Statistics for document processing.
/// </summary>
public class DocumentProcessingStats
{
    public int DealerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDocuments { get; set; }
    public long TotalRecordsProcessed { get; set; }
    public Dictionary<string, int> ByDocumentType { get; set; } = new();
}
