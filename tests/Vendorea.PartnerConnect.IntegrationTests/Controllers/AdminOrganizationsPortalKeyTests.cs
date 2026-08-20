using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vendorea.PartnerConnect.Api.Controllers.Admin;
using Vendorea.PartnerConnect.Billing.Interfaces;
// Both the controller and Application.Interfaces declare a CreateOrganizationRequest. Alias the
// controller's explicitly - an unqualified reference is ambiguous, and picking the wrong one
// would compile against a type this endpoint never sees.
using CreateOrganizationRequest = Vendorea.PartnerConnect.Api.Controllers.Admin.CreateOrganizationRequest;
using UpdateOrganizationRequest = Vendorea.PartnerConnect.Api.Controllers.Admin.UpdateOrganizationRequest;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Security;
using Vendorea.PartnerConnect.Api.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.IntegrationTests.Controllers;

/// <summary>
/// The two organization portal keys are separate secrets, one per direction.
/// </summary>
/// <remarks>
/// They were previously both derived from a single admin field: the outbound key was encrypted
/// into <see cref="Organization.PortalApiKey"/> and the same plaintext was hashed into
/// <see cref="Organization.PortalApiKeyHash"/> for inbound auth. That forced both directions to
/// share one secret — either side could present the other's credential — and it meant rotating
/// the outbound key silently rewrote the inbound hash, cutting off whoever was calling in.
///
/// These tests pin the behaviour that separates them, including the independent
/// blank-means-keep semantics, since none of this had controller-level coverage before.
/// </remarks>
public class AdminOrganizationsPortalKeyTests
{
    private const string Outbound = "outbound-key-abc";
    private const string Inbound = "inbound-key-xyz";

    private readonly Mock<IOrganizationRepository> _orgs = new();
    private readonly Mock<ICredentialProtector> _protector = new();

    private AdminOrganizationsController CreateController()
    {
        // Reversible stand-in for real encryption: enough to assert the value was protected
        // rather than stored raw, without dragging key material into a unit test.
        _protector.Setup(p => p.Protect(It.IsAny<string?>()))
                  .Returns((string? s) => s is null ? null : "enc:" + s);

        _orgs.Setup(r => r.GenerateNextCodeAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync("ORG001");
        Organization? lastSaved = null;
        _orgs.Setup(r => r.AddAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
             .Callback((Organization o, CancellationToken _) => lastSaved = o)
             .ReturnsAsync((Organization o, CancellationToken _) => o);

        // The create action re-reads the row to build its response DTO. Echo back what was saved.
        _orgs.Setup(r => r.GetByIdWithPartnersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => lastSaved);

        return new AdminOrganizationsController(
            _orgs.Object,
            Mock.Of<ITenantRepository>(),
            Mock.Of<ITradingPartnerRepository>(),
            Mock.Of<IBillingPlanRepository>(),
            Mock.Of<IOrganizationOnboardingService>(),
            _protector.Object,
            NullLogger<AdminOrganizationsController>.Instance);
    }

    [Fact]
    public async Task Create_stores_the_two_keys_from_their_own_fields()
    {
        var controller = CreateController();
        Organization? saved = null;
        _orgs.Setup(r => r.AddAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
             .Callback((Organization o, CancellationToken _) => saved = o)
             .ReturnsAsync((Organization o, CancellationToken _) => o);
        _orgs.Setup(r => r.GetByIdWithPartnersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => saved);

        await controller.CreateOrganization(new CreateOrganizationRequest
        {
            Name = "Acme",
            ExternalPortalEnabled = true,
            PortalBaseUrl = "https://acme.example.com",
            PortalApiKey = Outbound,
            InboundApiKey = Inbound
        }, CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.PortalApiKey.Should().Be("enc:" + Outbound, "the outbound key is encrypted, not hashed");
        saved.PortalApiKeyHash.Should().Be(ApiKeyHasher.Hash(Inbound), "the inbound key is hashed");

        // The heart of it: the hash must not be derivable from the outbound key.
        saved.PortalApiKeyHash.Should().NotBe(ApiKeyHasher.Hash(Outbound));
    }

    [Fact]
    public async Task Create_with_the_portal_disabled_stores_neither_key()
    {
        var controller = CreateController();
        Organization? saved = null;
        _orgs.Setup(r => r.AddAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
             .Callback((Organization o, CancellationToken _) => saved = o)
             .ReturnsAsync((Organization o, CancellationToken _) => o);
        _orgs.Setup(r => r.GetByIdWithPartnersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => saved);

        await controller.CreateOrganization(new CreateOrganizationRequest
        {
            Name = "Acme",
            ExternalPortalEnabled = false,
            PortalApiKey = Outbound,
            InboundApiKey = Inbound
        }, CancellationToken.None);

        saved!.PortalApiKey.Should().BeNull();
        saved.PortalApiKeyHash.Should().BeNull();
    }

    private Organization ExistingOrg() => new()
    {
        Id = 1,
        Code = "ORG001",
        Name = "Acme",
        Status = OrganizationStatus.Active,
        ExternalPortalEnabled = true,
        PortalBaseUrl = "https://acme.example.com",
        PortalApiKey = "enc:old-outbound",
        PortalApiKeyHash = ApiKeyHasher.Hash("old-inbound")
    };

    [Fact]
    public async Task Rotating_the_outbound_key_leaves_the_inbound_hash_alone()
    {
        // The regression that motivated the split: changing one key used to reset the other,
        // which silently broke whichever integration depended on it.
        var controller = CreateController();
        var org = ExistingOrg();
        _orgs.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(org);

        await controller.UpdateOrganization(1, new UpdateOrganizationRequest
        {
            Name = "Acme",
            ExternalPortalEnabled = true,
            PortalBaseUrl = "https://acme.example.com",
            PortalApiKey = "new-outbound",
            InboundApiKey = null
        }, CancellationToken.None);

        org.PortalApiKey.Should().Be("enc:new-outbound");
        org.PortalApiKeyHash.Should().Be(ApiKeyHasher.Hash("old-inbound"), "a blank inbound field keeps the stored hash");
    }

    [Fact]
    public async Task Rotating_the_inbound_key_leaves_the_outbound_key_alone()
    {
        var controller = CreateController();
        var org = ExistingOrg();
        _orgs.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(org);

        await controller.UpdateOrganization(1, new UpdateOrganizationRequest
        {
            Name = "Acme",
            ExternalPortalEnabled = true,
            PortalBaseUrl = "https://acme.example.com",
            PortalApiKey = null,
            InboundApiKey = "new-inbound"
        }, CancellationToken.None);

        org.PortalApiKey.Should().Be("enc:old-outbound", "a blank outbound field keeps the stored key");
        org.PortalApiKeyHash.Should().Be(ApiKeyHasher.Hash("new-inbound"));
    }

    [Fact]
    public async Task Saving_with_both_blank_changes_nothing()
    {
        // Editing an org's name must not disturb its credentials.
        var controller = CreateController();
        var org = ExistingOrg();
        _orgs.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(org);

        await controller.UpdateOrganization(1, new UpdateOrganizationRequest
        {
            Name = "Acme Renamed",
            ExternalPortalEnabled = true,
            PortalBaseUrl = "https://acme.example.com"
        }, CancellationToken.None);

        org.Name.Should().Be("Acme Renamed");
        org.PortalApiKey.Should().Be("enc:old-outbound");
        org.PortalApiKeyHash.Should().Be(ApiKeyHasher.Hash("old-inbound"));
    }

    [Fact]
    public async Task Disabling_the_external_portal_clears_both_keys()
    {
        var controller = CreateController();
        var org = ExistingOrg();
        _orgs.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(org);

        await controller.UpdateOrganization(1, new UpdateOrganizationRequest
        {
            Name = "Acme",
            ExternalPortalEnabled = false
        }, CancellationToken.None);

        org.PortalApiKey.Should().BeNull();
        org.PortalApiKeyHash.Should().BeNull("turning the portal off revokes inbound access too");
    }
}
