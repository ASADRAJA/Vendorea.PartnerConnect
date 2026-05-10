namespace Vendorea.PartnerConnect.Domain.StateMachine;

/// <summary>
/// Represents all possible states in the document lifecycle.
/// States follow a progression from initial receipt through final acknowledgment.
/// </summary>
public enum DocumentState
{
    /// <summary>Document has been received but not yet processed.</summary>
    Received = 0,

    /// <summary>Document is being validated.</summary>
    Validating = 10,

    /// <summary>Document has passed validation.</summary>
    Validated = 20,

    /// <summary>Document validation failed.</summary>
    ValidationFailed = 25,

    /// <summary>Document is being mapped to canonical format.</summary>
    Mapping = 30,

    /// <summary>Document has been successfully mapped.</summary>
    Mapped = 40,

    /// <summary>Document mapping failed.</summary>
    MapError = 45,

    /// <summary>Document is queued for delivery.</summary>
    Queued = 50,

    /// <summary>Document is being sent to destination.</summary>
    Sending = 60,

    /// <summary>Document has been sent successfully.</summary>
    Sent = 70,

    /// <summary>Document delivery failed.</summary>
    SendError = 75,

    /// <summary>Awaiting acknowledgment from recipient.</summary>
    AwaitingAcknowledgment = 80,

    /// <summary>Document has been acknowledged by recipient.</summary>
    Acknowledged = 90,

    /// <summary>Document was rejected by recipient.</summary>
    Rejected = 95,

    /// <summary>Document has been moved to quarantine for review.</summary>
    Quarantined = 100,

    /// <summary>Document processing was cancelled.</summary>
    Cancelled = 110,

    /// <summary>Document processing completed (terminal state).</summary>
    Completed = 200
}

/// <summary>
/// Category groupings for document states.
/// </summary>
public enum DocumentStateCategory
{
    /// <summary>Initial states before processing begins.</summary>
    Initial,

    /// <summary>Active processing states.</summary>
    Processing,

    /// <summary>States awaiting external action.</summary>
    Pending,

    /// <summary>Error states requiring attention.</summary>
    Error,

    /// <summary>Terminal success states.</summary>
    Success,

    /// <summary>Terminal failure states.</summary>
    Failure
}
