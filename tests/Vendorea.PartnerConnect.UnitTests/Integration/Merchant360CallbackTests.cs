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
/// Tests for the direct PC → Merchant360 lifecycle callbacks delivered via the outbox.
/// </summary>
public class Merchant360CallbackTests
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ---- Outbox processor dispatches M360 order-status callback ------------------------------

    [Fact]
    public async Task OutboxProcessor_OrderStatusMessage_CallsMerchant360Client()
    {
        OrderStatusUpdateRequest? captured = null;
        var client = new Mock<IMerchant360Client>();
        client.Setup(c => c.PushOrderStatusUpdateAsync(It.IsAny<int>(), It.IsAny<OrderStatusUpdateRequest>(), It.IsAny<CancellationToken>()))
            .Callback<int, OrderStatusUpdateRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync(new OrderStatusUpdateResult { Success = true });

        var processor = new DefaultOutboxMessageProcessor(
            new Mock<IHttpClientFactory>().Object, client.Object, NullLogger<DefaultOutboxMessageProcessor>.Instance);

        var payload = new Merchant360OrderStatusOutboxPayload
        {
            MerchantId = 42,
            Request = new OrderStatusUpdateRequest
            {
                PoNumber = "PO-ERR-1",
                StatusType = OrderStatusType.Failed,
                StatusCode = "SPR_ERROR_ACK"
            }
        };
        var message = new OutboxMessage
        {
            MessageType = Merchant360OutboxMessageTypes.OrderStatus,
            Payload = JsonSerializer.Serialize(payload, CamelCase)
        };

        await processor.ProcessAsync(message);

        client.Verify(c => c.PushOrderStatusUpdateAsync(42, It.IsAny<OrderStatusUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        captured.Should().NotBeNull();
        captured!.StatusType.Should().Be(OrderStatusType.Failed);
        captured.StatusCode.Should().Be("SPR_ERROR_ACK");
        captured.PoNumber.Should().Be("PO-ERR-1");
    }

    [Fact]
    public async Task OutboxProcessor_OrderStatusUnsuccessful_ThrowsSoOutboxRetries()
    {
        var client = new Mock<IMerchant360Client>();
        client.Setup(c => c.PushOrderStatusUpdateAsync(It.IsAny<int>(), It.IsAny<OrderStatusUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusUpdateResult { Success = false, ErrorMessage = "M360 down" });

        var processor = new DefaultOutboxMessageProcessor(
            new Mock<IHttpClientFactory>().Object, client.Object, NullLogger<DefaultOutboxMessageProcessor>.Instance);

        var message = new OutboxMessage
        {
            MessageType = Merchant360OutboxMessageTypes.OrderStatus,
            Payload = JsonSerializer.Serialize(new Merchant360OrderStatusOutboxPayload { MerchantId = 1 }, CamelCase)
        };

        // A throw signals the OutboxService to schedule a retry with backoff (non-fatal).
        var act = () => processor.ProcessAsync(message);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task OutboxProcessor_InventoryBatchMessage_CallsUpdateInventory()
    {
        int capturedMerchant = 0;
        List<InventoryUpdateItem>? capturedItems = null;
        var client = new Mock<IMerchant360Client>();
        client.Setup(c => c.UpdateInventoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<InventoryUpdateItem>>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, IEnumerable<InventoryUpdateItem>, CancellationToken>((m, _, items, _) =>
            {
                capturedMerchant = m;
                capturedItems = items.ToList();
            })
            .ReturnsAsync(new InventoryUpdateResult { Success = true });

        var processor = new DefaultOutboxMessageProcessor(
            new Mock<IHttpClientFactory>().Object, client.Object, NullLogger<DefaultOutboxMessageProcessor>.Instance);

        var payload = new Merchant360InventoryBatchOutboxPayload
        {
            MerchantId = 10,
            TradingPartnerId = 3,
            Items = new List<InventoryUpdateItem>
            {
                new("SKU-1", 5, 0, null, "Available", null)
            }
        };
        var message = new OutboxMessage
        {
            MessageType = Merchant360OutboxMessageTypes.InventoryBatch,
            Payload = JsonSerializer.Serialize(payload, CamelCase)
        };

        await processor.ProcessAsync(message);

        capturedMerchant.Should().Be(10);
        capturedItems.Should().ContainSingle(i => i.StockNumber == "SKU-1");
    }

    // ---- Inventory snapshot apply enqueues incremental callbacks per merchant ----------------

    [Fact]
    public async Task ApplySnapshot_EnqueuesIncrementalInventoryCallbackPerSubscribedMerchant()
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

        var enqueued = new List<Merchant360InventoryBatchOutboxPayload>();
        var outbox = new Mock<IOutboxService>();
        outbox.Setup(o => o.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<Merchant360InventoryBatchOutboxPayload>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Merchant360InventoryBatchOutboxPayload, string?, string?, int, CancellationToken>(
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

        // One callback per active merchant (2), each carrying all 3 changed items in one chunk.
        enqueued.Should().HaveCount(2);
        enqueued.Select(p => p.MerchantId).Should().BeEquivalentTo(new[] { 10, 20 });
        enqueued.Should().OnlyContain(p => p.Items.Count == 3 && p.TradingPartnerId == 3);
    }
}
