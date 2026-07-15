using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Infrastructure.Services;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Services;

public class SprStockCheckServiceTests
{
    private readonly Mock<ITradingPartnerRepository> _partners = new();
    private readonly Mock<ITenantPartnerAccountRepository> _connections = new();
    private readonly Mock<ICredentialProtector> _protector = new();
    private readonly Mock<ISprInteractiveServices> _spr = new();
    private readonly SprStockCheckService _sut;

    public SprStockCheckServiceTests()
    {
        // Passthrough protector (local-style plaintext creds).
        _protector.Setup(p => p.Unprotect(It.IsAny<string?>())).Returns((string? s) => s);
        var resolver = new SprWebServiceContextResolver(_partners.Object, _connections.Object, _protector.Object);
        _sut = new SprStockCheckService(resolver, _spr.Object);
    }

    private void SprPartnerConfigured()
    {
        var config = new SprConfiguration
        {
            WebServicesBaseUrl = "http://test/sprws/",
            WebServicesGroupCode = "GRP",
            WebServicesUserId = "WebService"
        };
        _partners.Setup(p => p.GetByCodeAsync("SPR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TradingPartner
            {
                Id = 1,
                Code = "SPR",
                TransportConfigJson = config.ToJson(),
                TransportCredentialsJson = JsonSerializer.Serialize(new SprCredentials { WebServicesPassword = "secret" })
            });
    }

    private void HasActiveConnection() =>
        _connections.Setup(c => c.GetConnectionsAsync(10, ConnectionApprovalStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPartnerAccount>
            {
                new()
                {
                    TradingPartnerId = 1,
                    ExternalTenantId = "3",
                    AccountNumber = "ASAD-SPR-001",
                    SpecialIdentifyingCode = "TENANT-GRP-3", // per-tenant SPR web-services group code
                    IsActive = true,
                    ApprovalStatus = ConnectionApprovalStatus.Approved,
                    Tenant = new Tenant { Status = TenantStatus.Active },
                    Organization = new Organization { Status = OrganizationStatus.Active }
                }
            });

    private static StockCheckRequest Request(List<int>? dcs = null) =>
        new() { ExternalTenantId = "3", ItemNumber = "SPRW1011", DcNumbers = dcs };

    [Fact]
    public async Task NotConfigured_WhenPartnerHasNoWebServiceConfig()
    {
        _partners.Setup(p => p.GetByCodeAsync("SPR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TradingPartner { Id = 1, Code = "SPR" }); // no transport config

        var outcome = await _sut.StockCheckAsync(10, Request());

        outcome.Status.Should().Be(StockCheckStatus.NotConfigured);
    }

    [Fact]
    public async Task NoActiveConnection_WhenTenantNotSubscribedToSpr()
    {
        SprPartnerConfigured();
        _connections.Setup(c => c.GetConnectionsAsync(10, ConnectionApprovalStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPartnerAccount>()); // none

        var outcome = await _sut.StockCheckAsync(10, Request());

        outcome.Status.Should().Be(StockCheckStatus.NoActiveConnection);
        _spr.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UsesDealerStockCheck_WhenNoDcsSpecified()
    {
        SprPartnerConfigured();
        HasActiveConnection();
        _spr.Setup(s => s.DealerStockCheckAsync(It.IsAny<SprWebServiceConfig>(), It.IsAny<SprStockCheckQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SprStockCheckResult { Success = true, RtnMessage = "OK", SprItemNumber = "SPRW1011", DealerPrice = 0.99m });

        var outcome = await _sut.StockCheckAsync(10, Request());

        outcome.Status.Should().Be(StockCheckStatus.Ok);
        outcome.Response!.Success.Should().BeTrue();
        outcome.Response.PricingIncluded.Should().BeTrue();
        outcome.Response.DealerPrice.Should().Be(0.99m);
        // GroupCode is the PER-TENANT SPR web-services group code (from the connection's
        // SpecialIdentifyingCode), NOT the partner-level "GRP"; CustNumber is the per-tenant dealer
        // account number; UserId is the shared partner cred.
        _spr.Verify(s => s.DealerStockCheckAsync(
            It.Is<SprWebServiceConfig>(c =>
                c.GroupCode == "TENANT-GRP-3" && c.CustNumber == "ASAD-SPR-001" && c.UserId == "WebService"),
            It.IsAny<SprStockCheckQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GroupCode_FallsBackToAccountNumber_WhenConnectionHasNoGroupCode()
    {
        SprPartnerConfigured(); // partner-level WebServicesGroupCode="GRP" must be IGNORED
        _connections.Setup(c => c.GetConnectionsAsync(10, ConnectionApprovalStatus.Approved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPartnerAccount>
            {
                new()
                {
                    TradingPartnerId = 1,
                    ExternalTenantId = "3",
                    AccountNumber = "ASAD-SPR-001",
                    SpecialIdentifyingCode = null, // no per-tenant group code set
                    IsActive = true,
                    ApprovalStatus = ConnectionApprovalStatus.Approved,
                    Tenant = new Tenant { Status = TenantStatus.Active },
                    Organization = new Organization { Status = OrganizationStatus.Active }
                }
            });
        _spr.Setup(s => s.DealerStockCheckAsync(It.IsAny<SprWebServiceConfig>(), It.IsAny<SprStockCheckQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SprStockCheckResult { Success = true, RtnMessage = "OK", SprItemNumber = "SPRW1011" });

        var outcome = await _sut.StockCheckAsync(10, Request());

        outcome.Status.Should().Be(StockCheckStatus.Ok);
        // No per-tenant group code -> falls back to the account number, and never the partner "GRP".
        _spr.Verify(s => s.DealerStockCheckAsync(
            It.Is<SprWebServiceConfig>(c => c.GroupCode == "ASAD-SPR-001"),
            It.IsAny<SprStockCheckQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UsesQuickCheckPlus_WhenDcsSpecified()
    {
        SprPartnerConfigured();
        HasActiveConnection();
        _spr.Setup(s => s.QuickCheckPlusAsync(It.IsAny<SprWebServiceConfig>(), It.IsAny<SprStockCheckQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SprStockCheckResult { Success = true, RtnStatus = "0000" });

        var outcome = await _sut.StockCheckAsync(10, Request(new List<int> { 1, 16 }));

        outcome.Status.Should().Be(StockCheckStatus.Ok);
        _spr.Verify(s => s.QuickCheckPlusAsync(It.IsAny<SprWebServiceConfig>(), It.IsAny<SprStockCheckQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _spr.Verify(s => s.DealerStockCheckAsync(It.IsAny<SprWebServiceConfig>(), It.IsAny<SprStockCheckQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
