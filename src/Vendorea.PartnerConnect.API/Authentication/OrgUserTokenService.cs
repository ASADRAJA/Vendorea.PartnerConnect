using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vendorea.PartnerConnect.Api.Authorization;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Authentication;

/// <summary>Issues signed JWTs for authenticated customer-portal (org) users.</summary>
public interface IOrgUserTokenService
{
    /// <summary>
    /// Issues a token for <paramref name="user"/>. <paramref name="accessibleTenantIds"/> is the
    /// resolved tenant scope (null/empty when the user has all-tenant access).
    /// </summary>
    OrgUserToken Issue(OrgPortalUser user, Organization organization, IReadOnlyCollection<int>? accessibleTenantIds);
}

/// <summary>A minted token + its expiry.</summary>
public record OrgUserToken(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Mints HS256 JWTs signed with the shared <see cref="JwtSettings.SigningKey"/> — the same key the
/// API's JWT bearer scheme validates. Tokens carry a <c>token_type=org_portal_user</c> claim so they
/// can never be confused with any other principal, plus scope claims derived from the user's role so
/// the existing <see cref="RequireScopeAttribute"/> authorization works unchanged.
/// </summary>
public class OrgUserTokenService : IOrgUserTokenService
{
    /// <summary>Distinguishes org-portal-user tokens from any other token/principal.</summary>
    public const string TokenTypeClaim = "token_type";
    public const string OrgPortalUserTokenType = "org_portal_user";
    public const string RoleClaim = "role";
    public const string TenantScopeClaim = "tenant_scope";
    public const string TenantScopeAll = "all";

    private readonly JwtSettings _settings;

    public OrgUserTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public OrgUserToken Issue(OrgPortalUser user, Organization organization, IReadOnlyCollection<int>? accessibleTenantIds)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddHours(_settings.OrgUserTokenHours <= 0 ? 8 : _settings.OrgUserTokenHours);

        var tenantScope = user.AllTenants
            ? TenantScopeAll
            : string.Join(',', (accessibleTenantIds ?? Array.Empty<int>()).OrderBy(id => id));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.DisplayName),
            new(TokenTypeClaim, OrgPortalUserTokenType),
            new(ApiPrincipalExtensions.ActorTypeClaim, ApiPrincipalExtensions.OrganizationActor),
            new(ApiPrincipalExtensions.OrgIdClaim, organization.Id.ToString()),
            new(RoleClaim, user.Role.ToString()),
            new(TenantScopeClaim, tenantScope)
        };

        if (!string.IsNullOrWhiteSpace(organization.Code))
            claims.Add(new Claim("org_code", organization.Code));

        // Map the role to API scopes so the org endpoints' [RequireScope] checks pass without change.
        // OrgAdmin/TenantManager get the same surface an org key gets (read + write); Viewer is
        // read-only (write scopes filtered out).
        foreach (var scope in ScopesForRole(user.Role))
            claims.Add(new Claim(ScopeAuthorizationHandler.ScopeClaimType, scope));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new OrgUserToken(token, expires);
    }

    /// <summary>The API scopes granted for a portal role.</summary>
    public static IEnumerable<string> ScopesForRole(OrgPortalRole role)
    {
        // Viewer: read-only — keep only the non-write scopes from the org default set.
        if (role == OrgPortalRole.Viewer)
        {
            return ApiScopes.OrganizationDefault
                .Where(s => !s.EndsWith(":write", StringComparison.OrdinalIgnoreCase)
                            && !s.Equals(ApiScopes.OrdersWrite, StringComparison.OrdinalIgnoreCase));
        }

        // OrgAdmin / TenantManager: same surface as the org API key (read + write).
        return ApiScopes.OrganizationDefault;
    }
}
