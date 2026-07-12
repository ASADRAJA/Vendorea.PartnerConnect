namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// A user that can log in to the customer (org) portal with an email + password. Distinct from the
/// organization's API key (which stays for machine/integration access): a portal user authenticates
/// natively and is issued a per-user token. Scoped to a single <see cref="Organization"/> and
/// carries a single <see cref="OrgPortalRole"/> plus a tenant scope (all tenants, or an explicit
/// subset via <see cref="OrgPortalUserTenant"/>).
/// </summary>
public class OrgPortalUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The organization this user belongs to.</summary>
    public int OrganizationId { get; set; }

    /// <summary>Login email. Unique per organization (case-insensitive).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 password hash (salt embedded), produced by PortalPasswordHasher.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Human-friendly display name shown in the portal.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The portal role granted to this user.</summary>
    public OrgPortalRole Role { get; set; } = OrgPortalRole.Viewer;

    /// <summary>
    /// When true, the user can access every tenant under the org. When false, access is limited to
    /// the tenants listed in <see cref="Tenants"/>.
    /// </summary>
    public bool AllTenants { get; set; } = true;

    /// <summary>Inactive users cannot log in.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Consecutive failed login attempts; reset to 0 on a successful login.</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>When set and in the future, login is locked (cooldown after too many failures).</summary>
    public DateTime? LockedUntil { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Navigation to the org (optional; not always loaded).</summary>
    public Organization? Organization { get; set; }

    /// <summary>Explicit tenant scope, used only when <see cref="AllTenants"/> is false.</summary>
    public ICollection<OrgPortalUserTenant> Tenants { get; set; } = new List<OrgPortalUserTenant>();
}

/// <summary>
/// Grants an <see cref="OrgPortalUser"/> access to a single tenant. Only meaningful when the user's
/// <see cref="OrgPortalUser.AllTenants"/> is false.
/// </summary>
public class OrgPortalUserTenant
{
    public Guid OrgPortalUserId { get; set; }

    /// <summary>PC's internal tenant id.</summary>
    public int TenantId { get; set; }

    public OrgPortalUser? OrgPortalUser { get; set; }
}

/// <summary>
/// Customer-portal roles. OrgAdmin manages the org (read + write everything the org key can);
/// TenantManager operates connections/orders (read + write); Viewer is read-only.
/// </summary>
public enum OrgPortalRole
{
    /// <summary>Full org access: read + write, plus (future) user management.</summary>
    OrgAdmin = 0,

    /// <summary>Operate connections/orders for the tenants in scope: read + write.</summary>
    TenantManager = 1,

    /// <summary>View-only.</summary>
    Viewer = 2
}
