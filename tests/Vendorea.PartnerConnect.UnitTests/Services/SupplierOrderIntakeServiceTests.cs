using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Services;

public class SupplierOrderIntakeServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<IOrganizationRepository> _orgRepoMock;
    private readonly Mock<ITenantRepository> _tenantRepoMock;
    private readonly Mock<ITenantPartnerAccountRepository> _accountRepoMock;
    private readonly Mock<IPartnerOrderResolutionService> _resolutionServiceMock;
    private readonly Mock<ILogger<SupplierOrderIntakeService>> _loggerMock;
    private readonly SupplierOrderIntakeService _service;

    public SupplierOrderIntakeServiceTests()
    {
        _orderRepoMock = new Mock<IOrderRepository>();
        _orgRepoMock = new Mock<IOrganizationRepository>();
        _tenantRepoMock = new Mock<ITenantRepository>();
        _accountRepoMock = new Mock<ITenantPartnerAccountRepository>();
        _resolutionServiceMock = new Mock<IPartnerOrderResolutionService>();
        _loggerMock = new Mock<ILogger<SupplierOrderIntakeService>>();

        _service = new SupplierOrderIntakeService(
            _orderRepoMock.Object,
            _orgRepoMock.Object,
            _tenantRepoMock.Object,
            _accountRepoMock.Object,
            _resolutionServiceMock.Object,
            _loggerMock.Object);
    }

    private static SubmitSupplierOrderRequest CreateValidRequest() => new()
    {
        SourcePlatform = "Merchant360",
        OrganizationId = 1,
        MerchantId = 10,
        PartnerConnectionId = 100,
        ExternalOrderId = "M360-ORDER-001",
        CorrelationId = Guid.NewGuid().ToString(),
        IdempotencyKey = Guid.NewGuid().ToString(),
        PoNumber = "PO-2024-001",
        ShipTo = new CanonicalAddressInfo
        {
            Name = "Test Customer",
            Address1 = "123 Main St",
            City = "Chicago",
            State = "IL",
            PostalCode = "60601",
            Country = "US"
        },
        Lines =
        [
            new CanonicalOrderLineRequest
            {
                VendorSku = "SKU-001",
                Quantity = 5,
                UnitPrice = 10.00m
            }
        ]
    };

    #region Routing Context Validation Tests

    [Fact]
    public async Task SubmitOrder_MissingSourcePlatform_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with { SourcePlatform = "" };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "SourcePlatform");
    }

    [Fact]
    public async Task SubmitOrder_InvalidOrganizationId_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with { OrganizationId = 0 };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "OrganizationId");
    }

    [Fact]
    public async Task SubmitOrder_InvalidMerchantId_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with { MerchantId = 0 };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "MerchantId");
    }

    [Fact]
    public async Task SubmitOrder_InvalidPartnerConnectionId_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with { PartnerConnectionId = 0 };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "PartnerConnectionId");
    }

    [Fact]
    public async Task SubmitOrder_MissingExternalOrderId_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with { ExternalOrderId = "" };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "ExternalOrderId");
    }

    [Fact]
    public async Task SubmitOrder_MissingCorrelationId_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with { CorrelationId = "" };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "CorrelationId");
    }

    [Fact]
    public async Task SubmitOrder_MissingIdempotencyKey_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with { IdempotencyKey = "" };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "IdempotencyKey");
    }

    #endregion

    #region Business Field Validation Tests

    [Fact]
    public async Task SubmitOrder_MissingPoNumber_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with { PoNumber = "" };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "PoNumber");
    }

    [Fact]
    public async Task SubmitOrder_NoOrderLines_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with { Lines = [] };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Lines");
    }

    [Fact]
    public async Task SubmitOrder_MissingVendorSku_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with
        {
            Lines = [new CanonicalOrderLineRequest { VendorSku = "", Quantity = 5 }]
        };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field.Contains("VendorSku"));
    }

    [Fact]
    public async Task SubmitOrder_ZeroQuantity_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with
        {
            Lines = [new CanonicalOrderLineRequest { VendorSku = "SKU-001", Quantity = 0 }]
        };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field.Contains("Quantity"));
    }

    [Fact]
    public async Task SubmitOrder_MissingShipTo_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest() with { ShipTo = null };

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "ShipTo");
    }

    #endregion

    #region Idempotency Tests

    [Fact]
    public async Task SubmitOrder_DuplicateSubmission_ReturnsExistingOrder()
    {
        // Arrange
        var request = CreateValidRequest();
        var existingOrder = new Order
        {
            Id = 42,
            OrganizationId = request.OrganizationId,
            TenantId = request.MerchantId,
            PoNumber = request.PoNumber,
            IdempotencyKey = request.IdempotencyKey,
            CorrelationId = Guid.NewGuid(),
            TradingPartnerId = 1,
            Lines = [new OrderLine { Sku = "SKU-001", Quantity = 5 }]
        };

        _orderRepoMock.Setup(r => r.GetByIdempotencyKeyAsync(
            request.OrganizationId, request.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeTrue();
        result.IsDuplicate.Should().BeTrue();
        result.PartnerConnectOrderId.Should().Be(42);
    }

    #endregion

    #region Organization/Tenant Validation Tests

    [Fact]
    public async Task SubmitOrder_OrganizationNotFound_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest();

        _orderRepoMock.Setup(r => r.GetByIdempotencyKeyAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        _orgRepoMock.Setup(r => r.GetByIdAsync(request.OrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "ORGANIZATION_NOT_FOUND");
    }

    [Fact]
    public async Task SubmitOrder_OrganizationNotActive_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest();

        _orderRepoMock.Setup(r => r.GetByIdempotencyKeyAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        _orgRepoMock.Setup(r => r.GetByIdAsync(request.OrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 1, Code = "ORG", Name = "Test Org", Status = OrganizationStatus.Suspended });

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "ORGANIZATION_NOT_ACTIVE");
    }

    [Fact]
    public async Task SubmitOrder_TenantNotFound_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest();

        _orderRepoMock.Setup(r => r.GetByIdempotencyKeyAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        _orgRepoMock.Setup(r => r.GetByIdAsync(request.OrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 1, Code = "ORG", Name = "Test Org", Status = OrganizationStatus.Active });

        _tenantRepoMock.Setup(r => r.GetByOrgAndExternalIdAsync(request.OrganizationId, request.MerchantId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "MERCHANT_NOT_FOUND");
    }

    [Fact]
    public async Task SubmitOrder_TenantNotInOrganization_ReturnsValidationError()
    {
        // Tenant resolution is now scoped to the org, so a tenant that belongs to a different org
        // simply isn't found for this org's request.
        var request = CreateValidRequest();

        _orderRepoMock.Setup(r => r.GetByIdempotencyKeyAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        _orgRepoMock.Setup(r => r.GetByIdAsync(request.OrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = 1, Code = "ORG", Name = "Test Org", Status = OrganizationStatus.Active });

        // No tenant exists under this org for the supplied external id.
        _tenantRepoMock.Setup(r => r.GetByOrgAndExternalIdAsync(request.OrganizationId, request.MerchantId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "MERCHANT_NOT_FOUND");
    }

    #endregion

    #region Partner Resolution Tests

    [Fact]
    public async Task SubmitOrder_PartnerConnectionNotFound_ReturnsValidationError()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupValidOrgAndTenant(request);

        _resolutionServiceMock.Setup(s => s.ValidatePartnerConnectionAsync(
            request.PartnerConnectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerResolutionResult.Failed("PARTNER_CONNECTION_NOT_FOUND", "PartnerConnectionId", "Not found"));

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "PARTNER_CONNECTION_NOT_FOUND");
    }

    #endregion

    #region Successful Submission Tests

    [Fact]
    public async Task SubmitOrder_ValidRequest_CreatesOrderSuccessfully()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupValidOrgAndTenant(request);
        SetupValidPartnerResolution(request);

        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order o, CancellationToken _) =>
            {
                o.Id = 123;
                return o;
            });

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();
        result.PartnerConnectOrderId.Should().Be(123);
        result.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SubmitOrder_ValidRequest_SetsIntegrationTrackingFields()
    {
        // Arrange
        var request = CreateValidRequest() with { SubmittedBy = "test-user" };
        SetupValidOrgAndTenant(request);
        SetupValidPartnerResolution(request);

        Order? createdOrder = null;
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => createdOrder = o)
            .ReturnsAsync((Order o, CancellationToken _) =>
            {
                o.Id = 123;
                return o;
            });

        // Act
        await _service.SubmitOrderAsync(request);

        // Assert
        createdOrder.Should().NotBeNull();
        createdOrder!.SourcePlatform.Should().Be("Merchant360");
        createdOrder.ExternalOrderId.Should().Be(request.ExternalOrderId);
        createdOrder.IdempotencyKey.Should().Be(request.IdempotencyKey);
        createdOrder.SubmittedBy.Should().Be("test-user");
        createdOrder.AllowPartialShipment.Should().BeTrue();
        createdOrder.AllowBackorder.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitOrder_ResolvesTenantByExternalId_NotInternalId()
    {
        // M360 sends its own merchant id (3), stored on the PC tenant as ExternalId — NOT PC's
        // internal Tenant.Id. The created order must be attributed to the resolved internal id (2),
        // even though a different tenant happens to have internal Id == 3.
        var request = CreateValidRequest() with { MerchantId = 3 };

        _orderRepoMock.Setup(r => r.GetByIdempotencyKeyAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        _orgRepoMock.Setup(r => r.GetByIdAsync(request.OrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization
            {
                Id = request.OrganizationId,
                Code = "ORG",
                Name = "Test Org",
                Status = OrganizationStatus.Active
            });
        _tenantRepoMock.Setup(r => r.GetByOrgAndExternalIdAsync(request.OrganizationId, "3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant
            {
                Id = 2,
                ExternalId = "3",
                OrganizationId = request.OrganizationId,
                Code = "ASAD",
                Name = "Asad Merchant",
                Status = TenantStatus.Active
            });
        // The connection belongs to the resolved INTERNAL tenant id (2), not the external merchant id (3).
        SetupValidPartnerResolution(request, accountTenantId: 2);

        Order? createdOrder = null;
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => createdOrder = o)
            .ReturnsAsync((Order o, CancellationToken _) => { o.Id = 55; return o; });

        // Act
        var result = await _service.SubmitOrderAsync(request);

        // Assert
        result.Accepted.Should().BeTrue();
        createdOrder.Should().NotBeNull();
        createdOrder!.TenantId.Should().Be(2);
    }

    [Fact]
    public async Task SubmitOrder_ConnectionBelongsToDifferentTenant_IsRejected()
    {
        // Effective-status chain guard: the resolved tenant is internal id 2, but the partner
        // connection belongs to a different tenant (id 7). The order must be rejected.
        var request = CreateValidRequest();
        SetupValidOrgAndTenant(request); // resolved tenant.Id == request.MerchantId
        SetupValidPartnerResolution(request, accountTenantId: 7);

        var result = await _service.SubmitOrderAsync(request);

        result.Accepted.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "PARTNER_CONNECTION_TENANT_MISMATCH");
    }

    [Fact]
    public async Task SubmitOrder_ValidRequest_CreatesOrderLines()
    {
        // Arrange
        var request = CreateValidRequest() with
        {
            Lines =
            [
                new CanonicalOrderLineRequest { VendorSku = "SKU-001", BuyerSku = "MY-001", Quantity = 5, UnitPrice = 10.00m },
                new CanonicalOrderLineRequest { VendorSku = "SKU-002", Quantity = 3, UnitPrice = 25.00m }
            ]
        };
        SetupValidOrgAndTenant(request);
        SetupValidPartnerResolution(request);

        Order? createdOrder = null;
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => createdOrder = o)
            .ReturnsAsync((Order o, CancellationToken _) =>
            {
                o.Id = 123;
                return o;
            });

        // Act
        await _service.SubmitOrderAsync(request);

        // Assert
        createdOrder.Should().NotBeNull();
        createdOrder!.Lines.Should().HaveCount(2);
        createdOrder.Lines.First().VendorSku.Should().Be("SKU-001");
        createdOrder.Lines.First().Sku.Should().Be("MY-001"); // BuyerSku used for Sku
        createdOrder.Lines.First().LineTotal.Should().Be(50.00m);
        createdOrder.SubTotal.Should().Be(125.00m); // 5*10 + 3*25
    }

    [Fact]
    public async Task SubmitOrder_ValidRequest_RecordsStatusHistory()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupValidOrgAndTenant(request);
        SetupValidPartnerResolution(request);

        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order o, CancellationToken _) =>
            {
                o.Id = 123;
                return o;
            });

        // Act
        await _service.SubmitOrderAsync(request);

        // Assert
        _orderRepoMock.Verify(r => r.AddStatusHistoryAsync(
            It.Is<OrderStatusHistory>(h =>
                h.ToStatus == OrderStatus.Submitted &&
                h.Source == "Merchant360"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    private void SetupValidOrgAndTenant(SubmitSupplierOrderRequest request)
    {
        _orderRepoMock.Setup(r => r.GetByIdempotencyKeyAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        _orgRepoMock.Setup(r => r.GetByIdAsync(request.OrganizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization
            {
                Id = request.OrganizationId,
                Code = "ORG",
                Name = "Test Org",
                Status = OrganizationStatus.Active
            });

        _tenantRepoMock.Setup(r => r.GetByOrgAndExternalIdAsync(request.OrganizationId, request.MerchantId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant
            {
                Id = request.MerchantId,
                ExternalId = request.MerchantId.ToString(),
                OrganizationId = request.OrganizationId,
                Code = "TENANT",
                Name = "Test Tenant",
                Status = TenantStatus.Active
            });
    }

    private void SetupValidPartnerResolution(SubmitSupplierOrderRequest request, int? accountTenantId = null)
    {
        var account = new TenantPartnerAccount
        {
            Id = request.PartnerConnectionId,
            // The connection's TenantId is the INTERNAL tenant id. In most tests the resolved tenant's
            // internal id equals MerchantId, but it can differ (external id vs internal id) — callers
            // pass accountTenantId explicitly in that case.
            TenantId = accountTenantId ?? request.MerchantId,
            TradingPartnerId = 1,
            AccountNumber = "ACCT-001",
            IsActive = true
        };

        var partner = new TradingPartner
        {
            Id = 1,
            Code = "SPR",
            Name = "S.P. Richards",
            Status = TradingPartnerStatus.Active
        };

        var config = new PartnerOrderConfiguration
        {
            PartnerCode = "SPR",
            AccountNumber = "ACCT-001"
        };

        _resolutionServiceMock.Setup(s => s.ValidatePartnerConnectionAsync(
            request.PartnerConnectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerResolutionResult.Succeeded(account, partner, config));

        _resolutionServiceMock.Setup(s => s.ResolvePartnerRequirementsAsync(
            request, account, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PartnerResolutionResult.Succeeded(account, partner, config));
    }

    #endregion
}
