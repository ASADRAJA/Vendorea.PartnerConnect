namespace Vendorea.PartnerConnect.Domain.Entities.Integration;

/// <summary>
/// How far synchronisation has got, per connection, per capability, per marketplace.
/// </summary>
/// <remarks>
/// All three parts of the key matter. A single LastSyncAt on the connection cannot express a
/// seller whose orders are current while listings lag, across several marketplaces — and a
/// shared watermark would let one capability's progress hide another's failure.
///
/// <see cref="Watermark"/> advances only after every row of a page is persisted. Advancing
/// earlier drops orders silently, which is the failure this field exists to prevent.
/// </remarks>
public class IntegrationSyncState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid IntegrationConnectionId { get; set; }

    public IntegrationCapabilities Capability { get; set; }

    /// <summary>Null where the capability is not marketplace-scoped.</summary>
    public string? MarketplaceId { get; set; }

    /// <summary>
    /// Provider-side position: everything changed at or before this is known to be persisted.
    /// </summary>
    public DateTime? Watermark { get; set; }

    /// <summary>
    /// Provider pagination cursor for a run in progress. Amazon's expires after 24 hours, so a
    /// resumed run must be prepared to restart from a safe overlap window instead of trusting it.
    /// </summary>
    public string? Cursor { get; set; }

    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public IntegrationConnection? Connection { get; set; }
}
