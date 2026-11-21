using FluentAssertions;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Services;

/// <summary>
/// When an order may be sent to the supplier.
/// </summary>
/// <remarks>
/// A resend is not idempotent. The outbound file name carries a timestamp, so sending twice does
/// not replace the first file — SPR receives a second document with the same CustomerPONo and
/// treats it as another purchase order. The rule previously blocked only terminal statuses, so
/// the button stayed live while an order was in flight and after SPR had accepted it.
/// </remarks>
public class OrderTransmitGuardTests
{
    private static Order Order(OrderStatus status, int? ackDocumentId = null) => new()
    {
        Id = 1,
        PoNumber = "WPO-20261121-0003",
        Status = status,
        AcknowledgmentDocumentId = ackDocumentId
    };

    // ---- may be sent ----

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Submitted)]
    public void An_order_that_has_not_been_sent_can_be(OrderStatus status)
    {
        Order(status).CanTransmit.Should().BeTrue();
    }

    [Fact]
    public void An_order_acknowledged_by_an_admin_can_be_sent()
    {
        // Acknowledged before dispatch: no acknowledgement document, because SPR has not replied.
        Order(OrderStatus.Acknowledged, ackDocumentId: null).CanTransmit.Should().BeTrue();
    }

    [Fact]
    public void A_failed_order_can_be_retried()
    {
        // An SPR ERROR ack means the order was not processed, and a transport failure leaves the
        // status untouched so it can be retried. Blocking Failed would strand both.
        Order(OrderStatus.Failed).CanTransmit.Should().BeTrue();
    }

    // ---- must not be sent ----

    [Fact]
    public void An_order_in_flight_cannot_be_sent_again()
    {
        // Processing means the EZPO4 is on SPR's SFTP and no acknowledgement has come back. This
        // is the window where a second press creates the duplicate.
        var order = Order(OrderStatus.Processing);

        order.CanTransmit.Should().BeFalse();
        order.TransmitBlockedReason.Should().Contain("awaiting");
    }

    [Fact]
    public void An_order_the_supplier_has_acknowledged_cannot_be_sent_again()
    {
        // Same status as the admin-acknowledged case above, opposite meaning. The acknowledgement
        // document is what tells them apart, which is why status alone is not the test.
        var order = Order(OrderStatus.Acknowledged, ackDocumentId: 42);

        order.CanTransmit.Should().BeFalse();
        order.TransmitBlockedReason.Should().Contain("supplier has acknowledged");
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public void Terminal_orders_stay_blocked(OrderStatus status)
    {
        Order(status).CanTransmit.Should().BeFalse();
    }

    [Fact]
    public void A_partially_shipped_order_is_blocked()
    {
        // Part of it is already with the customer; resending the whole PO would duplicate the
        // unshipped remainder. This was transmittable before.
        Order(OrderStatus.PartiallyShipped).CanTransmit.Should().BeFalse();
    }

    // ---- the reason is for a person to read ----

    [Fact]
    public void A_transmittable_order_gives_no_reason()
    {
        Order(OrderStatus.Submitted).TransmitBlockedReason.Should().BeNull();
    }

    [Theory]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.PartiallyShipped)]
    public void Every_blocked_order_says_why_without_naming_a_status(OrderStatus status)
    {
        var reason = Order(status).TransmitBlockedReason;

        reason.Should().NotBeNullOrWhiteSpace();
        reason.Should().NotContain(status.ToString(),
            "the reason is shown to an operator, so it describes what happened");
    }
}
