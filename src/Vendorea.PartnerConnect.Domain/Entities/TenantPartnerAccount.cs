namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a tenant's account with a trading partner.
/// A tenant can have multiple accounts with the same partner.
/// This is the link that allows a tenant to place orders with a specific partner.
/// </summary>
public class TenantPartnerAccount
{
    public int Id { get; set; }

    /// <summary>
    /// Tenant that owns this account. Null while the connection is Pending — the tenant is
    /// created/linked on approval (see <see cref="ApprovalStatus"/>).
    /// </summary>
    public int? TenantId { get; set; }

    /// <summary>
    /// Owning organization (set at connection request time; enables filtering and
    /// Pending-before-tenant). Null only for legacy accounts created before the connections workflow.
    /// </summary>
    public int? OrganizationId { get; set; }

    /// <summary>
    /// The tenant's org-side id — the id the org sends on this tenant's orders. Becomes the
    /// tenant's <c>ExternalId</c> when the tenant is created/linked on approval.
    /// </summary>
    public string ExternalTenantId { get; set; } = string.Empty;

    /// <summary>
    /// Trading partner this account is with.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// Account number with the trading partner.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Approval state of the connection request. PC staff confirm the tenant details with the
    /// partner before approving.
    /// </summary>
    public ConnectionApprovalStatus ApprovalStatus { get; set; } = ConnectionApprovalStatus.Pending;

    /// <summary>
    /// Reason captured when a connection is approved or denied.
    /// </summary>
    public string? DecisionReason { get; set; }

    /// <summary>
    /// When the connection was approved or denied.
    /// </summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>
    /// Contact first name entered on the connection (copied to the Tenant on approval).
    /// </summary>
    public string? ContactFirstName { get; set; }

    /// <summary>
    /// Contact last name entered on the connection (copied to the Tenant on approval).
    /// </summary>
    public string? ContactLastName { get; set; }

    /// <summary>
    /// Optional special identifying code provided for partner confirmation.
    /// </summary>
    public string? SpecialIdentifyingCode { get; set; }

    /// <summary>
    /// Free-text notes captured for partner confirmation.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Partner-specific confirmation field values (keyed by field name), as JSON. The field set
    /// comes from the trading partner's tenant-confirmation requirements.
    /// </summary>
    public string? ConfirmationFieldsJson { get; set; }

    /// <summary>
    /// Whether this account is active and can be used for orders.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display name/alias for this account (optional).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Whether this is the default account for this tenant + partner combination.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Partner-specific credentials or configuration as JSON.
    /// </summary>
    public string? CredentialsJson { get; set; }

    /// <summary>
    /// Additional configuration as JSON.
    /// </summary>
    public string? ConfigurationJson { get; set; }

    /// <summary>
    /// When the account was verified/connected.
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// Last time this account was used for an order.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Tenant? Tenant { get; set; }
    public Organization? Organization { get; set; }
    public TradingPartner? TradingPartner { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

/// <summary>
/// Approval state of a tenant-partner connection request.
/// </summary>
public enum ConnectionApprovalStatus
{
    /// <summary>Submitted; awaiting PC staff confirmation with the partner.</summary>
    Pending,

    /// <summary>Approved — the tenant has been created/linked and the connection is active.</summary>
    Approved,

    /// <summary>Denied by PC staff.</summary>
    Denied,

    /// <summary>Cancelled by the merchant while still Pending (request withdrawn before approval).</summary>
    Cancelled,

    /// <summary>Unsubscribed by the merchant after approval (the connection has been disabled).</summary>
    Unsubscribed
}
