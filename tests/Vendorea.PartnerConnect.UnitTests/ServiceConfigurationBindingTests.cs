using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Infrastructure.Amazon;
using Vendorea.PartnerConnect.Infrastructure.DependencyInjection;
using Vendorea.PartnerConnect.Infrastructure.Services;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests;

/// <summary>
/// Proves the shared registration extension actually binds its options from the host's
/// configuration.
/// </summary>
/// <remarks>
/// This exists because it did not. The extension took an optional <c>IConfiguration</c> with a
/// null fallback that registered empty options, and both hosts called it without one — so every
/// option bound there was empty in production, and the only symptom was a feature behaving as
/// though it had never been configured. Nothing failed, nothing logged, and no test noticed.
/// </remarks>
public class ServiceConfigurationBindingTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPartnerConnectServices(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Amazon_LWA_settings_reach_the_options_class()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            ["Amazon:Lwa:ApplicationId"] = "amzn1.application.abc",
            ["Amazon:Lwa:ClientId"] = "amzn1.application-oa2-client.abc",
            ["Amazon:Lwa:ClientSecret"] = "a-secret",
            ["Amazon:Lwa:RedirectUri"] = "https://pc.example.com/api/v1/integrations/callback/AMAZON_SP",
            ["Amazon:Lwa:DraftApplication"] = "true"
        });

        var options = provider.GetRequiredService<IOptions<AmazonLwaOptions>>().Value;

        options.ApplicationId.Should().Be("amzn1.application.abc");
        options.RedirectUri.Should().Be("https://pc.example.com/api/v1/integrations/callback/AMAZON_SP");
        options.DraftApplication.Should().BeTrue();
        options.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void The_return_url_allow_list_reaches_the_options_class()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            ["Integrations:Authorization:AllowedReturnUrls:0"] = "https://app.merchant360.com/integrations"
        });

        var options = provider.GetRequiredService<IOptions<IntegrationAuthorizationOptions>>().Value;

        options.AllowedReturnUrls.Should().ContainSingle();
        options.IsReturnUrlAllowed("https://app.merchant360.com/integrations/amazon").Should().BeTrue();
    }

    [Fact]
    public void Email_settings_reach_the_options_class()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            ["Email:From"] = "noreply@example.com",
            ["Email:Port"] = "587"
        });

        var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;

        options.From.Should().Be("noreply@example.com");
        options.Port.Should().Be(587);
    }

    [Fact]
    public void A_host_that_defines_no_sections_still_gets_usable_defaults()
    {
        using var provider = Build(new Dictionary<string, string?>());

        // Absent configuration must leave the defaults, not produce a null options object.
        provider.GetRequiredService<IOptions<AmazonLwaOptions>>().Value
            .TokenEndpoint.Should().Be("https://api.amazon.com/auth/o2/token");
        provider.GetRequiredService<IOptions<AmazonLwaOptions>>().Value
            .IsConfigured.Should().BeFalse();
        provider.GetRequiredService<IOptions<IntegrationAuthorizationOptions>>().Value
            .AllowedReturnUrls.Should().BeEmpty();
    }
}
