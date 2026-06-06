namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a billable organization/account holder in PartnerConnect.
/// Organizations can be platforms like M360 (multi-tenant) or individual dealers (single-tenant).
/// </summary>
public class Organization
{
    public int Id { get; set; }

    /// <summary>
    /// Unique organization code (e.g., "M360", "ABC-DEALER").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Organization status.
    /// </summary>
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Pending;

    /// <summary>
    /// Reference to billing plan (e.g., Stripe plan ID or internal plan code).
    /// </summary>
    public string? BillingPlanId { get; set; }

    /// <summary>
    /// Whether this org supports multiple tenants.
    /// If false, a default tenant is created automatically.
    /// </summary>
    public bool IsMultiTenant { get; set; }

    /// <summary>
    /// Primary contact email.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Primary contact phone.
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// Business address line 1.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// City.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// State/Province.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Postal code.
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Country code (ISO 3166-1 alpha-2).
    /// </summary>
    public string Country { get; set; } = "US";

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? SuspensionReason { get; set; }

    // Navigation properties
    public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
}

/// <summary>
/// Status of an organization.
/// </summary>
public enum OrganizationStatus
{
    /// <summary>
    /// Pending activation.
    /// </summary>
    Pending,

    /// <summary>
    /// Active and can use services.
    /// </summary>
    Active,

    /// <summary>
    /// Temporarily suspended (e.g., billing issue).
    /// </summary>
    Suspended,

    /// <summary>
    /// Permanently closed.
    /// </summary>
    Closed
}
