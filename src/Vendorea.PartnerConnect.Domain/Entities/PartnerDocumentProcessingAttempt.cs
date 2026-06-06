namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Records each attempt to process a partner document.
/// Enables retry tracking, error analysis, and processing history.
/// </summary>
public class PartnerDocumentProcessingAttempt
{
    public int Id { get; set; }

    /// <summary>
    /// The document being processed.
    /// </summary>
    public int PartnerDocumentId { get; set; }

    /// <summary>
    /// Attempt number (1-based).
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Processing phase during this attempt.
    /// </summary>
    public ProcessingPhase Phase { get; set; }

    /// <summary>
    /// Result of this attempt.
    /// </summary>
    public ProcessingResult Result { get; set; }

    /// <summary>
    /// When this attempt started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this attempt completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Error code if failed.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Detailed error information (stack trace, inner exceptions).
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Whether this error is retryable.
    /// </summary>
    public bool IsRetryable { get; set; }

    /// <summary>
    /// Worker/processor that handled this attempt.
    /// </summary>
    public string? ProcessorId { get; set; }

    /// <summary>
    /// Machine/instance that processed this attempt.
    /// </summary>
    public string? MachineName { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    public string? MetadataJson { get; set; }

    // Navigation
    public PartnerDocument? PartnerDocument { get; set; }
}

/// <summary>
/// Processing phases for document pipeline.
/// </summary>
public enum ProcessingPhase
{
    /// <summary>Document received, pending processing.</summary>
    Received = 0,

    /// <summary>Validating document structure/schema.</summary>
    Validation = 10,

    /// <summary>Parsing document content.</summary>
    Parsing = 20,

    /// <summary>Transforming to canonical format.</summary>
    Transformation = 30,

    /// <summary>Correlating with existing data.</summary>
    Correlation = 40,

    /// <summary>Persisting to database.</summary>
    Persistence = 50,

    /// <summary>Sending notifications/webhooks.</summary>
    Notification = 60,

    /// <summary>Generating response documents.</summary>
    Response = 70,

    /// <summary>Final completion.</summary>
    Completed = 100
}

/// <summary>
/// Result of a processing attempt.
/// </summary>
public enum ProcessingResult
{
    /// <summary>Processing in progress.</summary>
    InProgress = 0,

    /// <summary>Processing completed successfully.</summary>
    Success = 10,

    /// <summary>Processing completed with warnings.</summary>
    SuccessWithWarnings = 20,

    /// <summary>Processing failed - retryable error.</summary>
    FailedRetryable = 30,

    /// <summary>Processing failed - permanent error.</summary>
    FailedPermanent = 40,

    /// <summary>Processing skipped (duplicate, superseded).</summary>
    Skipped = 50
}
