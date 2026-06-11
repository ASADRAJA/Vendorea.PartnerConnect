using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.UnitTests.Integration;

/// <summary>
/// Tests for the direct PC → Merchant360 lifecycle callbacks delivered via the outbox:
/// order status, shipment, invoice, and inventory snapshot notification.
/// </summary>
public class Merchant360CallbackTests
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static DefaultOutboxMessageProcessor Processor(IMerchant360Client client) =>
        new(new Mock<IHttpClientFactory>().Object, client, NullLogger<DefaultOutboxMessageProcessor>.Instance);

    private static OutboxMessage Message(string type, object payload) =>
        new() { MessageType = type, Payload = JsonSerializer.Serialize(payload, CamelCase) };

    // ---- Outbox processor dispatch -----------------------------------------------------------

    [Fact]
    public async Task OutboxProcessor_OrderStatusMessage_CallsMerchant360Client()
    {
        OrderStatusUpdateRequest? captured = null;
        var client = new Mock<IMerchant360Client>();
        client.Setup(c => c.PushOrderStatusUpdateAsync(It.IsAny<int>(), It.IsAny<OrderStatusUpdateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<int, OrderStatusUpdateRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync(new OrderStatusUpdateResult { Success = true });

        await Processor(client.Object).ProcessAsync(Message(
            Merchant360OutboxMessageTypes.OrderStatus,
            new Merchant360OrderStatusOutboxPayload
            {
                MerchantId = 42,
                Request = new OrderStatusUpdateRequest { PoNumber = "PO-1", StatusType = OrderStatusType.Failed, StatusCode = "SPR_ERROR_ACK" }
            }));

        captured.Should().NotBeNull();
        captured!.StatusCode.Should().Be("SPR_ERROR_ACK");
        captured.PoNumber.Should().Be("PO-1");
    }

    [Fact]
    public async Task OutboxProcessor_OrderStatusUnsuccessful_ThrowsSoOutboxRetries()
    {
        var client = new Mock<IMerchant360Client>();
        client.Setup(c => c.PushOrderStatusUpdateAsync(It.IsAny<int>(), It.IsAny<OrderStatusUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusUpdateResult { Success = false, ErrorMessage = "M360 down" });

        var act = () => Processor(client.Object).ProcessAsync(Message(
            Merchant360OutboxMessageTypes.OrderStatus,
            new Merchant360OrderStatusOutboxPayload { MerchantId = 1 }));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OutboxProcessor_ShipmentMessage_CallsPushShipment()
    {
        ShipmentUpdateRequest? captured = null;
        var client = new Mock<IMerchant360Client>();
        client.Setup(c => c.PushShipmentUpdateAsync(It.IsAny<int>(), It.IsAny<ShipmentUpdateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<int, ShipmentUpdateRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync(new ShipmentUpdateResult { Success = true });

        await Processor(client.Object).ProcessAsync(Message(
            Merchant360OutboxMessageTypes.Shipment,
            new Merchant360ShipmentOutboxPayload
            {
                MerchantId = 7,
                Request = new ShipmentUpdateRequest { PoNumber = "PO-9", ShipmentId = "MAN-1", TrackingNumber = "1Z..." }
            }));

        captured.Should().NotBeNull();
        captured!.ShipmentId.Should().Be("MAN-1");
        captured.PoNumber.Should().Be("PO-9");
    }

    [Fact]
    public async Task OutboxProcessor_InvoiceMessage_CallsPushInvoice()
    {
        InvoiceUpdateRequest? captured = null;
        var client = new Mock<IMerchant360Client>();
        client.Setup(c => c.PushInvoiceUpdateAsync(It.IsAny<int>(), It.IsAny<InvoiceUpdateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<int, InvoiceUpdateRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync(new InvoiceUpdateResult { Success = true });

        await Processor(client.Object).ProcessAsync(Message(
            Merchant360OutboxMessageTypes.Invoice,
            new Merchant360InvoiceOutboxPayload
            {
                MerchantId = 7,
                Request = new InvoiceUpdateRequest { PoNumber = "PO-9", InvoiceNumber = "INV-1", TotalAmount = 100m }
            }));

        captured.Should().NotBeNull();
        captured!.InvoiceNumber.Should().Be("INV-1");
    }

    [Fact]
    public async Task OutboxProcessor_InventorySnapshotMessage_CallsNotification()
    {
        SupplierInventorySnapshotNotificationRequest? captured = null;
        var client = new Mock<IMerchant360Client>();
        client.Setup(c => c.PushInventorySnapshotNotificationAsync(It.IsAny<int>(), It.IsAny<SupplierInventorySnapshotNotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<int, SupplierInventorySnapshotNotificationRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync(new InventorySnapshotNotificationResult { Success = true });

        await Processor(client.Object).ProcessAsync(Message(
            Merchant360OutboxMessageTypes.InventorySnapshot,
            new Merchant360InventorySnapshotOutboxPayload
            {
                MerchantId = 10,
                Request = new SupplierInventorySnapshotNotificationRequest { SnapshotId = "snap.csv", NewItemCount = 3 }
            }));

        captured.Should().NotBeNull();
        captured!.SnapshotId.Should().Be("snap.csv");
        captured.NewItemCount.Should().Be(3);
    }

    // ---- Inventory snapshot apply enqueues a notification per merchant -----------------------

    [Fact]
    public async Task ApplySnapshot_EnqueuesSnapshotNotificationPerSubscribedMerchant()
    {
        var snapshot = new SupplierInventorySnapshot
        {
            Id = 1,
            TradingPartnerId = 3,
            SnapshotId = "snap.csv",
            CorrelationId = "corr-1",
            Status = InventorySnapshotStatus.Staging,
            IsFullRefresh = true,
            PreviousSnapshotId = null,
            Items = new List<SupplierInventoryItem>
            {
                new() { SupplierSku = "SKU-1", QuantityAvailable = 5, Status = InventoryItemStatus.Available },
                new() { SupplierSku = "SKU-2", QuantityAvailable = 0, Status = InventoryItemStatus.OutOfStock },
                new() { SupplierSku = "SKU-3", QuantityAvailable = 9, Status = InventoryItemStatus.Available }
            }
        };

        var snapshotRepo = new Mock<ISupplierInventorySnapshotRepository>();
        snapshotRepo.Setup(r => r.GetByIdWithItemsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        snapshotRepo.Setup(r => r.UpdateAsync(It.IsAny<SupplierInventorySnapshot>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var accountRepo = new Mock<ITenantPartnerAccountRepository>();
        accountRepo.Setup(r => r.GetByTradingPartnerIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPartnerAccount>
            {
                new() { TenantId = 10, TradingPartnerId = 3, IsActive = true },
                new() { TenantId = 20, TradingPartnerId = 3, IsActive = true },
                new() { TenantId = 30, TradingPartnerId = 3, IsActive = false } // inactive: excluded
            });

        var enqueued = new List<Merchant360InventorySnapshotOutboxPayload>();
        var outbox = new Mock<IOutboxService>();
        outbox.Setup(o => o.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<Merchant360InventorySnapshotOutboxPayload>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Merchant360InventorySnapshotOutboxPayload, string?, string?, int, CancellationToken>(
                (_, payload, _, _, _, _) => enqueued.Add(payload))
            .ReturnsAsync(Guid.NewGuid());

        var service = new InventoryFullRefreshService(
            snapshotRepo.Object,
            new Mock<ISupplierInventoryItemRepository>().Object,
            accountRepo.Object,
            outbox.Object,
            NullLogger<InventoryFullRefreshService>.Instance);

        var result = await service.ApplySnapshotAsync(1);

        result.Success.Should().BeTrue();
        result.NewItems.Should().Be(3);

        // One lightweight notification per active merchant (2), carrying summary counts.
        enqueued.Should().HaveCount(2);
        enqueued.Select(p => p.MerchantId).Should().BeEquivalentTo(new[] { 10, 20 });
        enqueued.Should().OnlyContain(p => p.Request.NewItemCount == 3 && p.Request.TotalItemCount == 3);
    }
}
