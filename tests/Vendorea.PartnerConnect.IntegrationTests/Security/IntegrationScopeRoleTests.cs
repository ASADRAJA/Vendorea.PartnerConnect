using FluentAssertions;
using Vendorea.PartnerConnect.Api.Authentication;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.IntegrationTests.Security;

/// <summary>
/// The new integration scopes must inherit the existing role behaviour rather than need rules of
/// their own. Matrix 1.6, 1.7. Lives here because ScopesForRole is in the API project.
/// </summary>
public class IntegrationScopeRoleTests
{
    [Fact]
    public void OrgAdmin_gets_both_integration_scopes()
    {
        var scopes = OrgUserTokenService.ScopesForRole(OrgPortalRole.OrgAdmin).ToList();

        scopes.Should().Contain(ApiScopes.IntegrationsRead);
        scopes.Should().Contain(ApiScopes.IntegrationsWrite);
    }

    [Fact]
    public void TenantManager_gets_both_integration_scopes()
    {
        var scopes = OrgUserTokenService.ScopesForRole(OrgPortalRole.TenantManager).ToList();

        scopes.Should().Contain(ApiScopes.IntegrationsRead);
        scopes.Should().Contain(ApiScopes.IntegrationsWrite);
    }

    [Fact]
    public void Viewer_can_read_but_not_write()
    {
        // No role logic was changed for these scopes. Viewer loses the write scope purely because
        // ScopesForRole drops anything ending ":write" - which is why that convention is worth
        // keeping rather than naming scopes ad hoc.
        var scopes = OrgUserTokenService.ScopesForRole(OrgPortalRole.Viewer).ToList();

        scopes.Should().Contain(ApiScopes.IntegrationsRead);
        scopes.Should().NotContain(ApiScopes.IntegrationsWrite);
    }

    [Fact]
    public void Viewer_loses_every_write_scope_not_just_the_integration_one()
    {
        var scopes = OrgUserTokenService.ScopesForRole(OrgPortalRole.Viewer).ToList();

        scopes.Should().NotContain(s => s.EndsWith(":write"));
        scopes.Should().NotContain(ApiScopes.OrgAdmin);
    }
}
