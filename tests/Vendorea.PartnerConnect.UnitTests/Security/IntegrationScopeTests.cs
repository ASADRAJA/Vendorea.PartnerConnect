using FluentAssertions;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Security;

/// <summary>Scope registration and key matching for the integration scopes. Matrix 1.6.</summary>
public class IntegrationScopeTests
{
    [Fact]
    public void Both_scopes_are_registered_in_the_known_list()
    {
        // A scope absent from AllScopes cannot be granted to an API key at all.
        ApiScopes.AllScopes.Should().Contain(ApiScopes.IntegrationsRead);
        ApiScopes.AllScopes.Should().Contain(ApiScopes.IntegrationsWrite);
    }

    [Fact]
    public void Both_scopes_are_in_the_organization_default_set()
    {
        ApiScopes.OrganizationDefault.Should().Contain(ApiScopes.IntegrationsRead);
        ApiScopes.OrganizationDefault.Should().Contain(ApiScopes.IntegrationsWrite);
    }

    [Fact]
    public void An_api_key_with_wildcard_satisfies_the_integration_scopes()
    {
        var key = new ApiKey { Scopes = new List<string> { ApiScopes.All } };

        key.HasScope(ApiScopes.IntegrationsRead).Should().BeTrue();
        key.HasScope(ApiScopes.IntegrationsWrite).Should().BeTrue();
    }

    [Fact]
    public void A_read_only_key_does_not_satisfy_write()
    {
        var key = new ApiKey { Scopes = new List<string> { ApiScopes.IntegrationsRead } };

        key.HasScope(ApiScopes.IntegrationsRead).Should().BeTrue();
        key.HasScope(ApiScopes.IntegrationsWrite).Should().BeFalse();
    }

    [Fact]
    public void The_write_scope_follows_the_colon_write_convention()
    {
        // Load-bearing: Viewer filtering keys off this suffix, so a scope named otherwise would
        // silently be granted to read-only users.
        ApiScopes.IntegrationsWrite.Should().EndWith(":write");
    }
}
