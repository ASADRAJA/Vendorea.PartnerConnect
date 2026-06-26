namespace Vendorea.PartnerConnect.AdminPortal.Models;

/// <summary>Admin Portal user as returned by the API.</summary>
public class PortalUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "ReadOnly";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class CreatePortalUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "ReadOnly";
    public string? DisplayName { get; set; }
}

public class UpdatePortalUserRequest
{
    public string Role { get; set; } = "ReadOnly";
    public bool IsActive { get; set; } = true;
    public string? DisplayName { get; set; }
}

/// <summary>The three portal roles, used to populate dropdowns.</summary>
public static class PortalRoles
{
    public const string Admin = "Admin";
    public const string Support = "Support";
    public const string ReadOnly = "ReadOnly";

    public static readonly string[] All = { Admin, Support, ReadOnly };
}
