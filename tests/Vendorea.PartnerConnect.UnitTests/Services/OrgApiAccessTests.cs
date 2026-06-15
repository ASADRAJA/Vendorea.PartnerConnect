using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Security;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Services;

public class OrgApiKeyAuthenticatorTests
{
    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly OrgApiKeyAuthenticator _sut;

    public OrgApiKeyAuthenticatorTests() => _sut = new OrgApiKeyAuthenticator(_orgRepo.Object);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveActiveOrganization_BlankKey_ReturnsNull(string? key)
    {
        var result = await _sut.ResolveActiveOrganizationAsync(key);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveActiveOrganization_ValidKeyActiveOrg_ReturnsOrg()
    {
        const string key = "secret-key-123";
        var hash = ApiKeyHasher.Hash(key)!;
        _orgRepo.Setup(r => r.GetByPortalApiKeyHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 5, Code = "ORG", Name = "Org", Status = OrganizationStatus.Active });

        var result = await _sut.ResolveActiveOrganizationAsync(key);

        result.Should().NotBeNull();
        result!.Id.Should().Be(5);
    }

    [Fact]
    public async Task ResolveActiveOrganization_SuspendedOrg_ReturnsNull()
    {
        const string key = "secret-key-123";
        var hash = ApiKeyHasher.Hash(key)!;
        _orgRepo.Setup(r => r.GetByPortalApiKeyHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 5, Status = OrganizationStatus.Suspended });

        var result = await _sut.ResolveActiveOrganizationAsync(key);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveActiveOrganization_UnknownKey_ReturnsNull()
    {
        _orgRepo.Setup(r => r.GetByPortalApiKeyHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);

        var result = await _sut.ResolveActiveOrganizationAsync("whatever");

        result.Should().BeNull();
    }
}

public class TenantConnectionServiceTests
{
    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly Mock<ITenantPartnerAccountRepository> _connRepo = new();
    private readonly TenantConnectionService _sut;

    public TenantConnectionServiceTests()
        => _sut = new TenantConnectionService(_orgRepo.Object, _connRepo.Object,
            Mock.Of<ILogger<TenantConnectionService>>());

    private static Organization ActiveOrgWithPartner(int orgId, int partnerId) => new()
    {
        Id = orgId,
        Status = OrganizationStatus.Active,
        Partners = new List<OrganizationPartner>
        {
            new() { OrganizationId = orgId, TradingPartnerId = partnerId }
        }
    };

    private static RequestConnectionInput ValidInput(int partnerId = 1) =>
        new(partnerId, ExternalTenantId: "ext-1", AccountNumber: "ACCT-1");

    [Fact]
    public async Task RequestConnection_ValidWhitelistedPartner_CreatesPending()
    {
        _orgRepo.Setup(r => r.GetByIdWithPartnersAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOrgWithPartner(10, 1));
        _connRepo.Setup(r => r.ConnectionExistsAsync(10, "ext-1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _connRepo.Setup(r => r.AddAsync(It.IsAny<TenantPartnerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPartnerAccount a, CancellationToken _) => { a.Id = 99; return a; });

        var result = await _sut.RequestConnectionAsync(10, ValidInput());

        result.Success.Should().BeTrue();
        result.Connection!.ApprovalStatus.Should().Be(ConnectionApprovalStatus.Pending);
        result.Connection.IsActive.Should().BeFalse();
        result.Connection.OrganizationId.Should().Be(10);
    }

    [Fact]
    public async Task RequestConnection_InactiveOrg_Fails()
    {
        _orgRepo.Setup(r => r.GetByIdWithPartnersAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 10, Status = OrganizationStatus.Suspended });

        var result = await _sut.RequestConnectionAsync(10, ValidInput());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not active");
    }

    [Fact]
    public async Task RequestConnection_PartnerNotWhitelisted_Fails()
    {
        _orgRepo.Setup(r => r.GetByIdWithPartnersAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOrgWithPartner(10, partnerId: 2)); // whitelist has 2, request 1

        var result = await _sut.RequestConnectionAsync(10, ValidInput(partnerId: 1));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not enabled");
    }

    [Fact]
    public async Task RequestConnection_Duplicate_Fails()
    {
        _orgRepo.Setup(r => r.GetByIdWithPartnersAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOrgWithPartner(10, 1));
        _connRepo.Setup(r => r.ConnectionExistsAsync(10, "ext-1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.RequestConnectionAsync(10, ValidInput());

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("already exists");
    }
}
