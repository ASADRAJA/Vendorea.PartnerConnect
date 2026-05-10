namespace Vendorea.PartnerConnect.Domain.Events;

/// <summary>
/// Event raised when a document is received from a partner.
/// </summary>
public class DocumentReceivedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public int DealerId { get; }
    public int TradingPartnerId { get; }
    public string DocumentType { get; }
    public string? FileName { get; }
    public long FileSizeBytes { get; }

    public DocumentReceivedEvent(
        Guid documentId,
        int dealerId,
        int tradingPartnerId,
        string documentType,
        string? fileName = null,
        long fileSizeBytes = 0)
    {
        DocumentId = documentId;
        DealerId = dealerId;
        TradingPartnerId = tradingPartnerId;
        DocumentType = documentType;
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
    }
}

/// <summary>
/// Event raised when a document passes validation.
/// </summary>
public class DocumentValidatedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public int DealerId { get; }
    public string DocumentType { get; }
    public int ValidationRulesApplied { get; }

    public DocumentValidatedEvent(
        Guid documentId,
        int dealerId,
        string documentType,
        int validationRulesApplied)
    {
        DocumentId = documentId;
        DealerId = dealerId;
        DocumentType = documentType;
        ValidationRulesApplied = validationRulesApplied;
    }
}

/// <summary>
/// Event raised when a document fails validation.
/// </summary>
public class DocumentValidationFailedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public int DealerId { get; }
    public string DocumentType { get; }
    public IReadOnlyList<string> Errors { get; }

    public DocumentValidationFailedEvent(
        Guid documentId,
        int dealerId,
        string documentType,
        IEnumerable<string> errors)
    {
        DocumentId = documentId;
        DealerId = dealerId;
        DocumentType = documentType;
        Errors = errors.ToList().AsReadOnly();
    }
}

/// <summary>
/// Event raised when a document is successfully processed.
/// </summary>
public class DocumentProcessedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public int DealerId { get; }
    public string DocumentType { get; }
    public TimeSpan ProcessingTime { get; }

    public DocumentProcessedEvent(
        Guid documentId,
        int dealerId,
        string documentType,
        TimeSpan processingTime)
    {
        DocumentId = documentId;
        DealerId = dealerId;
        DocumentType = documentType;
        ProcessingTime = processingTime;
    }
}

/// <summary>
/// Event raised when a document fails processing.
/// </summary>
public class DocumentFailedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public int DealerId { get; }
    public string DocumentType { get; }
    public string FailureReason { get; }
    public string? ErrorDetails { get; }
    public bool WasQuarantined { get; }

    public DocumentFailedEvent(
        Guid documentId,
        int dealerId,
        string documentType,
        string failureReason,
        string? errorDetails = null,
        bool wasQuarantined = false)
    {
        DocumentId = documentId;
        DealerId = dealerId;
        DocumentType = documentType;
        FailureReason = failureReason;
        ErrorDetails = errorDetails;
        WasQuarantined = wasQuarantined;
    }
}

/// <summary>
/// Event raised when a document is sent to a destination.
/// </summary>
public class DocumentSentEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public int DealerId { get; }
    public string Destination { get; }
    public string DocumentType { get; }

    public DocumentSentEvent(
        Guid documentId,
        int dealerId,
        string destination,
        string documentType)
    {
        DocumentId = documentId;
        DealerId = dealerId;
        Destination = destination;
        DocumentType = documentType;
    }
}

/// <summary>
/// Event raised when a document is acknowledged by the recipient.
/// </summary>
public class DocumentAcknowledgedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public int DealerId { get; }
    public string AcknowledgementType { get; }
    public string? AcknowledgementId { get; }

    public DocumentAcknowledgedEvent(
        Guid documentId,
        int dealerId,
        string acknowledgementType,
        string? acknowledgementId = null)
    {
        DocumentId = documentId;
        DealerId = dealerId;
        AcknowledgementType = acknowledgementType;
        AcknowledgementId = acknowledgementId;
    }
}

/// <summary>
/// Event raised when a document is quarantined.
/// </summary>
public class DocumentQuarantinedEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public Guid QuarantineId { get; }
    public int DealerId { get; }
    public string Reason { get; }

    public DocumentQuarantinedEvent(
        Guid documentId,
        Guid quarantineId,
        int dealerId,
        string reason)
    {
        DocumentId = documentId;
        QuarantineId = quarantineId;
        DealerId = dealerId;
        Reason = reason;
    }
}

/// <summary>
/// Event raised when a document is released from quarantine.
/// </summary>
public class DocumentReleasedFromQuarantineEvent : DomainEventBase
{
    public Guid DocumentId { get; }
    public Guid QuarantineId { get; }
    public int DealerId { get; }
    public string ReleasedBy { get; }
    public string Action { get; } // "retry" or "discard"

    public DocumentReleasedFromQuarantineEvent(
        Guid documentId,
        Guid quarantineId,
        int dealerId,
        string releasedBy,
        string action)
    {
        DocumentId = documentId;
        QuarantineId = quarantineId;
        DealerId = dealerId;
        ReleasedBy = releasedBy;
        Action = action;
    }
}
