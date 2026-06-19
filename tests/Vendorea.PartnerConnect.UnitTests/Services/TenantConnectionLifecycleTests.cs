using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Services;

/// <summary>
/// Cancel / unsubscribe lifecycle on a tenant-partner connection (merchant-initiated via the org API).
/// Connections are keyed by (org, ExternalTenantId, PartnerConnect TradingPartnerId).
/// </summary>
public class TenantConnectionLifecycleTests
{
    private const int OrgId = 7;
    private const string ExternalTenantId = "42";
    private const int TradingPartnerId = 1;

    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly Mock<ITenantPartnerAccountRepository> _connRepo = new();
    private readonly TenantConnectionService _sut;

    public TenantConnectionLifecycleTests()
        => _sut = new TenantConnectionService(_orgRepo.Object, _connRepo.Object, NullLogger<TenantConnectionService>.Instance);

    private void HasConnections(params TenantPartnerAccount[] connections) =>
        _connRepo.Setup(r => r.GetConnectionsAsync(OrgId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connections.ToList());

    private static TenantPartnerAccount Conn(ConnectionApprovalStatus status, bool isActive) => new()
    {
        Id = 100,
        OrganizationId = OrgId,
        ExternalTenantId = ExternalTenantId,
        TradingPartnerId = TradingPartnerId,
        ApprovalStatus = status,
        IsActive = isActive
    };

    [Fact]
    public async Task Cancel_PendingConnection_SetsCancelledAndInactive()
    {
        var conn = Conn(ConnectionApprovalStatus.Pending, isActive: false);
        HasConnections(conn);

        var result = await _sut.CancelConnectionAsync(OrgId, ExternalTenantId, TradingPartnerId);

        result.Status.Should().Be(ConnectionChangeStatus.Ok);
        conn.ApprovalStatus.Should().Be(ConnectionApprovalStatus.Cancelled);
        conn.IsActive.Should().BeFalse();
        _connRepo.Verify(r => r.UpdateAsync(conn, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_NoConnection_ReturnsNotFound()
    {
        HasConnections();
        var result = await _sut.CancelConnectionAsync(OrgId, ExternalTenantId, TradingPartnerId);
        result.Status.Should().Be(ConnectionChangeStatus.NotFound);
    }

    [Fact]
    public async Task Cancel_ApprovedConnection_ReturnsInvalidState()
    {
        HasConnections(Conn(ConnectionApprovalStatus.Approved, isActive: true));
        var result = await _sut.CancelConnectionAsync(OrgId, ExternalTenantId, TradingPartnerId);
        result.Status.Should().Be(ConnectionChangeStatus.InvalidState);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_IsIdempotent()
    {
        HasConnections(Conn(ConnectionApprovalStatus.Cancelled, isActive: false));
        var result = await _sut.CancelConnectionAsync(OrgId, ExternalTenantId, TradingPartnerId);
        result.Status.Should().Be(ConnectionChangeStatus.Ok);
        _connRepo.Verify(r => r.UpdateAsync(It.IsAny<TenantPartnerAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unsubscribe_ApprovedConnection_SetsUnsubscribedAndInactive()
    {
        var conn = Conn(ConnectionApprovalStatus.Approved, isActive: true);
        HasConnections(conn);

        var result = await _sut.UnsubscribeConnectionAsync(OrgId, ExternalTenantId, TradingPartnerId);

        result.Status.Should().Be(ConnectionChangeStatus.Ok);
        conn.ApprovalStatus.Should().Be(ConnectionApprovalStatus.Unsubscribed);
        conn.IsActive.Should().BeFalse();
        _connRepo.Verify(r => r.UpdateAsync(conn, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unsubscribe_PendingConnection_ReturnsInvalidState()
    {
        HasConnections(Conn(ConnectionApprovalStatus.Pending, isActive: false));
        var result = await _sut.UnsubscribeConnectionAsync(OrgId, ExternalTenantId, TradingPartnerId);
        result.Status.Should().Be(ConnectionChangeStatus.InvalidState);
    }

    [Fact]
    public async Task Unsubscribe_NoConnection_ReturnsNotFound()
    {
        HasConnections();
        var result = await _sut.UnsubscribeConnectionAsync(OrgId, ExternalTenantId, TradingPartnerId);
        result.Status.Should().Be(ConnectionChangeStatus.NotFound);
    }

    [Fact]
    public async Task Cancel_DifferentTenantOrPartner_NotMatched_ReturnsNotFound()
    {
        // A connection exists, but for a different tenant id — must not be cancelled.
        var other = Conn(ConnectionApprovalStatus.Pending, isActive: false);
        other.ExternalTenantId = "999";
        HasConnections(other);

        var result = await _sut.CancelConnectionAsync(OrgId, ExternalTenantId, TradingPartnerId);

        result.Status.Should().Be(ConnectionChangeStatus.NotFound);
    }
}
