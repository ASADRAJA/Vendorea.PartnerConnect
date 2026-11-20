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

    /// <summary>
    /// Whether this tenant's submitted orders wait for an admin, or go to the supplier on their own.
    /// </summary>
    /// <remarks>
    /// Manual is the default, and deliberately so: Automatic removes the only human gate between
    /// an order arriving and a purchase order reaching a real supplier. A tenant has to be opted
    /// in by an operator, never by omission.
    /// </remarks>
    public OrderProcessingMode OrderProcessingMode { get; set; } = OrderProcessingMode.Manual;

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

/// <summary>
/// How a tenant's submitted orders reach the supplier.
/// </summary>
public enum OrderProcessingMode
{
    /// <summary>
    /// An admin acknowledges the order and then transmits it. Today's behaviour, and the default.
    /// </summary>
    Manual,

    /// <summary>
    /// The order is acknowledged on arrival and its transmission is queued, with no admin step.
    /// </summary>
    Automatic
}
