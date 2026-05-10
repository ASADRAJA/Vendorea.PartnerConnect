namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a permission that can be assigned to roles.
/// </summary>
public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique permission code (e.g., "documents:read", "partners:write").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this permission allows.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category for grouping permissions (e.g., "Documents", "Partners", "Admin").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to roles that have this permission.
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>
/// Standard permission codes used in the system.
/// </summary>
public static class PermissionCodes
{
    // Document permissions
    public const string DocumentsRead = "documents:read";
    public const string DocumentsWrite = "documents:write";
    public const string DocumentsDelete = "documents:delete";
    public const string DocumentsReprocess = "documents:reprocess";

    // Partner permissions
    public const string PartnersRead = "partners:read";
    public const string PartnersWrite = "partners:write";
    public const string PartnersDelete = "partners:delete";

    // Connection permissions
    public const string ConnectionsRead = "connections:read";
    public const string ConnectionsWrite = "connections:write";
    public const string ConnectionsDelete = "connections:delete";

    // Webhook permissions
    public const string WebhooksRead = "webhooks:read";
    public const string WebhooksWrite = "webhooks:write";
    public const string WebhooksDelete = "webhooks:delete";

    // API Key permissions
    public const string ApiKeysRead = "apikeys:read";
    public const string ApiKeysWrite = "apikeys:write";
    public const string ApiKeysDelete = "apikeys:delete";

    // Quarantine permissions
    public const string QuarantineRead = "quarantine:read";
    public const string QuarantineProcess = "quarantine:process";

    // Usage/Metering permissions
    public const string UsageRead = "usage:read";
    public const string UsageExport = "usage:export";

    // Audit permissions
    public const string AuditRead = "audit:read";

    // Admin permissions
    public const string AdminFull = "admin:full";
    public const string AdminUsers = "admin:users";
    public const string AdminRoles = "admin:roles";
    public const string AdminBilling = "admin:billing";
    public const string AdminOnboarding = "admin:onboarding";
}
