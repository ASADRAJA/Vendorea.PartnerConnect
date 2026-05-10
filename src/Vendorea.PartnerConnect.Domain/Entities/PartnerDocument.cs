using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a trading document exchanged with a partner (price list, inventory feed, PO, invoice, etc.).
/// Tracks document lifecycle from receipt through processing.
/// </summary>
public class PartnerDocument
{
    public int Id { get; set; }
    public int DealerPartnerConnectionId { get; set; }
    public DocumentType DocumentType { get; set; }
    public DocumentDirection Direction { get; set; }
    public DocumentState State { get; set; } = DocumentState.Received;

    /// <summary>
    /// Legacy status property - maps to State for backwards compatibility.
    /// </summary>
    public DocumentStatus Status
    {
        get => MapStateToStatus(State);
        set => State = MapStatusToState(value);
    }

    public string? ExternalReference { get; set; }
    public string? FileName { get; set; }
    public string? StoragePath { get; set; }
    public string? CanonicalStoragePath { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? ContentHash { get; set; }
    public string? ContentType { get; set; }
    public int? RecordCount { get; set; }
    public int? ProcessedCount { get; set; }
    public int? ErrorCount { get; set; }
    public string? ErrorDetails { get; set; }
    public string? LastErrorCode { get; set; }
    public int RetryCount { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? LastStateChangeAt { get; set; }
    public string? CorrelationId { get; set; }
    public string? ParentDocumentId { get; set; }

    // Navigation properties
    public DealerPartnerConnection? DealerPartnerConnection { get; set; }
    public ICollection<DocumentStateHistory> StateHistory { get; set; } = new List<DocumentStateHistory>();
    public QuarantinedDocument? QuarantineEntry { get; set; }

    /// <summary>
    /// Creates a state machine for this document.
    /// </summary>
    public IDocumentStateMachine CreateStateMachine()
    {
        return new DocumentStateMachine(Id, State);
    }

    /// <summary>
    /// Updates the document state from a state machine.
    /// </summary>
    public void UpdateFromStateMachine(IDocumentStateMachine stateMachine)
    {
        if (stateMachine.DocumentId != Id)
        {
            throw new InvalidOperationException("State machine document ID does not match");
        }

        State = stateMachine.CurrentState;
        LastStateChangeAt = DateTime.UtcNow;
    }

    private static DocumentStatus MapStateToStatus(DocumentState state)
    {
        return state switch
        {
            DocumentState.Received => DocumentStatus.Received,
            DocumentState.Validating or DocumentState.Mapping or DocumentState.Sending => DocumentStatus.Processing,
            DocumentState.Validated or DocumentState.Mapped or DocumentState.Sent => DocumentStatus.Processing,
            DocumentState.Queued => DocumentStatus.Queued,
            DocumentState.Completed or DocumentState.Acknowledged => DocumentStatus.Completed,
            DocumentState.Cancelled => DocumentStatus.Cancelled,
            DocumentState.ValidationFailed or DocumentState.MapError or DocumentState.SendError
                or DocumentState.Quarantined or DocumentState.Rejected => DocumentStatus.Failed,
            DocumentState.AwaitingAcknowledgment => DocumentStatus.Processing,
            _ => DocumentStatus.Received
        };
    }

    private static DocumentState MapStatusToState(DocumentStatus status)
    {
        return status switch
        {
            DocumentStatus.Received => DocumentState.Received,
            DocumentStatus.Queued => DocumentState.Queued,
            DocumentStatus.Processing => DocumentState.Validating,
            DocumentStatus.Completed => DocumentState.Completed,
            DocumentStatus.PartiallyCompleted => DocumentState.Completed,
            DocumentStatus.Failed => DocumentState.Quarantined,
            DocumentStatus.Cancelled => DocumentState.Cancelled,
            _ => DocumentState.Received
        };
    }
}

public enum DocumentType
{
    PriceList,
    InventoryFeed,
    ProductCatalog,
    PurchaseOrder,
    PurchaseOrderAcknowledgment,
    AdvanceShipNotice,
    Invoice,
    CreditMemo,
    ReturnAuthorization
}

public enum DocumentDirection
{
    Inbound,
    Outbound
}

public enum DocumentStatus
{
    Received,
    Queued,
    Processing,
    Completed,
    PartiallyCompleted,
    Failed,
    Cancelled
}
