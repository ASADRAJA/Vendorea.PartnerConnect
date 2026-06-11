using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.UnitTests.Services;

public class InventoryFullRefreshServiceTests
{
    private readonly Mock<ISupplierInventorySnapshotRepository> _snapshotRepoMock;
    private readonly Mock<ISupplierInventoryItemRepository> _itemRepoMock;
    private readonly Mock<ILogger<InventoryFullRefreshService>> _loggerMock;
    private readonly InventoryFullRefreshService _sut;

    public InventoryFullRefreshServiceTests()
    {
        _snapshotRepoMock = new Mock<ISupplierInventorySnapshotRepository>();
        _itemRepoMock = new Mock<ISupplierInventoryItemRepository>();
        _loggerMock = new Mock<ILogger<InventoryFullRefreshService>>();
        var accountRepoMock = new Mock<ITenantPartnerAccountRepository>();
        accountRepoMock.Setup(r => r.GetByTradingPartnerIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPartnerAccount>());
        _sut = new InventoryFullRefreshService(
            _snapshotRepoMock.Object,
            _itemRepoMock.Object,
            accountRepoMock.Object,
            new Mock<IOutboxService>().Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateSnapshotAsync_CreatesNewSnapshot()
    {
        // Arrange
        var tradingPartnerId = 1;
        var fileName = "inventory_20260606.csv";
        var inventoryDate = new DateTime(2026, 6, 6);

        _snapshotRepoMock
            .Setup(r => r.GetLatestAppliedAsync(tradingPartnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierInventorySnapshot?)null);

        _snapshotRepoMock
            .Setup(r => r.AddAsync(It.IsAny<SupplierInventorySnapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierInventorySnapshot s, CancellationToken _) =>
            {
                s.Id = 1;
                return s;
            });

        // Act
        var result = await _sut.CreateSnapshotAsync(tradingPartnerId, fileName, inventoryDate);

        // Assert
        result.Should().NotBeNull();
        result.TradingPartnerId.Should().Be(tradingPartnerId);
        result.SnapshotId.Should().Be(fileName);
        result.InventoryDate.Should().Be(inventoryDate);
        result.Status.Should().Be(InventorySnapshotStatus.Received);
        result.IsFullRefresh.Should().BeTrue();
        result.PreviousSnapshotId.Should().BeNull();
    }

    [Fact]
    public async Task CreateSnapshotAsync_LinksToPreviousSnapshot()
    {
        // Arrange
        var tradingPartnerId = 1;
        var previousSnapshot = new SupplierInventorySnapshot
        {
            Id = 10,
            TradingPartnerId = tradingPartnerId,
            Status = InventorySnapshotStatus.Applied
        };

        _snapshotRepoMock
            .Setup(r => r.GetLatestAppliedAsync(tradingPartnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousSnapshot);

        _snapshotRepoMock
            .Setup(r => r.AddAsync(It.IsAny<SupplierInventorySnapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierInventorySnapshot s, CancellationToken _) =>
            {
                s.Id = 11;
                return s;
            });

        // Act
        var result = await _sut.CreateSnapshotAsync(tradingPartnerId, "file.csv", DateTime.UtcNow);

        // Assert
        result.PreviousSnapshotId.Should().Be(10);
    }

    [Fact]
    public async Task ValidateAndStageAsync_WithValidItems_StagesItems()
    {
        // Arrange
        var snapshotId = 1;
        var snapshot = new SupplierInventorySnapshot
        {
            Id = snapshotId,
            Status = InventorySnapshotStatus.Received
        };

        var items = new List<SupplierInventoryItem>
        {
            new() { SupplierSku = "SKU001", QuantityAvailable = 100 },
            new() { SupplierSku = "SKU002", QuantityAvailable = 50 },
            new() { SupplierSku = "SKU003", QuantityAvailable = 25 }
        };

        _snapshotRepoMock
            .Setup(r => r.GetByIdAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await _sut.ValidateAndStageAsync(snapshotId, items);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.TotalItems.Should().Be(3);
        result.ValidItems.Should().Be(3);
        result.InvalidItems.Should().Be(0);
        result.ResultStatus.Should().Be(InventorySnapshotStatus.Staging);

        _itemRepoMock.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<SupplierInventoryItem>>(i => i.Count() == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAndStageAsync_WithInvalidItems_FailsValidation()
    {
        // Arrange
        var snapshotId = 1;
        var snapshot = new SupplierInventorySnapshot
        {
            Id = snapshotId,
            Status = InventorySnapshotStatus.Received
        };

        var items = new List<SupplierInventoryItem>
        {
            new() { SupplierSku = "", QuantityAvailable = 100 }, // Invalid: empty SKU
            new() { SupplierSku = "SKU002", QuantityAvailable = -5 } // Invalid: negative quantity
        };

        _snapshotRepoMock
            .Setup(r => r.GetByIdAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await _sut.ValidateAndStageAsync(snapshotId, items);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.InvalidItems.Should().BeGreaterThan(0);
        result.Errors.Should().NotBeEmpty();
        result.ResultStatus.Should().Be(InventorySnapshotStatus.ValidationFailed);
    }

    [Fact]
    public async Task ValidateAndStageAsync_WithDuplicateSkus_ReportsErrors()
    {
        // Arrange
        var snapshotId = 1;
        var snapshot = new SupplierInventorySnapshot
        {
            Id = snapshotId,
            Status = InventorySnapshotStatus.Received
        };

        var items = new List<SupplierInventoryItem>
        {
            new() { SupplierSku = "SKU001", QuantityAvailable = 100 },
            new() { SupplierSku = "SKU001", QuantityAvailable = 50 }, // Duplicate
            new() { SupplierSku = "SKU002", QuantityAvailable = 25 }
        };

        _snapshotRepoMock
            .Setup(r => r.GetByIdAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await _sut.ValidateAndStageAsync(snapshotId, items);

        // Assert
        result.InvalidItems.Should().Be(1); // The duplicate
        result.Errors.Should().Contain(e => e.Message.Contains("Duplicate"));
    }

    [Fact]
    public async Task ApplySnapshotAsync_StagedSnapshot_AppliesSuccessfully()
    {
        // Arrange
        var snapshotId = 1;
        var snapshot = new SupplierInventorySnapshot
        {
            Id = snapshotId,
            TradingPartnerId = 1,
            Status = InventorySnapshotStatus.Staging,
            Items = new List<SupplierInventoryItem>
            {
                new() { SupplierSku = "SKU001", QuantityAvailable = 100, UnitCost = 10.00m, Status = InventoryItemStatus.Available },
                new() { SupplierSku = "SKU002", QuantityAvailable = 50, UnitCost = 20.00m, Status = InventoryItemStatus.Available }
            }
        };

        _snapshotRepoMock
            .Setup(r => r.GetByIdWithItemsAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await _sut.ApplySnapshotAsync(snapshotId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NewItems.Should().Be(2);
        result.UpdatedItems.Should().Be(0);
        result.RemovedItems.Should().Be(0);
    }

    [Fact]
    public async Task ApplySnapshotAsync_NotInStagingStatus_ReturnsError()
    {
        // Arrange
        var snapshotId = 1;
        var snapshot = new SupplierInventorySnapshot
        {
            Id = snapshotId,
            Status = InventorySnapshotStatus.Received
        };

        _snapshotRepoMock
            .Setup(r => r.GetByIdWithItemsAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await _sut.ApplySnapshotAsync(snapshotId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Staging");
    }

    [Fact]
    public async Task ApplySnapshotAsync_WithPreviousSnapshot_CalculatesChanges()
    {
        // Arrange
        var previousSnapshotId = 1;
        var currentSnapshotId = 2;

        var previousSnapshot = new SupplierInventorySnapshot
        {
            Id = previousSnapshotId,
            TradingPartnerId = 1,
            Status = InventorySnapshotStatus.Applied,
            Items = new List<SupplierInventoryItem>
            {
                new() { SupplierSku = "SKU001", QuantityAvailable = 100, UnitCost = 10.00m, Status = InventoryItemStatus.Available },
                new() { SupplierSku = "SKU002", QuantityAvailable = 50, UnitCost = 20.00m, Status = InventoryItemStatus.Available },
                new() { SupplierSku = "SKU003", QuantityAvailable = 25, UnitCost = 15.00m, Status = InventoryItemStatus.Available } // Will be removed
            }
        };

        var currentSnapshot = new SupplierInventorySnapshot
        {
            Id = currentSnapshotId,
            TradingPartnerId = 1,
            PreviousSnapshotId = previousSnapshotId,
            Status = InventorySnapshotStatus.Staging,
            Items = new List<SupplierInventoryItem>
            {
                new() { SupplierSku = "SKU001", QuantityAvailable = 100, UnitCost = 10.00m, Status = InventoryItemStatus.Available }, // Unchanged
                new() { SupplierSku = "SKU002", QuantityAvailable = 75, UnitCost = 20.00m, Status = InventoryItemStatus.Available }, // Updated qty
                new() { SupplierSku = "SKU004", QuantityAvailable = 30, UnitCost = 25.00m, Status = InventoryItemStatus.Available } // New
            }
        };

        _snapshotRepoMock
            .Setup(r => r.GetByIdWithItemsAsync(currentSnapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentSnapshot);

        _snapshotRepoMock
            .Setup(r => r.GetByIdWithItemsAsync(previousSnapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousSnapshot);

        // Act
        var result = await _sut.ApplySnapshotAsync(currentSnapshotId);

        // Assert
        result.Success.Should().BeTrue();
        result.NewItems.Should().Be(1); // SKU004
        result.UpdatedItems.Should().Be(1); // SKU002
        result.UnchangedItems.Should().Be(1); // SKU001
        result.RemovedItems.Should().Be(1); // SKU003
        result.SupersededSnapshotId.Should().Be(previousSnapshotId);
    }

    [Fact]
    public async Task MarkFailedAsync_UpdatesSnapshotStatus()
    {
        // Arrange
        var snapshotId = 1;
        var snapshot = new SupplierInventorySnapshot
        {
            Id = snapshotId,
            Status = InventorySnapshotStatus.Validating
        };

        _snapshotRepoMock
            .Setup(r => r.GetByIdAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        await _sut.MarkFailedAsync(snapshotId, "Test error");

        // Assert
        _snapshotRepoMock.Verify(r => r.UpdateAsync(
            It.Is<SupplierInventorySnapshot>(s =>
                s.Status == InventorySnapshotStatus.Failed &&
                s.ErrorMessage == "Test error"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
