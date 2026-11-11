namespace Vendorea.PartnerConnect.Domain.Entities.Integration;

/// <summary>
/// An explicit grant letting a second tenant connect an external account another tenant owns.
/// </summary>
/// <remarks>
/// The exception that proves the rule. A seller account maps to one Merchant360 tenant, and
/// nothing in the authorization flow can change that: a delegation is created deliberately, by an
/// administrator, with a named grantor and a recorded reason. No OAuth callback, no org API key
/// and no provider redirect can produce one.
///
/// That matters because the alternative is silent. Without this, a seller who authorises the same
/// Amazon account from two Merchant360 tenants would simply end up with two sets of their orders,
/// two inventory positions and two parties able to read their customers' addresses — and nobody
/// would have decided that should happen.
///
/// A delegation grants the right to hold a connection. It does not share one: the delegate
/// authorises the account itself and receives its own credentials, its own watermark and its own
/// orders. Nothing is read across the tenant boundary.
/// </remarks>
public class IntegrationAccountDelegation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int IntegrationProviderId { get; set; }

    /// <summary>The provider's account identifier the grant is about.</summary>
    public string ExternalAccountId { get; set; } = string.Empty;

    /// <summary>The tenant holding the Primary connection for that account.</summary>
    public int OwnerTenantId { get; set; }

    /// <summary>The tenant permitted to connect it as well.</summary>
    public int DelegateTenantId { get; set; }

    /// <summary>
    /// Who authorised this, as an auditable identity rather than a flag.
    /// </summary>
    /// <remarks>
    /// Required. A delegated-access decision that cannot be attributed to a person is not an
    /// authorization — it is an unexplained exception sitting in a table.
    /// </remarks>
    public string GrantedBy { get; set; } = string.Empty;

    /// <summary>Why the grant exists, in the grantor's words.</summary>
    public string? Reason { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the grant is withdrawn. The row is retained for audit.</summary>
    public DateTime? RevokedAt { get; set; }

    public string? RevokedBy { get; set; }

    public IntegrationProvider? Provider { get; set; }

    public bool IsActive => RevokedAt is null;
}
