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
    /// Tenant that owns this account.
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Trading partner this account is with.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// Account number with the trading partner.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

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
    public TradingPartner? TradingPartner { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
