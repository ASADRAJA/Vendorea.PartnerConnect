namespace Vendorea.PartnerConnect.Domain.StateMachine;

/// <summary>
/// Represents a transition trigger in the document state machine.
/// </summary>
public enum DocumentTrigger
{
    // Standard progression
    StartValidation,
    ValidationPassed,
    ValidationFailed,
    StartMapping,
    MappingSucceeded,
    MappingFailed,
    Enqueue,
    StartSending,
    SendSucceeded,
    SendFailed,
    AcknowledgmentReceived,
    RejectionReceived,
    Complete,

    // Error handling
    Quarantine,
    RetryFromError,
    ReprocessFromQuarantine,

    // Manual operations
    Cancel,
    ForceComplete,
    Reset
}

/// <summary>
/// Represents a state transition in the document lifecycle.
/// </summary>
public class DocumentTransition
{
    public DocumentState FromState { get; init; }
    public DocumentState ToState { get; init; }
    public DocumentTrigger Trigger { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// Guard condition that must be true for the transition to occur.
    /// </summary>
    public Func<DocumentTransitionContext, bool>? Guard { get; init; }

    /// <summary>
    /// Action to execute when the transition occurs.
    /// </summary>
    public Action<DocumentTransitionContext>? OnTransition { get; init; }

    public DocumentTransition(
        DocumentState from,
        DocumentState to,
        DocumentTrigger trigger,
        string? description = null)
    {
        FromState = from;
        ToState = to;
        Trigger = trigger;
        Description = description;
    }
}

/// <summary>
/// Context provided during state transitions.
/// </summary>
public class DocumentTransitionContext
{
    public required int DocumentId { get; init; }
    public required DocumentState CurrentState { get; init; }
    public required DocumentState TargetState { get; init; }
    public required DocumentTrigger Trigger { get; init; }
    public string? Reason { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; } = new();
}

/// <summary>
/// Represents the result of a state transition attempt.
/// </summary>
public class TransitionResult
{
    public bool Success { get; init; }
    public DocumentState? NewState { get; init; }
    public DocumentState PreviousState { get; init; }
    public string? ErrorMessage { get; init; }

    public static TransitionResult Succeeded(DocumentState from, DocumentState to)
        => new() { Success = true, PreviousState = from, NewState = to };

    public static TransitionResult Failed(DocumentState currentState, string error)
        => new() { Success = false, PreviousState = currentState, ErrorMessage = error };
}

/// <summary>
/// Record of a state transition for audit purposes.
/// </summary>
public class DocumentStateHistoryEntry
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public DocumentState FromState { get; set; }
    public DocumentState ToState { get; set; }
    public DocumentTrigger Trigger { get; set; }
    public string? Reason { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Metadata { get; set; }
}
