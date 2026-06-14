using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a document that has been quarantined due to processing errors.
/// Contains details about the failure and options for resolution.
/// </summary>
public class QuarantinedDocument
{
    public int Id { get; set; }
    public int PartnerDocumentId { get; set; }

    /// <summary>Trading partner this quarantined document belongs to (converged key).</summary>
    public int TradingPartnerId { get; set; }

    /// <summary>Tenant this document is for, when known.</summary>
    public int? TenantId { get; set; }

    public DocumentState QuarantinedFromState { get; set; }
    public QuarantineReason Reason { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorDetails { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public DateTime QuarantinedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public QuarantineResolution? Resolution { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }

    // Navigation properties
    public PartnerDocument? PartnerDocument { get; set; }

    /// <summary>
    /// Creates a new quarantine entry for a document.
    /// </summary>
    public static QuarantinedDocument Create(
        int documentId,
        int tradingPartnerId,
        DocumentState fromState,
        QuarantineReason reason,
        string? errorCode = null,
        string? errorMessage = null,
        string? errorDetails = null)
    {
        return new QuarantinedDocument
        {
            PartnerDocumentId = documentId,
            TradingPartnerId = tradingPartnerId,
            QuarantinedFromState = fromState,
            Reason = reason,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ErrorDetails = errorDetails,
            RetryCount = 0,
            QuarantinedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Records a retry attempt.
    /// </summary>
    public void RecordRetry()
    {
        RetryCount++;
    }

    /// <summary>
    /// Checks if more retries are allowed.
    /// </summary>
    public bool CanRetry => RetryCount < MaxRetries;

    /// <summary>
    /// Marks the document as reviewed.
    /// </summary>
    public void MarkReviewed(string reviewedBy)
    {
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = reviewedBy;
    }

    /// <summary>
    /// Resolves the quarantine with the specified resolution.
    /// </summary>
    public void Resolve(QuarantineResolution resolution, string resolvedBy, string? notes = null)
    {
        Resolution = resolution;
        ResolvedAt = DateTime.UtcNow;
        ResolvedBy = resolvedBy;
        ResolutionNotes = notes;
    }
}

/// <summary>
/// Reasons for quarantining a document.
/// </summary>
public enum QuarantineReason
{
    /// <summary>Document failed validation rules.</summary>
    ValidationFailed,

    /// <summary>Document could not be mapped to canonical format.</summary>
    MappingFailed,

    /// <summary>Document could not be delivered.</summary>
    DeliveryFailed,

    /// <summary>Document was rejected by the recipient.</summary>
    Rejected,

    /// <summary>Maximum retry attempts exceeded.</summary>
    MaxRetriesExceeded,

    /// <summary>Duplicate document detected.</summary>
    DuplicateDetected,

    /// <summary>Document format is invalid or unrecognized.</summary>
    InvalidFormat,

    /// <summary>Required partner configuration is missing.</summary>
    ConfigurationMissing,

    /// <summary>Manual quarantine by administrator.</summary>
    ManualQuarantine
}

/// <summary>
/// Resolution options for quarantined documents.
/// </summary>
public enum QuarantineResolution
{
    /// <summary>Document was fixed and reprocessed.</summary>
    Reprocessed,

    /// <summary>Document was discarded.</summary>
    Discarded,

    /// <summary>Document was forwarded for manual processing.</summary>
    ManualProcessing,

    /// <summary>Document was marked as a known issue to ignore.</summary>
    Ignored,

    /// <summary>Document was cancelled at user request.</summary>
    Cancelled
}
