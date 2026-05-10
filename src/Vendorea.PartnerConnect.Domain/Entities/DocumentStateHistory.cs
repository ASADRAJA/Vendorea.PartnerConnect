using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Records a state transition in the document lifecycle for audit and debugging purposes.
/// </summary>
public class DocumentStateHistory
{
    public int Id { get; set; }
    public int PartnerDocumentId { get; set; }
    public DocumentState FromState { get; set; }
    public DocumentState ToState { get; set; }
    public DocumentTrigger Trigger { get; set; }
    public string? Reason { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Metadata { get; set; }

    // Navigation property
    public PartnerDocument? PartnerDocument { get; set; }

    /// <summary>
    /// Creates a history entry from a state machine history entry.
    /// </summary>
    public static DocumentStateHistory FromStateMachineEntry(DocumentStateHistoryEntry entry)
    {
        return new DocumentStateHistory
        {
            PartnerDocumentId = entry.DocumentId,
            FromState = entry.FromState,
            ToState = entry.ToState,
            Trigger = entry.Trigger,
            Reason = entry.Reason,
            PerformedBy = entry.PerformedBy,
            OccurredAt = entry.OccurredAt,
            Metadata = entry.Metadata
        };
    }
}
