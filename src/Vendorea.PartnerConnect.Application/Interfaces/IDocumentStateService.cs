using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for managing document state transitions.
/// </summary>
public interface IDocumentStateService
{
    /// <summary>
    /// Transitions a document to a new state via the specified trigger.
    /// </summary>
    Task<StateTransitionResult> TransitionAsync(
        int documentId,
        DocumentTrigger trigger,
        string? reason = null,
        string? performedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a document with full context.
    /// </summary>
    Task<StateTransitionResult> TransitionWithContextAsync(
        int documentId,
        DocumentTrigger trigger,
        DocumentTransitionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the triggers that can be fired from the document's current state.
    /// </summary>
    Task<IReadOnlyList<DocumentTrigger>> GetPermittedTriggersAsync(
        int documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of a document.
    /// </summary>
    Task<DocumentState?> GetCurrentStateAsync(
        int documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the state transition history for a document.
    /// </summary>
    Task<IReadOnlyList<DocumentStateHistory>> GetHistoryAsync(
        int documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries a document from an error state.
    /// </summary>
    Task<StateTransitionResult> RetryFromErrorAsync(
        int documentId,
        string? performedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Quarantines a document with the specified reason.
    /// </summary>
    Task<StateTransitionResult> QuarantineAsync(
        int documentId,
        QuarantineReason reason,
        string? errorCode = null,
        string? errorMessage = null,
        string? errorDetails = null,
        CancellationToken cancellationToken = default);
}
