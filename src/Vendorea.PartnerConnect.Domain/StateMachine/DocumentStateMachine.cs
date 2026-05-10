namespace Vendorea.PartnerConnect.Domain.StateMachine;

/// <summary>
/// Executes state transitions for documents and maintains transition history.
/// </summary>
public class DocumentStateMachine : IDocumentStateMachine
{
    private readonly List<DocumentStateHistoryEntry> _history = new();

    public int DocumentId { get; }
    public DocumentState CurrentState { get; private set; }
    public IReadOnlyList<DocumentStateHistoryEntry> History => _history.AsReadOnly();

    public DocumentStateMachine(int documentId, DocumentState initialState = DocumentState.Received)
    {
        DocumentId = documentId;
        CurrentState = initialState;
    }

    /// <summary>
    /// Creates a state machine from existing history.
    /// </summary>
    public static DocumentStateMachine FromHistory(
        int documentId,
        DocumentState currentState,
        IEnumerable<DocumentStateHistoryEntry> history)
    {
        var machine = new DocumentStateMachine(documentId, currentState);
        machine._history.AddRange(history);
        return machine;
    }

    /// <summary>
    /// Attempts to fire a trigger to transition to a new state.
    /// </summary>
    public TransitionResult Fire(DocumentTrigger trigger, string? reason = null, string? performedBy = null)
    {
        var context = new DocumentTransitionContext
        {
            DocumentId = DocumentId,
            CurrentState = CurrentState,
            TargetState = DocumentLifecycle.GetTargetState(CurrentState, trigger) ?? CurrentState,
            Trigger = trigger,
            Reason = reason,
            PerformedBy = performedBy
        };

        return FireWithContext(trigger, context);
    }

    /// <summary>
    /// Attempts to fire a trigger with full context.
    /// </summary>
    public TransitionResult FireWithContext(DocumentTrigger trigger, DocumentTransitionContext context)
    {
        var (isValid, error) = DocumentLifecycle.ValidateTransition(CurrentState, trigger, context);

        if (!isValid)
        {
            return TransitionResult.Failed(CurrentState, error!);
        }

        var transition = DocumentLifecycle.GetTransition(CurrentState, trigger)!;
        var previousState = CurrentState;

        // Execute transition action if defined
        transition.OnTransition?.Invoke(context);

        // Update state
        CurrentState = transition.ToState;

        // Record history
        var historyEntry = new DocumentStateHistoryEntry
        {
            DocumentId = DocumentId,
            FromState = previousState,
            ToState = CurrentState,
            Trigger = trigger,
            Reason = context.Reason,
            PerformedBy = context.PerformedBy,
            OccurredAt = context.Timestamp,
            Metadata = context.Metadata.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(context.Metadata)
                : null
        };
        _history.Add(historyEntry);

        return TransitionResult.Succeeded(previousState, CurrentState);
    }

    /// <summary>
    /// Checks if a trigger can be fired from the current state.
    /// </summary>
    public bool CanFire(DocumentTrigger trigger) => DocumentLifecycle.CanTransition(CurrentState, trigger);

    /// <summary>
    /// Gets all triggers that can be fired from the current state.
    /// </summary>
    public IEnumerable<DocumentTrigger> GetPermittedTriggers() => DocumentLifecycle.GetValidTriggers(CurrentState);

    /// <summary>
    /// Gets whether the document is in a terminal state.
    /// </summary>
    public bool IsTerminal => DocumentLifecycle.IsTerminal(CurrentState);

    /// <summary>
    /// Gets whether the document is in an error state.
    /// </summary>
    public bool IsError => DocumentLifecycle.IsError(CurrentState);

    /// <summary>
    /// Gets whether the current state can be retried.
    /// </summary>
    public bool CanRetry => DocumentLifecycle.IsRetryable(CurrentState);

    /// <summary>
    /// Gets the category of the current state.
    /// </summary>
    public DocumentStateCategory StateCategory => DocumentLifecycle.GetCategory(CurrentState);
}

/// <summary>
/// Interface for document state machine operations.
/// </summary>
public interface IDocumentStateMachine
{
    int DocumentId { get; }
    DocumentState CurrentState { get; }
    IReadOnlyList<DocumentStateHistoryEntry> History { get; }

    TransitionResult Fire(DocumentTrigger trigger, string? reason = null, string? performedBy = null);
    TransitionResult FireWithContext(DocumentTrigger trigger, DocumentTransitionContext context);
    bool CanFire(DocumentTrigger trigger);
    IEnumerable<DocumentTrigger> GetPermittedTriggers();

    bool IsTerminal { get; }
    bool IsError { get; }
    bool CanRetry { get; }
    DocumentStateCategory StateCategory { get; }
}
