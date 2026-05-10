using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Authorization;

/// <summary>
/// Handles permission-based authorization.
/// </summary>
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PermissionHandler> _logger;

    public PermissionHandler(
        IServiceProvider serviceProvider,
        ILogger<PermissionHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = GetUserId(context.User);
        if (userId == null)
        {
            _logger.LogDebug("No user ID found in claims");
            return;
        }

        // Check if user has AdminFull permission (superuser)
        if (await HasPermissionAsync(userId.Value, PermissionCodes.AdminFull))
        {
            context.Succeed(requirement);
            return;
        }

        // Check for the specific permission
        if (await HasPermissionAsync(userId.Value, requirement.Permission))
        {
            context.Succeed(requirement);
            return;
        }

        _logger.LogDebug(
            "User {UserId} does not have permission {Permission}",
            userId,
            requirement.Permission);
    }

    private async Task<bool> HasPermissionAsync(Guid userId, string permissionCode)
    {
        using var scope = _serviceProvider.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = await userRepository.GetWithRolesAsync(userId);
        if (user == null)
        {
            return false;
        }

        return user.HasPermission(permissionCode);
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ??
                          user.FindFirst("sub") ??
                          user.FindFirst("user_id");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}

/// <summary>
/// Handles authorization for any of multiple permissions.
/// </summary>
public class AnyPermissionHandler : AuthorizationHandler<AnyPermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnyPermissionHandler> _logger;

    public AnyPermissionHandler(
        IServiceProvider serviceProvider,
        ILogger<AnyPermissionHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyPermissionRequirement requirement)
    {
        var userId = GetUserId(context.User);
        if (userId == null)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = await userRepository.GetWithRolesAsync(userId.Value);
        if (user == null)
        {
            return;
        }

        // Check for AdminFull
        if (user.HasPermission(PermissionCodes.AdminFull))
        {
            context.Succeed(requirement);
            return;
        }

        // Check for any of the required permissions
        if (user.HasAnyPermission(requirement.Permissions.ToArray()))
        {
            context.Succeed(requirement);
            return;
        }

        _logger.LogDebug(
            "User {UserId} does not have any of permissions: {Permissions}",
            userId,
            string.Join(", ", requirement.Permissions));
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ??
                          user.FindFirst("sub") ??
                          user.FindFirst("user_id");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}
