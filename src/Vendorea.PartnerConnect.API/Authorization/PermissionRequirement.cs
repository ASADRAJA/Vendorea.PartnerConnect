using Microsoft.AspNetCore.Authorization;

namespace Vendorea.PartnerConnect.Api.Authorization;

/// <summary>
/// Represents a permission requirement for authorization.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The permission code required.
    /// </summary>
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

/// <summary>
/// Represents a requirement for any of multiple permissions.
/// </summary>
public class AnyPermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The permissions, any of which satisfies the requirement.
    /// </summary>
    public IReadOnlyList<string> Permissions { get; }

    public AnyPermissionRequirement(params string[] permissions)
    {
        Permissions = permissions;
    }
}
