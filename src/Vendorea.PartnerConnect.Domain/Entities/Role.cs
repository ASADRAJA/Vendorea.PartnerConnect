namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a role that can be assigned to users.
/// </summary>
public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique role code (e.g., "admin", "dealer", "operator").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of this role.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this is a system role that cannot be deleted.
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>
    /// Whether this role is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the role was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the role was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property to permissions assigned to this role.
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    /// <summary>
    /// Navigation property to users assigned to this role.
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

/// <summary>
/// Join entity for Role-Permission many-to-many relationship.
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    /// <summary>
    /// When this permission was assigned to the role.
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who assigned this permission.
    /// </summary>
    public string? AssignedBy { get; set; }
}

/// <summary>
/// Standard role codes used in the system.
/// </summary>
public static class RoleCodes
{
    /// <summary>
    /// System administrator with full access.
    /// </summary>
    public const string SystemAdmin = "system_admin";

    /// <summary>
    /// Tenant administrator - can manage their tenant's users and settings.
    /// </summary>
    public const string TenantAdmin = "tenant_admin";

    /// <summary>
    /// Dealer user - standard dealer operations.
    /// </summary>
    public const string Dealer = "dealer";

    /// <summary>
    /// Operator - read-only access for monitoring.
    /// </summary>
    public const string Operator = "operator";

    /// <summary>
    /// External API user - limited API access.
    /// </summary>
    public const string ExternalApi = "external_api";
}
