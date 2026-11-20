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
/// What PUT /api/admin/tenants/{id} may and may not change.
/// </summary>
/// <remarks>
/// Both fields here turn on the same asymmetry: an omitted value must leave the tenant alone.
/// Callers of this endpoint send partial bodies — a rename carries neither the order-processing
/// mode nor the external id — so reading absence as a default writes over settings nobody
/// touched. For the mode that would mean silently writing Manual on every rename, and a future
/// reordering of the enum would make absence mean Automatic. For the external id it was not
/// hypothetical: the assignment was unconditional, so a rename unlinked the tenant from
/// Merchant360.
///
/// Neither failure announced itself, which is why both are pinned here rather than left to the
/// portal happening to send the right body.
/// </remarks>
public class AdminTenantsUpdateTests
{
    private readonly Mock<ITenantRepository> _tenants = new();

    private AdminTenantsController CreateController() => new(
        _tenants.Object,
        new Mock<IOrganizationRepository>().Object,
        new Mock<ITenantPartnerAccountRepository>().Object,
        new Mock<ITradingPartnerRepository>().Object,
        NullLogger<AdminTenantsController>.Instance);

    private Tenant Existing(OrderProcessingMode mode, string? externalId = "7001")
    {
        var tenant = new Tenant
        {
            Id = 5, Code = "D1", Name = "Dealer", OrderProcessingMode = mode, ExternalId = externalId
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

    // ---- external id: the Merchant360 merchant id ----

    [Fact]
    public async Task An_omitted_external_id_does_not_unlink_the_tenant()
    {
        // The bug this replaces: ExternalId was assigned unconditionally from a nullable field, so
        // a rename with no externalId in the body silently set it to null. Nothing failed - price
        // pushes then reported the tenant "isn't linked to a Merchant360 merchant" and the
        // order-status, shipment and invoice callbacks skipped with a warning.
        var tenant = Existing(OrderProcessingMode.Manual);

        var result = await CreateController().UpdateTenant(
            5, new UpdateTenantRequest { Name = "Renamed" }, default);

        result.Should().BeOfType<NoContentResult>();
        tenant.ExternalId.Should().Be("7001");
        tenant.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task An_empty_external_id_does_not_unlink_the_tenant()
    {
        var tenant = Existing(OrderProcessingMode.Manual);

        await CreateController().UpdateTenant(
            5, new UpdateTenantRequest { Name = "Dealer", ExternalId = "   " }, default);

        tenant.ExternalId.Should().Be("7001");
    }

    [Fact]
    public async Task Resending_the_same_external_id_is_accepted()
    {
        // Any caller still echoing the current value back must keep working.
        var tenant = Existing(OrderProcessingMode.Manual);

        var result = await CreateController().UpdateTenant(
            5, new UpdateTenantRequest { Name = "Dealer", ExternalId = "7001" }, default);

        result.Should().BeOfType<NoContentResult>();
        tenant.ExternalId.Should().Be("7001");
    }

    [Fact]
    public async Task Changing_an_existing_external_id_is_refused_rather_than_ignored()
    {
        // Refused, not silently dropped: re-pointing a tenant would route another merchant's
        // shipments and invoices here, because every PC -> M360 flow resolves this at send time.
        var tenant = Existing(OrderProcessingMode.Manual);

        var result = await CreateController().UpdateTenant(
            5, new UpdateTenantRequest { Name = "Dealer", ExternalId = "9999" }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
        tenant.ExternalId.Should().Be("7001");
        _tenants.Verify(t => t.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_tenant_that_was_never_linked_can_be_given_an_external_id()
    {
        // Linking is not re-pointing: there is nothing in flight to misroute, and without this the
        // only remedy for a tenant created without one is hand-written SQL.
        var tenant = Existing(OrderProcessingMode.Manual, externalId: null);

        var result = await CreateController().UpdateTenant(
            5, new UpdateTenantRequest { Name = "Dealer", ExternalId = "7001" }, default);

        result.Should().BeOfType<NoContentResult>();
        tenant.ExternalId.Should().Be("7001");
    }

    [Fact]
    public async Task A_name_only_update_still_saves_the_name()
    {
        var tenant = Existing(OrderProcessingMode.Automatic);

        await CreateController().UpdateTenant(5, new UpdateTenantRequest { Name = "New Name" }, default);

        tenant.Name.Should().Be("New Name");
        tenant.ExternalId.Should().Be("7001");
        tenant.OrderProcessingMode.Should().Be(OrderProcessingMode.Automatic);
    }
}
