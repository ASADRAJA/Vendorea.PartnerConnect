using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for managing document state transitions with persistence and history tracking.
/// </summary>
public class DocumentStateService : IDocumentStateService
{
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly IDocumentStateHistoryRepository _historyRepository;
    private readonly IQuarantineService _quarantineService;
    private readonly ILogger<DocumentStateService> _logger;

    public DocumentStateService(
        IPartnerDocumentRepository documentRepository,
        IDocumentStateHistoryRepository historyRepository,
        IQuarantineService quarantineService,
        ILogger<DocumentStateService> logger)
    {
        _documentRepository = documentRepository;
        _historyRepository = historyRepository;
        _quarantineService = quarantineService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StateTransitionResult> TransitionAsync(
        int documentId,
        DocumentTrigger trigger,
        string? reason = null,
        string? performedBy = null,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document == null)
        {
            return StateTransitionResult.Failed($"Document {documentId} not found");
        }

        var stateMachine = document.CreateStateMachine();

        if (!stateMachine.CanFire(trigger))
        {
            _logger.LogWarning(
                "Invalid state transition for document {DocumentId}: Cannot fire {Trigger} from state {State}",
                documentId, trigger, stateMachine.CurrentState);

            return StateTransitionResult.Failed(
                $"Cannot fire trigger '{trigger}' from state '{stateMachine.CurrentState}'");
        }

        var transitionResult = stateMachine.Fire(trigger, reason, performedBy);

        if (transitionResult.Success)
        {
            document.UpdateFromStateMachine(stateMachine);

            // Persist history entries
            foreach (var entry in stateMachine.History)
            {
                var historyRecord = DocumentStateHistory.FromStateMachineEntry(entry);
                await _historyRepository.AddAsync(historyRecord, cancellationToken);
            }

            await _documentRepository.UpdateAsync(document, cancellationToken);

            _logger.LogInformation(
                "Document {DocumentId} transitioned from {FromState} to {ToState} via {Trigger}",
                documentId, transitionResult.PreviousState, transitionResult.NewState, trigger);

            // Handle quarantine if needed
            if (transitionResult.NewState == DocumentState.Quarantined)
            {
                await HandleQuarantineAsync(document, trigger, reason, cancellationToken);
            }
        }

        return StateTransitionResult.FromTransitionResult(transitionResult);
    }

    /// <inheritdoc />
    public async Task<StateTransitionResult> TransitionWithContextAsync(
        int documentId,
        DocumentTrigger trigger,
        DocumentTransitionContext context,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document == null)
        {
            return StateTransitionResult.Failed($"Document {documentId} not found");
        }

        var stateMachine = document.CreateStateMachine();
        var transitionResult = stateMachine.FireWithContext(trigger, context);

        if (transitionResult.Success)
        {
            document.UpdateFromStateMachine(stateMachine);

            foreach (var entry in stateMachine.History)
            {
                var historyRecord = DocumentStateHistory.FromStateMachineEntry(entry);
                historyRecord.ErrorMessage = context.ErrorMessage;
                await _historyRepository.AddAsync(historyRecord, cancellationToken);
            }

            await _documentRepository.UpdateAsync(document, cancellationToken);

            if (transitionResult.NewState == DocumentState.Quarantined)
            {
                await HandleQuarantineAsync(document, trigger, context.Reason, cancellationToken);
            }
        }

        return StateTransitionResult.FromTransitionResult(transitionResult);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentTrigger>> GetPermittedTriggersAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document == null)
        {
            return Array.Empty<DocumentTrigger>();
        }

        return DocumentLifecycle.GetValidTriggers(document.State).ToList();
    }

    /// <inheritdoc />
    public async Task<DocumentState?> GetCurrentStateAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        return document?.State;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentStateHistory>> GetHistoryAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        return await _historyRepository.GetByDocumentIdAsync(documentId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StateTransitionResult> RetryFromErrorAsync(
        int documentId,
        string? performedBy = null,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document == null)
        {
            return StateTransitionResult.Failed($"Document {documentId} not found");
        }

        if (!DocumentLifecycle.IsRetryable(document.State))
        {
            return StateTransitionResult.Failed(
                $"Document state '{document.State}' is not retryable");
        }

        document.RetryCount++;
        document.ErrorDetails = null;
        document.LastErrorCode = null;

        return await TransitionAsync(
            documentId,
            DocumentTrigger.RetryFromError,
            $"Retry attempt {document.RetryCount}",
            performedBy,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StateTransitionResult> QuarantineAsync(
        int documentId,
        QuarantineReason reason,
        string? errorCode = null,
        string? errorMessage = null,
        string? errorDetails = null,
        CancellationToken cancellationToken = default)
    {
        var result = await TransitionAsync(
            documentId,
            DocumentTrigger.Quarantine,
            errorMessage ?? reason.ToString(),
            cancellationToken: cancellationToken);

        if (result.Success)
        {
            var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
            if (document != null)
            {
                await _quarantineService.QuarantineDocumentAsync(
                    document,
                    reason,
                    errorCode,
                    errorMessage,
                    errorDetails,
                    cancellationToken);
            }
        }

        return result;
    }

    private async Task HandleQuarantineAsync(
        PartnerDocument document,
        DocumentTrigger trigger,
        string? reason,
        CancellationToken cancellationToken)
    {
        var quarantineReason = trigger switch
        {
            DocumentTrigger.Quarantine => QuarantineReason.ManualQuarantine,
            _ => QuarantineReason.ManualQuarantine
        };

        // Check if already quarantined
        var existingQuarantine = await _quarantineService.GetByDocumentIdAsync(
            document.Id, cancellationToken);

        if (existingQuarantine == null)
        {
            await _quarantineService.QuarantineDocumentAsync(
                document,
                quarantineReason,
                document.LastErrorCode,
                reason,
                document.ErrorDetails,
                cancellationToken);
        }
    }
}

/// <summary>
/// Result of a state transition operation.
/// </summary>
public class StateTransitionResult
{
    public bool Success { get; init; }
    public DocumentState? PreviousState { get; init; }
    public DocumentState? NewState { get; init; }
    public string? ErrorMessage { get; init; }

    public static StateTransitionResult Succeeded(DocumentState from, DocumentState to)
        => new() { Success = true, PreviousState = from, NewState = to };

    public static StateTransitionResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };

    public static StateTransitionResult FromTransitionResult(TransitionResult result)
        => new()
        {
            Success = result.Success,
            PreviousState = result.PreviousState,
            NewState = result.NewState,
            ErrorMessage = result.ErrorMessage
        };
}
