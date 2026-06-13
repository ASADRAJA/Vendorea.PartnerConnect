namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents an end user/tenant within an organization.
/// Tenants place orders and receive data. Each tenant belongs to exactly one organization.
/// </summary>
public class Tenant
{
    public int Id { get; set; }

    /// <summary>
    /// Parent organization ID.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Unique tenant code within the organization.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tenant status.
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>
    /// Whether this is the default tenant for a single-tenant organization.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Contact first name (captured on the connection that created/updated this tenant).
    /// </summary>
    public string? ContactFirstName { get; set; }

    /// <summary>
    /// Contact last name.
    /// </summary>
    public string? ContactLastName { get; set; }

    /// <summary>
    /// Primary contact email.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Primary contact phone.
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// External reference ID (e.g., M360 dealer ID).
    /// </summary>
    public string? ExternalId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Organization? Organization { get; set; }
    public ICollection<TenantPartnerAccount> PartnerAccounts { get; set; } = new List<TenantPartnerAccount>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

/// <summary>
/// Status of a tenant.
/// </summary>
public enum TenantStatus
{
    /// <summary>
    /// Active and can use services.
    /// </summary>
    Active,

    /// <summary>
    /// Temporarily suspended.
    /// </summary>
    Suspended,

    /// <summary>
    /// Inactive/disabled.
    /// </summary>
    Inactive
}
