using Vendorea.PartnerConnect.Contracts.Interfaces;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Outbox message-type discriminators for direct PC → Merchant360 lifecycle callbacks.
/// These are delivered reliably through the existing OutboxMessage pipeline (retry/backoff/
/// last-error + background worker) rather than a generic webhook subscription system.
/// </summary>
public static class Merchant360OutboxMessageTypes
{
    public const string OrderStatus = "Merchant360OrderStatus";
    public const string InventoryBatch = "Merchant360InventoryBatch";
}

/// <summary>
/// Outbox envelope for a Merchant360 order status callback. Carries the merchant id (route
/// scope) and the canonical request, so the drain step can replay the exact call.
/// </summary>
public record Merchant360OrderStatusOutboxPayload
{
    public int MerchantId { get; init; }
    public OrderStatusUpdateRequest Request { get; init; } = new();
}

/// <summary>
/// Outbox envelope for an incremental Merchant360 inventory batch callback
/// (POST /merchants/{merchantId}/inventory/batch). One message per merchant per chunk.
/// </summary>
public record Merchant360InventoryBatchOutboxPayload
{
    public int MerchantId { get; init; }
    public int TradingPartnerId { get; init; }
    public List<InventoryUpdateItem> Items { get; init; } = new();
}
