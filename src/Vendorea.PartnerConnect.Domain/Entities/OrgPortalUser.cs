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

    /// <summary>
    /// PBKDF2 password hash (salt embedded), produced by PortalPasswordHasher. Empty while the user is
    /// <see cref="OrgPortalUserStatus.Invited"/> — no password is set (or emailed) until the user
    /// activates their account via the invitation link and chooses their own password.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Human-friendly display name shown in the portal.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The portal role granted to this user.</summary>
    public OrgPortalRole Role { get; set; } = OrgPortalRole.Viewer;

    /// <summary>
    /// Lifecycle state. New users start <see cref="OrgPortalUserStatus.Invited"/> (no password) until
    /// they activate via the invitation link; <see cref="OrgPortalUserStatus.Disabled"/> blocks login.
    /// </summary>
    public OrgPortalUserStatus Status { get; set; } = OrgPortalUserStatus.Invited;

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

/// <summary>
/// Lifecycle state of an <see cref="OrgPortalUser"/>. Users are created <see cref="Invited"/> with no
/// password and become <see cref="Active"/> only after activating via their invitation link.
/// </summary>
public enum OrgPortalUserStatus
{
    /// <summary>Created but not yet activated: has no password and cannot log in.</summary>
    Invited = 0,

    /// <summary>Activated with a password; can log in normally.</summary>
    Active = 1,

    /// <summary>Deactivated: cannot log in.</summary>
    Disabled = 2
}

/// <summary>
/// A single-use, short-lived, secure token issued to an <see cref="OrgPortalUser"/> for account
/// activation or password reset. Only the SHA-256 hash of the raw token is stored — the raw token
/// lives only in the emailed link, so a leaked database row can't be used to activate/reset.
/// </summary>
public class OrgPortalUserToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user this token belongs to.</summary>
    public Guid OrgPortalUserId { get; set; }

    /// <summary>SHA-256 (lowercase hex) of the raw token. The raw token is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>What the token authorizes.</summary>
    public OrgPortalUserTokenPurpose Purpose { get; set; }

    /// <summary>UTC instant after which the token is no longer valid.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set once the token has been redeemed; a used token cannot be reused.</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the owning user (optional; loaded on hash lookup).</summary>
    public OrgPortalUser? OrgPortalUser { get; set; }
}

/// <summary>What an <see cref="OrgPortalUserToken"/> authorizes.</summary>
public enum OrgPortalUserTokenPurpose
{
    /// <summary>First-time account activation (set the initial password).</summary>
    Activation = 0,

    /// <summary>Reset a forgotten password (reused later, Phase 6).</summary>
    PasswordReset = 1
}
