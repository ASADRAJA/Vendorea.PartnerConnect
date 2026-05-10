namespace Vendorea.PartnerConnect.Domain.StateMachine;

/// <summary>
/// Defines the document lifecycle state machine with all valid transitions.
/// This is the source of truth for document state progression rules.
/// </summary>
public static class DocumentLifecycle
{
    private static readonly List<DocumentTransition> _transitions;
    private static readonly Dictionary<DocumentState, DocumentStateCategory> _stateCategories;
    private static readonly HashSet<DocumentState> _terminalStates;
    private static readonly HashSet<DocumentState> _errorStates;
    private static readonly HashSet<DocumentState> _retryableStates;

    static DocumentLifecycle()
    {
        _transitions = BuildTransitions();
        _stateCategories = BuildStateCategories();
        _terminalStates = new HashSet<DocumentState>
        {
            DocumentState.Completed,
            DocumentState.Cancelled,
            DocumentState.Rejected
        };
        _errorStates = new HashSet<DocumentState>
        {
            DocumentState.ValidationFailed,
            DocumentState.MapError,
            DocumentState.SendError,
            DocumentState.Quarantined
        };
        _retryableStates = new HashSet<DocumentState>
        {
            DocumentState.ValidationFailed,
            DocumentState.MapError,
            DocumentState.SendError
        };
    }

    /// <summary>
    /// Gets all defined transitions.
    /// </summary>
    public static IReadOnlyList<DocumentTransition> Transitions => _transitions.AsReadOnly();

    /// <summary>
    /// Gets whether a state is terminal (no further transitions possible in normal flow).
    /// </summary>
    public static bool IsTerminal(DocumentState state) => _terminalStates.Contains(state);

    /// <summary>
    /// Gets whether a state is an error state.
    /// </summary>
    public static bool IsError(DocumentState state) => _errorStates.Contains(state);

    /// <summary>
    /// Gets whether a state can be retried.
    /// </summary>
    public static bool IsRetryable(DocumentState state) => _retryableStates.Contains(state);

    /// <summary>
    /// Gets the category of a state.
    /// </summary>
    public static DocumentStateCategory GetCategory(DocumentState state)
        => _stateCategories.GetValueOrDefault(state, DocumentStateCategory.Processing);

    /// <summary>
    /// Gets all valid transitions from a given state.
    /// </summary>
    public static IEnumerable<DocumentTransition> GetTransitionsFrom(DocumentState state)
        => _transitions.Where(t => t.FromState == state);

    /// <summary>
    /// Gets all valid triggers from a given state.
    /// </summary>
    public static IEnumerable<DocumentTrigger> GetValidTriggers(DocumentState state)
        => GetTransitionsFrom(state).Select(t => t.Trigger).Distinct();

    /// <summary>
    /// Determines if a transition is valid.
    /// </summary>
    public static bool CanTransition(DocumentState from, DocumentTrigger trigger)
        => _transitions.Any(t => t.FromState == from && t.Trigger == trigger);

    /// <summary>
    /// Gets the target state for a transition, if valid.
    /// </summary>
    public static DocumentState? GetTargetState(DocumentState from, DocumentTrigger trigger)
        => _transitions.FirstOrDefault(t => t.FromState == from && t.Trigger == trigger)?.ToState;

    /// <summary>
    /// Gets the transition definition for a specific state and trigger.
    /// </summary>
    public static DocumentTransition? GetTransition(DocumentState from, DocumentTrigger trigger)
        => _transitions.FirstOrDefault(t => t.FromState == from && t.Trigger == trigger);

    /// <summary>
    /// Validates a transition and returns an error message if invalid.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateTransition(
        DocumentState from,
        DocumentTrigger trigger,
        DocumentTransitionContext? context = null)
    {
        var transition = GetTransition(from, trigger);

        if (transition == null)
        {
            return (false, $"No transition defined from '{from}' with trigger '{trigger}'");
        }

        if (context != null && transition.Guard != null && !transition.Guard(context))
        {
            return (false, $"Guard condition failed for transition from '{from}' to '{transition.ToState}'");
        }

        return (true, null);
    }

    /// <summary>
    /// Gets the initial state for a new document.
    /// </summary>
    public static DocumentState InitialState => DocumentState.Received;

    /// <summary>
    /// Gets the happy path progression for a document.
    /// </summary>
    public static IReadOnlyList<DocumentState> HappyPath => new[]
    {
        DocumentState.Received,
        DocumentState.Validating,
        DocumentState.Validated,
        DocumentState.Mapping,
        DocumentState.Mapped,
        DocumentState.Queued,
        DocumentState.Sending,
        DocumentState.Sent,
        DocumentState.AwaitingAcknowledgment,
        DocumentState.Acknowledged,
        DocumentState.Completed
    };

    private static List<DocumentTransition> BuildTransitions()
    {
        return new List<DocumentTransition>
        {
            // === Normal Flow ===

            // Received -> Validating
            new(DocumentState.Received, DocumentState.Validating, DocumentTrigger.StartValidation,
                "Begin validation of received document"),

            // Validating -> Validated or ValidationFailed
            new(DocumentState.Validating, DocumentState.Validated, DocumentTrigger.ValidationPassed,
                "Document passed validation"),
            new(DocumentState.Validating, DocumentState.ValidationFailed, DocumentTrigger.ValidationFailed,
                "Document failed validation"),

            // Validated -> Mapping
            new(DocumentState.Validated, DocumentState.Mapping, DocumentTrigger.StartMapping,
                "Begin mapping to canonical format"),

            // Mapping -> Mapped or MapError
            new(DocumentState.Mapping, DocumentState.Mapped, DocumentTrigger.MappingSucceeded,
                "Document mapped successfully"),
            new(DocumentState.Mapping, DocumentState.MapError, DocumentTrigger.MappingFailed,
                "Document mapping failed"),

            // Mapped -> Queued
            new(DocumentState.Mapped, DocumentState.Queued, DocumentTrigger.Enqueue,
                "Document queued for delivery"),

            // Queued -> Sending
            new(DocumentState.Queued, DocumentState.Sending, DocumentTrigger.StartSending,
                "Begin sending document"),

            // Sending -> Sent or SendError
            new(DocumentState.Sending, DocumentState.Sent, DocumentTrigger.SendSucceeded,
                "Document sent successfully"),
            new(DocumentState.Sending, DocumentState.SendError, DocumentTrigger.SendFailed,
                "Document send failed"),

            // Sent -> AwaitingAcknowledgment (for EDI documents)
            new(DocumentState.Sent, DocumentState.AwaitingAcknowledgment, DocumentTrigger.AcknowledgmentReceived,
                "Awaiting acknowledgment from recipient"),

            // Sent -> Completed (for documents that don't require acknowledgment)
            new(DocumentState.Sent, DocumentState.Completed, DocumentTrigger.Complete,
                "Document processing complete"),

            // AwaitingAcknowledgment -> Acknowledged or Rejected
            new(DocumentState.AwaitingAcknowledgment, DocumentState.Acknowledged, DocumentTrigger.AcknowledgmentReceived,
                "Acknowledgment received from recipient"),
            new(DocumentState.AwaitingAcknowledgment, DocumentState.Rejected, DocumentTrigger.RejectionReceived,
                "Document rejected by recipient"),

            // Acknowledged -> Completed
            new(DocumentState.Acknowledged, DocumentState.Completed, DocumentTrigger.Complete,
                "Document lifecycle complete"),

            // === Error Recovery ===

            // ValidationFailed -> Quarantined or retry
            new(DocumentState.ValidationFailed, DocumentState.Quarantined, DocumentTrigger.Quarantine,
                "Move to quarantine for manual review"),
            new(DocumentState.ValidationFailed, DocumentState.Validating, DocumentTrigger.RetryFromError,
                "Retry validation"),

            // MapError -> Quarantined or retry
            new(DocumentState.MapError, DocumentState.Quarantined, DocumentTrigger.Quarantine,
                "Move to quarantine for manual review"),
            new(DocumentState.MapError, DocumentState.Mapping, DocumentTrigger.RetryFromError,
                "Retry mapping"),

            // SendError -> Quarantined or retry
            new(DocumentState.SendError, DocumentState.Quarantined, DocumentTrigger.Quarantine,
                "Move to quarantine for manual review"),
            new(DocumentState.SendError, DocumentState.Queued, DocumentTrigger.RetryFromError,
                "Re-queue for sending"),

            // Quarantined -> Reprocess from start
            new(DocumentState.Quarantined, DocumentState.Received, DocumentTrigger.ReprocessFromQuarantine,
                "Reprocess document from quarantine"),

            // === Manual Operations ===

            // Cancel from various states
            new(DocumentState.Received, DocumentState.Cancelled, DocumentTrigger.Cancel, "Cancel processing"),
            new(DocumentState.Validated, DocumentState.Cancelled, DocumentTrigger.Cancel, "Cancel processing"),
            new(DocumentState.Mapped, DocumentState.Cancelled, DocumentTrigger.Cancel, "Cancel processing"),
            new(DocumentState.Queued, DocumentState.Cancelled, DocumentTrigger.Cancel, "Cancel processing"),
            new(DocumentState.ValidationFailed, DocumentState.Cancelled, DocumentTrigger.Cancel, "Cancel processing"),
            new(DocumentState.MapError, DocumentState.Cancelled, DocumentTrigger.Cancel, "Cancel processing"),
            new(DocumentState.SendError, DocumentState.Cancelled, DocumentTrigger.Cancel, "Cancel processing"),
            new(DocumentState.Quarantined, DocumentState.Cancelled, DocumentTrigger.Cancel, "Cancel from quarantine"),

            // Force complete (admin operation)
            new(DocumentState.SendError, DocumentState.Completed, DocumentTrigger.ForceComplete,
                "Force complete despite error"),
            new(DocumentState.Quarantined, DocumentState.Completed, DocumentTrigger.ForceComplete,
                "Force complete from quarantine"),

            // Reset (admin operation)
            new(DocumentState.Quarantined, DocumentState.Received, DocumentTrigger.Reset,
                "Reset to initial state"),
        };
    }

    private static Dictionary<DocumentState, DocumentStateCategory> BuildStateCategories()
    {
        return new Dictionary<DocumentState, DocumentStateCategory>
        {
            [DocumentState.Received] = DocumentStateCategory.Initial,
            [DocumentState.Validating] = DocumentStateCategory.Processing,
            [DocumentState.Validated] = DocumentStateCategory.Processing,
            [DocumentState.ValidationFailed] = DocumentStateCategory.Error,
            [DocumentState.Mapping] = DocumentStateCategory.Processing,
            [DocumentState.Mapped] = DocumentStateCategory.Processing,
            [DocumentState.MapError] = DocumentStateCategory.Error,
            [DocumentState.Queued] = DocumentStateCategory.Pending,
            [DocumentState.Sending] = DocumentStateCategory.Processing,
            [DocumentState.Sent] = DocumentStateCategory.Processing,
            [DocumentState.SendError] = DocumentStateCategory.Error,
            [DocumentState.AwaitingAcknowledgment] = DocumentStateCategory.Pending,
            [DocumentState.Acknowledged] = DocumentStateCategory.Success,
            [DocumentState.Rejected] = DocumentStateCategory.Failure,
            [DocumentState.Quarantined] = DocumentStateCategory.Error,
            [DocumentState.Cancelled] = DocumentStateCategory.Failure,
            [DocumentState.Completed] = DocumentStateCategory.Success,
        };
    }
}
