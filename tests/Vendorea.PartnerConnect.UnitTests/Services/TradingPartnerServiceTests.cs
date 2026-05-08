using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.UnitTests.Services;

public class TradingPartnerServiceTests
{
    private readonly Mock<ITradingPartnerRepository> _repositoryMock;
    private readonly Mock<ILogger<TradingPartnerService>> _loggerMock;
    private readonly TradingPartnerService _sut;

    public TradingPartnerServiceTests()
    {
        _repositoryMock = new Mock<ITradingPartnerRepository>();
        _loggerMock = new Mock<ILogger<TradingPartnerService>>();
        _sut = new TradingPartnerService(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_WhenPartnerExists_ReturnsPartnerDto()
    {
        // Arrange
        var partner = new TradingPartner
        {
            Id = 1,
            Code = "TEST",
            Name = "Test Partner",
            PartnerType = TradingPartnerType.Wholesaler,
            Status = TradingPartnerStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Code.Should().Be("TEST");
        result.Name.Should().Be("Test Partner");
    }

    [Fact]
    public async Task GetByIdAsync_WhenPartnerDoesNotExist_ReturnsNull()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradingPartner?)null);

        // Act
        var result = await _sut.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_CreatesPartnerWithPendingStatus()
    {
        // Arrange
        var command = new CreateTradingPartnerCommand(
            Code: "NEW",
            Name: "New Partner",
            Description: "Test Description",
            PartnerType: TradingPartnerType.Distributor,
            ContactEmail: "test@example.com",
            ContactPhone: null,
            WebsiteUrl: null);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<TradingPartner>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradingPartner p, CancellationToken _) =>
            {
                p.Id = 1;
                return p;
            });

        // Act
        var result = await _sut.CreateAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Code.Should().Be("NEW");
        result.Status.Should().Be(TradingPartnerStatus.Pending);
        _repositoryMock.Verify(r => r.AddAsync(
            It.Is<TradingPartner>(p => p.Status == TradingPartnerStatus.Pending),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
