namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// A user that can log in to the Admin Portal. Separate from the dealer/API <see cref="User"/>
/// RBAC model: portal users authenticate with a username + password and carry a single portal role.
/// </summary>
public class AdminPortalUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique login name (case-insensitive).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>PBKDF2 password hash (salt embedded), produced by PortalPasswordHasher.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>The portal role granted to this user.</summary>
    public AdminPortalRole Role { get; set; } = AdminPortalRole.ReadOnly;

    /// <summary>Human-friendly display name shown in the portal.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Inactive users cannot log in.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// Admin Portal roles. Higher capability does not strictly contain lower; capabilities are mapped
/// explicitly in the portal (Admin = everything, Support = operate but not configure, ReadOnly = view).
/// </summary>
public enum AdminPortalRole
{
    /// <summary>View-only: cannot modify configs or approve/run anything.</summary>
    ReadOnly = 0,

    /// <summary>Can approve/run/operate (orders, connections, price feeds, content, etc.) but not change configs or manage users.</summary>
    Support = 1,

    /// <summary>Full access including configuration changes and user management.</summary>
    Admin = 2
}
