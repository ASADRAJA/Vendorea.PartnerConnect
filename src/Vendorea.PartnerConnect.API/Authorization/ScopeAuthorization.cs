using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Authorization;

/// <summary>
/// Authorization requirement satisfied when the caller's API key carries a given scope claim
/// (or the wildcard "*" scope). This is the API-key authorization model and is intentionally
/// separate from the user/role <see cref="PermissionRequirement"/> system.
/// </summary>
public sealed class ScopeRequirement : IAuthorizationRequirement
{
    public ScopeRequirement(string scope) => Scope = scope;

    public string Scope { get; }
}

/// <summary>Grants a <see cref="ScopeRequirement"/> when the principal has the scope (or "*").</summary>
public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    public const string ScopeClaimType = "scope";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var scopes = context.User.FindAll(ScopeClaimType).Select(c => c.Value);
        var ok = scopes.Any(s =>
            s.Equals(ApiScopes.All, StringComparison.Ordinal) ||
            s.Equals(requirement.Scope, StringComparison.OrdinalIgnoreCase));

        if (ok)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Dynamic policy provider: any policy named "scope:&lt;value&gt;" is materialized on demand as a
/// <see cref="ScopeRequirement"/>. All other policy names (and the default/fallback policies)
/// delegate to the framework default provider.
/// </summary>
public sealed class ScopePolicyProvider : IAuthorizationPolicyProvider
{
    public const string Prefix = "scope:";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public ScopePolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var scope = policyName[Prefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ScopeRequirement(scope))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}

/// <summary>
/// Requires the caller's API key to carry the given scope. Usage: <c>[RequireScope(ApiScopes.OrdersWrite)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireScopeAttribute : AuthorizeAttribute
{
    public RequireScopeAttribute(string scope) : base(ScopePolicyProvider.Prefix + scope) { }
}

/// <summary>Claims helpers for API-key principals.</summary>
public static class ApiPrincipalExtensions
{
    public const string ActorTypeClaim = "actor_type";
    public const string OrganizationActor = "Organization";
    public const string OrgIdClaim = "org_id";

    /// <summary>The authenticated organization id, when the caller authenticated with an org key.</summary>
    public static int? GetOrganizationId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(OrgIdClaim);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>True when the caller authenticated as an organization (vs. a dealer or admin key).</summary>
    public static bool IsOrganization(this ClaimsPrincipal user)
        => string.Equals(user.FindFirst(ActorTypeClaim)?.Value, OrganizationActor, StringComparison.Ordinal);
}
