namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a user in the system.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// External identity provider ID (e.g., Azure AD object ID).
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// User's email address (unique).
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// User's first name.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// User's last name.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// The dealer this user belongs to (null for system admins).
    /// </summary>
    public int? DealerId { get; set; }

    /// <summary>
    /// User status.
    /// </summary>
    public UserStatus Status { get; set; } = UserStatus.Active;

    /// <summary>
    /// When the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When the user last logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Number of failed login attempts.
    /// </summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// When the user account is locked until.
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// User preferences stored as JSON.
    /// </summary>
    public string? Preferences { get; set; }

    /// <summary>
    /// Navigation property to roles assigned to this user.
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    /// <summary>
    /// Gets all permission codes for this user across all roles.
    /// </summary>
    public IEnumerable<string> GetPermissions()
    {
        return UserRoles
            .Where(ur => ur.Role.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct();
    }

    /// <summary>
    /// Checks if the user has a specific permission.
    /// </summary>
    public bool HasPermission(string permissionCode)
    {
        return UserRoles
            .Where(ur => ur.Role.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Any(rp => rp.Permission.Code == permissionCode ||
                       rp.Permission.Code == PermissionCodes.AdminFull);
    }

    /// <summary>
    /// Checks if the user has any of the specified permissions.
    /// </summary>
    public bool HasAnyPermission(params string[] permissionCodes)
    {
        return permissionCodes.Any(HasPermission);
    }

    /// <summary>
    /// Checks if the user is in a specific role.
    /// </summary>
    public bool IsInRole(string roleCode)
    {
        return UserRoles.Any(ur => ur.Role.Code == roleCode && ur.Role.IsActive);
    }
}

/// <summary>
/// Join entity for User-Role many-to-many relationship.
/// </summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>
    /// When this role was assigned to the user.
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who assigned this role.
    /// </summary>
    public string? AssignedBy { get; set; }

    /// <summary>
    /// When this role assignment expires (null for no expiration).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// User status enumeration.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// User account is active and can log in.
    /// </summary>
    Active = 0,

    /// <summary>
    /// User account is inactive and cannot log in.
    /// </summary>
    Inactive = 1,

    /// <summary>
    /// User account is temporarily suspended.
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// User account is locked due to failed login attempts.
    /// </summary>
    Locked = 3,

    /// <summary>
    /// User account is pending verification.
    /// </summary>
    PendingVerification = 4
}
