using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vendorea.PartnerConnect.Api.Controllers.Admin;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.IntegrationTests.Controllers;

/// <summary>
/// The tenant Order Processing setting as the admin API sees it.
/// </summary>
/// <remarks>
/// The guarantee worth pinning is the asymmetry: an omitted mode must leave the tenant alone.
/// Every existing caller of PUT /api/admin/tenants/{id} — and the Merchant360-side tooling that
/// syncs tenant names — sends a body with no OrderProcessingMode in it. If an absent field were
/// read as the enum's default, those callers would be silently writing Manual on every update,
/// and worse, a future reordering of the enum would make absence mean Automatic.
/// </remarks>
public class AdminTenantsOrderProcessingTests
{
    private readonly Mock<ITenantRepository> _tenants = new();

    private AdminTenantsController CreateController() => new(
        _tenants.Object,
        new Mock<IOrganizationRepository>().Object,
        new Mock<ITenantPartnerAccountRepository>().Object,
        new Mock<ITradingPartnerRepository>().Object,
        NullLogger<AdminTenantsController>.Instance);

    private Tenant Existing(OrderProcessingMode mode)
    {
        var tenant = new Tenant
        {
            Id = 5, Code = "D1", Name = "Dealer", OrderProcessingMode = mode
        };
        _tenants.Setup(t => t.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        return tenant;
    }

    [Fact]
    public async Task An_omitted_mode_leaves_an_automatic_tenant_automatic()
    {
        // The direction that would be a data-loss bug: a name-only update must not quietly turn
        // automatic dispatch off.
        var tenant = Existing(OrderProcessingMode.Automatic);

        var result = await CreateController().UpdateTenant(
            5, new UpdateTenantRequest { Name = "Renamed" }, default);

        result.Should().BeOfType<NoContentResult>();
        tenant.OrderProcessingMode.Should().Be(OrderProcessingMode.Automatic);
        tenant.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task An_omitted_mode_leaves_a_manual_tenant_manual()
    {
        // The direction that would be a safety bug: absence must never mean Automatic.
        var tenant = Existing(OrderProcessingMode.Manual);

        await CreateController().UpdateTenant(5, new UpdateTenantRequest { Name = "Renamed" }, default);

        tenant.OrderProcessingMode.Should().Be(OrderProcessingMode.Manual);
    }

    [Fact]
    public async Task An_empty_mode_is_treated_as_omitted()
    {
        var tenant = Existing(OrderProcessingMode.Automatic);

        await CreateController().UpdateTenant(
            5, new UpdateTenantRequest { Name = "Dealer", OrderProcessingMode = "   " }, default);

        tenant.OrderProcessingMode.Should().Be(OrderProcessingMode.Automatic);
    }

    [Theory]
    [InlineData("Automatic")]
    [InlineData("automatic")]
    [InlineData("AUTOMATIC")]
    public async Task A_tenant_can_be_switched_to_automatic(string value)
    {
        var tenant = Existing(OrderProcessingMode.Manual);

        var result = await CreateController().UpdateTenant(
            5, new UpdateTenantRequest { Name = "Dealer", OrderProcessingMode = value }, default);

        result.Should().BeOfType<NoContentResult>();
        tenant.OrderProcessingMode.Should().Be(OrderProcessingMode.Automatic);
    }

    [Fact]
    public async Task A_tenant_can_be_switched_back_to_manual()
    {
        var tenant = Existing(OrderProcessingMode.Automatic);

        await CreateController().UpdateTenant(
            5, new UpdateTenantRequest { Name = "Dealer", OrderProcessingMode = "Manual" }, default);

        tenant.OrderProcessingMode.Should().Be(OrderProcessingMode.Manual);
    }

    [Theory]
    [InlineData("Auto")]
    [InlineData("Automated")]
    [InlineData("true")]
    [InlineData("1")]
    public async Task An_unrecognised_mode_is_rejected_rather_than_guessed(string value)
    {
        // "Auto" and "1" are the plausible near-misses. Falling back to a default for either
        // would mean a caller believing it enabled automatic dispatch when it did not, or the
        // reverse.
        var tenant = Existing(OrderProcessingMode.Manual);

        var result = await CreateController().UpdateTenant(
            5, new UpdateTenantRequest { Name = "Dealer", OrderProcessingMode = value }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
        tenant.OrderProcessingMode.Should().Be(OrderProcessingMode.Manual);
        _tenants.Verify(t => t.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_unknown_tenant_is_not_found()
    {
        _tenants.Setup(t => t.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var result = await CreateController().UpdateTenant(
            99, new UpdateTenantRequest { Name = "Dealer" }, default);

        result.Should().BeOfType<NotFoundResult>();
    }
}
