using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Interface for document processing orchestration.
/// Workers poll for documents and drive them through the pipeline.
/// </summary>
public interface IDocumentProcessingOrchestrator
{
    /// <summary>
    /// Processes pending inbound documents for a partner.
    /// </summary>
    /// <param name="tradingPartnerId">Trading partner to process (null = all partners).</param>
    /// <param name="batchSize">Max documents to process in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result with statistics.</returns>
    Task<DocumentProcessingBatchResult> ProcessInboundDocumentsAsync(
        int? tradingPartnerId = null,
        int batchSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes pending outbound documents (sends to partner).
    /// </summary>
    /// <param name="tradingPartnerId">Trading partner to process (null = all partners).</param>
    /// <param name="batchSize">Max documents to process in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result with statistics.</returns>
    Task<DocumentProcessingBatchResult> ProcessOutboundDocumentsAsync(
        int? tradingPartnerId = null,
        int batchSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries failed documents that are eligible for retry.
    /// </summary>
    /// <param name="maxAttempts">Max retry attempts before giving up.</param>
    /// <param name="batchSize">Max documents to retry in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Retry result with statistics.</returns>
    Task<DocumentRetryBatchResult> RetryFailedDocumentsAsync(
        int maxAttempts = 3,
        int batchSize = 25,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of processing a batch of documents.
/// </summary>
public class DocumentProcessingBatchResult
{
    /// <summary>
    /// Total documents processed.
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Documents completed successfully.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Documents that failed.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Documents skipped (e.g., already processed).
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Individual document results.
    /// </summary>
    public List<DocumentProcessingResult> Results { get; set; } = new();

    /// <summary>
    /// Processing duration in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Timestamp when batch started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Timestamp when batch completed.
    /// </summary>
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Result of processing a single document.
/// </summary>
public class DocumentProcessingResult
{
    /// <summary>
    /// Document ID.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Document type.
    /// </summary>
    public DocumentType DocumentType { get; set; }

    /// <summary>
    /// Whether processing succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Processing duration for this document (ms).
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Business reference extracted.
    /// </summary>
    public string? BusinessReference { get; set; }
}

/// <summary>
/// Result of retrying failed documents.
/// </summary>
public class DocumentRetryBatchResult
{
    /// <summary>
    /// Total documents attempted.
    /// </summary>
    public int TotalAttempted { get; set; }

    /// <summary>
    /// Documents that succeeded on retry.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Documents that failed again.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Documents that exceeded max attempts (gave up).
    /// </summary>
    public int Exhausted { get; set; }

    /// <summary>
    /// Individual retry results.
    /// </summary>
    public List<DocumentRetryResult> Results { get; set; } = new();

    /// <summary>
    /// Processing duration in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// Result of retrying a single document.
/// </summary>
public class DocumentRetryResult
{
    /// <summary>
    /// Document ID.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Current attempt number.
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Whether retry succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether this was the last attempt.
    /// </summary>
    public bool IsExhausted { get; set; }
}
