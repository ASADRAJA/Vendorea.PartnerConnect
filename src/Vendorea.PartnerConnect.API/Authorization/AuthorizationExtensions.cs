using Microsoft.AspNetCore.Authorization;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Authorization;

/// <summary>
/// Extension methods for configuring authorization.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds permission-based authorization policies.
    /// </summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IAuthorizationHandler, AnyPermissionHandler>();

        services.AddAuthorizationBuilder()
            // Document policies
            .AddPolicy("CanReadDocuments", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.DocumentsRead)))
            .AddPolicy("CanWriteDocuments", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.DocumentsWrite)))
            .AddPolicy("CanDeleteDocuments", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.DocumentsDelete)))
            .AddPolicy("CanReprocessDocuments", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.DocumentsReprocess)))

            // Partner policies
            .AddPolicy("CanReadPartners", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.PartnersRead)))
            .AddPolicy("CanWritePartners", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.PartnersWrite)))
            .AddPolicy("CanDeletePartners", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.PartnersDelete)))

            // Connection policies
            .AddPolicy("CanReadConnections", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.ConnectionsRead)))
            .AddPolicy("CanWriteConnections", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.ConnectionsWrite)))
            .AddPolicy("CanDeleteConnections", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.ConnectionsDelete)))

            // Webhook policies
            .AddPolicy("CanReadWebhooks", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.WebhooksRead)))
            .AddPolicy("CanWriteWebhooks", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.WebhooksWrite)))
            .AddPolicy("CanDeleteWebhooks", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.WebhooksDelete)))

            // API Key policies
            .AddPolicy("CanReadApiKeys", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.ApiKeysRead)))
            .AddPolicy("CanWriteApiKeys", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.ApiKeysWrite)))
            .AddPolicy("CanDeleteApiKeys", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.ApiKeysDelete)))

            // Quarantine policies
            .AddPolicy("CanReadQuarantine", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.QuarantineRead)))
            .AddPolicy("CanProcessQuarantine", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.QuarantineProcess)))

            // Usage policies
            .AddPolicy("CanReadUsage", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.UsageRead)))
            .AddPolicy("CanExportUsage", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.UsageExport)))

            // Audit policies
            .AddPolicy("CanReadAudit", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.AuditRead)))

            // Admin policies
            .AddPolicy("IsAdmin", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.AdminFull)))
            .AddPolicy("CanManageUsers", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.AdminUsers)))
            .AddPolicy("CanManageRoles", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.AdminRoles)))
            .AddPolicy("CanManageBilling", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.AdminBilling)))
            .AddPolicy("CanManageOnboarding", policy =>
                policy.Requirements.Add(new PermissionRequirement(PermissionCodes.AdminOnboarding)));

        return services;
    }
}

/// <summary>
/// Authorization policy attribute for permissions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
        : base(GetPolicyName(permission))
    {
    }

    private static string GetPolicyName(string permission)
    {
        // Convert permission code to policy name
        // e.g., "documents:read" -> "CanReadDocuments"
        var parts = permission.Split(':');
        if (parts.Length != 2)
            return permission;

        var resource = char.ToUpperInvariant(parts[0][0]) + parts[0][1..];
        var action = char.ToUpperInvariant(parts[1][0]) + parts[1][1..];

        return $"Can{action}{resource}";
    }
}
