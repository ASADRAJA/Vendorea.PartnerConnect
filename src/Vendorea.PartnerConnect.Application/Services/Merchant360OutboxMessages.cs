using Vendorea.PartnerConnect.Contracts.Interfaces;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Outbox message-type discriminators for direct PC → Merchant360 lifecycle callbacks.
/// Delivered reliably through the existing OutboxMessage pipeline (retry/backoff/last-error +
/// background worker) rather than a generic webhook subscription system.
/// </summary>
public static class Merchant360OutboxMessageTypes
{
    public const string OrderStatus = "Merchant360OrderStatus";
    public const string Shipment = "Merchant360Shipment";
    public const string Invoice = "Merchant360Invoice";
    public const string InventorySnapshot = "Merchant360InventorySnapshot";
}

/// <summary>Outbox envelope for a Merchant360 order status callback (POST …/orders/status).</summary>
public record Merchant360OrderStatusOutboxPayload
{
    public int MerchantId { get; init; }
    public OrderStatusUpdateRequest Request { get; init; } = new();
}

/// <summary>Outbox envelope for a Merchant360 shipment callback (POST …/shipments).</summary>
public record Merchant360ShipmentOutboxPayload
{
    public int MerchantId { get; init; }
    public ShipmentUpdateRequest Request { get; init; } = new();
}

/// <summary>Outbox envelope for a Merchant360 invoice/credit callback (POST …/invoices).</summary>
public record Merchant360InvoiceOutboxPayload
{
    public int MerchantId { get; init; }
    public InvoiceUpdateRequest Request { get; init; } = new();
}

/// <summary>
/// Outbox envelope for a Merchant360 inventory snapshot-applied notification
/// (POST …/inventory/snapshot — lightweight summary counts).
/// </summary>
public record Merchant360InventorySnapshotOutboxPayload
{
    public int MerchantId { get; init; }
    public SupplierInventorySnapshotNotificationRequest Request { get; init; } = new();
}
