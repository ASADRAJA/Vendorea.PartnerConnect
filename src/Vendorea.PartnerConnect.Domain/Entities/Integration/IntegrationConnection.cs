namespace Vendorea.PartnerConnect.Domain.Entities.Integration;

/// <summary>
/// One authorised external account, owned by exactly one tenant.
/// </summary>
/// <remarks>
/// A tenant may hold several: two Amazon seller accounts, or the same seller across regions.
/// <see cref="TenantPartnerAccount"/> cannot express that — it allows one row per
/// (org, tenant, partner) and carries an approval workflow that means something different.
///
/// <see cref="TenantId"/> is non-nullable on purpose. Tenant isolation is then a schema
/// invariant rather than a convention every query has to remember.
///
/// Identity for reconnection is <see cref="ExternalAccountId"/>, not this row's id. Reauthorising
/// an account already connected for this tenant must reuse the existing row — a new row would
/// lose the watermark, re-import every order, and defeat the uniqueness that keeps one Amazon
/// order mapped to one Merchant360 sales order.
/// </remarks>
public class IntegrationConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int OrganizationId { get; set; }

    /// <summary>Owning tenant. Required — this is the isolation key.</summary>
    public int TenantId { get; set; }

    public int IntegrationProviderId { get; set; }

    /// <summary>Operator-facing name, e.g. "Acme EU seller account".</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The provider's own account identifier (Amazon selling partner id). Null until the
    /// authorisation callback returns it, which is why uniqueness is enforced by a filtered index.
    /// </summary>
    public string? ExternalAccountId { get; set; }

    public string? Region { get; set; }

    /// <summary>Marketplace identifiers this connection covers, as a JSON array.</summary>
    public string? MarketplaceIdsJson { get; set; }

    public IntegrationConnectionStatus Status { get; set; } = IntegrationConnectionStatus.Pending;

    /// <summary>
    /// What the provider actually granted — not what the provider could offer. A seller may
    /// authorise orders but withhold finances, and "connected" must not imply either.
    /// </summary>
    public IntegrationCapabilities GrantedCapabilities { get; set; }

    /// <summary>Non-secret per-connection settings, as JSON.</summary>
    public string? ConfigJson { get; set; }

    public DateTime? LastErrorAt { get; set; }
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Organization? Organization { get; set; }
    public Tenant? Tenant { get; set; }
    public IntegrationProvider? Provider { get; set; }

    /// <summary>
    /// Secrets live on their own rows so the default read path cannot load them by accident.
    /// </summary>
    public ICollection<IntegrationCredential> Credentials { get; set; } = new List<IntegrationCredential>();

    public ICollection<IntegrationSyncState> SyncStates { get; set; } = new List<IntegrationSyncState>();
}

public enum IntegrationConnectionStatus
{
    /// <summary>Created; authorisation not yet completed. Has no external account id.</summary>
    Pending,
    Connected,
    Expired,
    Revoked,
    Suspended,
    Error
}
