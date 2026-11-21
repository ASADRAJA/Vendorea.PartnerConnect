using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Services;

/// <summary>
/// Whether a failed automatic transmission retries or retires.
/// </summary>
/// <remarks>
/// This is the whole risk of Automatic in one place. The outbox retries a handler that throws and
/// retires one that returns, so the handler's choice between the two decides whether a temporary
/// supplier outage is survived or an order is silently dropped. SPR's SFTP was unreachable for 86
/// days; under Automatic that window is exactly the case these tests are about.
/// </remarks>
public class OrderAutoTransmitDispatchTests
{
    private readonly Mock<ISprOutboundOrderService> _outbound = new();

    private DefaultOutboxMessageProcessor Processor() => new(
        new Mock<IHttpClientFactory>().Object,
        new Mock<IMerchant360Client>().Object,
        // CaptureCallbacks must be off, or the handler short-circuits before it runs.
        Options.Create(new SprSimulationOptions()),
        NullLogger<DefaultOutboxMessageProcessor>.Instance,
        integrationEvents: null,
        outboundOrders: () => _outbound.Object);

    private static OutboxMessage Message(int orderId = 42) => new()
    {
        MessageType = OrderAutoProcessingMessageTypes.Transmit,
        Payload = JsonSerializer.Serialize(
            new OrderAutoTransmitPayload { OrderId = orderId },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
    };

    private void TransmitReturns(SprTransmitResult result) =>
        _outbound.Setup(o => o.TransmitOrderAsync(42, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    // ---- transient: must throw, so the outbox retries ----

    [Fact]
    public async Task An_unreachable_supplier_throws_so_the_outbox_retries()
    {
        // The case that matters. Returning here would retire the message and lose the order.
        TransmitReturns(new SprTransmitResult
        {
            Success = false,
            ErrorMessage = "Connection failed to establish within 30000 milliseconds"
        });

        var act = async () => await Processor().ProcessAsync(Message());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---- permanent: must not throw, so the message retires ----

    [Fact]
    public async Task A_schema_invalid_order_does_not_retry()
    {
        // Strict XSD validation fails identically on every attempt. Retrying burns the budget and
        // buries the one log line that says what is wrong with the order.
        TransmitReturns(new SprTransmitResult
        {
            Success = false, ValidationFailed = true, ErrorMessage = "EZPO4 failed XSD validation",
            Errors = { "ShipTo is required" }
        });

        var act = async () => await Processor().ProcessAsync(Message());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task A_missing_order_does_not_retry()
    {
        TransmitReturns(SprTransmitResult.NotFoundResult());

        var act = async () => await Processor().ProcessAsync(Message());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task An_order_past_the_transmittable_statuses_does_not_retry()
    {
        // A cancelled order must not be retried onto the supplier forever.
        TransmitReturns(new SprTransmitResult
        {
            Success = false, InvalidState = true, ErrorMessage = "Order is Cancelled"
        });

        var act = async () => await Processor().ProcessAsync(Message());

        await act.Should().NotThrowAsync();
    }

    // ---- success ----

    [Fact]
    public async Task A_transmitted_order_completes_the_message()
    {
        TransmitReturns(new SprTransmitResult { Success = true, DocumentId = 99 });

        var act = async () => await Processor().ProcessAsync(Message());

        await act.Should().NotThrowAsync();
        _outbound.Verify(o => o.TransmitOrderAsync(42, false, It.IsAny<CancellationToken>()), Times.Once,
            "automatic dispatch must obey the duplicate guard; it never forces");
    }

    // ---- degenerate inputs ----

    [Fact]
    public async Task A_host_without_an_outbound_service_retires_rather_than_retrying_forever()
    {
        // Throwing would retry against a process that can never serve the message.
        var processor = new DefaultOutboxMessageProcessor(
            new Mock<IHttpClientFactory>().Object,
            new Mock<IMerchant360Client>().Object,
            Options.Create(new SprSimulationOptions()),
            NullLogger<DefaultOutboxMessageProcessor>.Instance,
            integrationEvents: null,
            outboundOrders: null);

        var act = async () => await processor.ProcessAsync(Message());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task A_message_with_no_order_id_does_not_reach_the_supplier()
    {
        var act = async () => await Processor().ProcessAsync(Message(orderId: 0));

        await act.Should().NotThrowAsync();
        _outbound.Verify(o => o.TransmitOrderAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Capture_mode_does_not_send_an_order_to_the_supplier()
    {
        // SprSimulation capture mode exists so a test environment can exercise the pipeline
        // without touching the real supplier. Automatic dispatch must honour it.
        var processor = new DefaultOutboxMessageProcessor(
            new Mock<IHttpClientFactory>().Object,
            new Mock<IMerchant360Client>().Object,
            Options.Create(new SprSimulationOptions { CaptureCallbacks = true }),
            NullLogger<DefaultOutboxMessageProcessor>.Instance,
            integrationEvents: null,
            outboundOrders: () => _outbound.Object);

        await processor.ProcessAsync(Message());

        _outbound.Verify(o => o.TransmitOrderAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
