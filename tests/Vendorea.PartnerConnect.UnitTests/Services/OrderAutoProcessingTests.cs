using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Services;

/// <summary>
/// The tenant Order Processing setting, and what Automatic actually does.
/// </summary>
/// <remarks>
/// The behaviour under test is one that dispatches a purchase order to a real supplier with no
/// human looking at it, so the tests are written around the two ways that can go wrong: acting
/// when it should not have, and failing silently once it has.
/// </remarks>
public class OrderAutoProcessingTests
{
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IOutboxService> _outbox = new();
    private readonly OrderAutoProcessingService _service;

    public OrderAutoProcessingTests()
    {
        _service = new OrderAutoProcessingService(
            _tenants.Object, _orders.Object, _outbox.Object,
            new Mock<ILogger<OrderAutoProcessingService>>().Object);
    }

    private void TenantIs(OrderProcessingMode mode) =>
        _tenants.Setup(t => t.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = 7, Code = "D1", Name = "Dealer", OrderProcessingMode = mode });

    private static Order SubmittedOrder() => new()
    {
        Id = 42, TenantId = 7, OrganizationId = 1, PoNumber = "PO-42", Status = OrderStatus.Submitted
    };

    private void VerifyNothingQueued() =>
        _outbox.Verify(o => o.EnqueueAsync(
            It.IsAny<string>(), It.IsAny<OrderAutoTransmitPayload>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

    // ---- when it must not act ----

    [Fact]
    public async Task A_manual_tenant_is_left_alone()
    {
        // The default, and the behaviour every existing tenant keeps.
        TenantIs(OrderProcessingMode.Manual);
        var order = SubmittedOrder();

        var acted = await _service.TryAutoProcessAsync(order);

        acted.Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Submitted);
        order.AcknowledgedAt.Should().BeNull();
        VerifyNothingQueued();
    }

    [Fact]
    public async Task An_order_that_is_not_submitted_is_left_alone()
    {
        // Re-acknowledging an order that has already moved on would rewrite a status another
        // process owns, and could dispatch it a second time.
        TenantIs(OrderProcessingMode.Automatic);
        var order = SubmittedOrder();
        order.Status = OrderStatus.Processing;

        var acted = await _service.TryAutoProcessAsync(order);

        acted.Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Processing);
        VerifyNothingQueued();
    }

    [Fact]
    public async Task A_missing_tenant_does_not_throw_and_does_not_dispatch()
    {
        _tenants.Setup(t => t.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);
        var order = SubmittedOrder();

        var acted = await _service.TryAutoProcessAsync(order);

        acted.Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Submitted);
        VerifyNothingQueued();
    }

    // ---- when it must act ----

    [Fact]
    public async Task An_automatic_tenant_has_its_order_acknowledged_and_queued()
    {
        TenantIs(OrderProcessingMode.Automatic);
        var order = SubmittedOrder();

        var acted = await _service.TryAutoProcessAsync(order);

        acted.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Acknowledged);
        order.AcknowledgedAt.Should().NotBeNull();

        _outbox.Verify(o => o.EnqueueAsync(
            OrderAutoProcessingMessageTypes.Transmit,
            It.Is<OrderAutoTransmitPayload>(p => p.OrderId == 42),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task The_acknowledgement_is_recorded_in_history_as_automatic()
    {
        // Without this, an order that went out unreviewed is indistinguishable from one an admin
        // approved.
        TenantIs(OrderProcessingMode.Automatic);

        await _service.TryAutoProcessAsync(SubmittedOrder());

        _orders.Verify(o => o.AddStatusHistoryAsync(
            It.Is<OrderStatusHistory>(h =>
                h.OrderId == 42 &&
                h.FromStatus == OrderStatus.Submitted &&
                h.ToStatus == OrderStatus.Acknowledged &&
                h.Source == "Auto"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- when the automation itself fails ----

    [Fact]
    public async Task A_failure_to_queue_does_not_propagate_to_the_submitter()
    {
        // The order is already saved and the caller is about to be told it was accepted. Throwing
        // here would report failure for an order that exists.
        TenantIs(OrderProcessingMode.Automatic);
        _outbox.Setup(o => o.EnqueueAsync(
                It.IsAny<string>(), It.IsAny<OrderAutoTransmitPayload>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox down"));

        var act = async () => await _service.TryAutoProcessAsync(SubmittedOrder());

        await act.Should().NotThrowAsync();
        (await _service.TryAutoProcessAsync(SubmittedOrder())).Should().BeFalse();
    }

    [Fact]
    public async Task A_failure_to_queue_leaves_the_order_acknowledged_for_manual_dispatch()
    {
        // Acknowledge-then-queue means a queue failure lands the order exactly where a Manual
        // tenant's order sits: an admin can transmit it by hand. The reverse order would risk
        // dispatching an order whose acknowledgement never stuck.
        TenantIs(OrderProcessingMode.Automatic);
        _outbox.Setup(o => o.EnqueueAsync(
                It.IsAny<string>(), It.IsAny<OrderAutoTransmitPayload>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox down"));
        var order = SubmittedOrder();

        await _service.TryAutoProcessAsync(order);

        order.Status.Should().Be(OrderStatus.Acknowledged);
    }

    // ---- the default ----

    [Fact]
    public void A_new_tenant_is_manual()
    {
        new Tenant().OrderProcessingMode.Should().Be(OrderProcessingMode.Manual);
    }
}
