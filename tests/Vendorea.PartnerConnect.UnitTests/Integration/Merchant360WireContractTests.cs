using System.Text.Json;
using FluentAssertions;
using Vendorea.PartnerConnect.Contracts.Interfaces;

namespace Vendorea.PartnerConnect.UnitTests.Integration;

/// <summary>
/// Contract-style tests: the callback request DTOs must serialize to the exact field names and
/// JSON types Merchant360's inbound DTOs expect. Uses the same Web (camelCase) serializer the
/// HTTP client (PostAsJsonAsync) uses on the wire.
/// </summary>
public class Merchant360WireContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static JsonElement Serialize(object request) =>
        JsonDocument.Parse(JsonSerializer.Serialize(request, Web)).RootElement;

    [Fact]
    public void OrderStatus_SerializesToM360FieldNamesAndStringStatus()
    {
        var json = Serialize(new OrderStatusUpdateRequest
        {
            EventId = "evt-1",
            PartnerConnectOrderId = 7,
            CorrelationId = "corr",
            ExternalOrderId = "M360-1",
            Status = "Failed",
            PartnerOrderNumber = "SO-1",
            OccurredAt = DateTime.UtcNow,
            ErrorCode = "SPR_ERROR_ACK",
            FailureReason = "bad stock"
        });

        foreach (var field in new[] { "eventId", "partnerConnectOrderId", "correlationId", "externalOrderId",
                     "status", "partnerOrderNumber", "occurredAt", "errorCode", "failureReason" })
            json.TryGetProperty(field, out _).Should().BeTrue($"M360 expects '{field}'");

        // status must be a string, NOT a numeric enum; no legacy statusType field.
        json.GetProperty("status").ValueKind.Should().Be(JsonValueKind.String);
        json.GetProperty("status").GetString().Should().Be("Failed");
        json.TryGetProperty("statusType", out _).Should().BeFalse();
    }

    [Fact]
    public void Shipment_SerializesToEnvelopeWithShipmentsArray()
    {
        var json = Serialize(new ShipmentUpdateRequest
        {
            EventId = "evt-2",
            PartnerConnectOrderId = 9,
            IsComplete = false,
            Shipments = new List<ShipmentDto>
            {
                new()
                {
                    ShipmentId = "MAN-1",
                    Carrier = "UPS",
                    TrackingNumber = "1Z",
                    ShippedAt = DateTime.UtcNow,
                    EstimatedDelivery = "2026-06-10",
                    Lines = new List<ShipmentLineDto> { new() { LineNumber = 1, VendorSku = "SKU-1", QuantityShipped = 5 } }
                }
            }
        });

        json.TryGetProperty("eventId", out _).Should().BeTrue();
        json.GetProperty("shipments").ValueKind.Should().Be(JsonValueKind.Array);
        var ship = json.GetProperty("shipments")[0];
        foreach (var field in new[] { "shipmentId", "carrier", "trackingNumber", "shippedAt", "estimatedDelivery", "lines" })
            ship.TryGetProperty(field, out _).Should().BeTrue($"M360 PCShipmentDto expects '{field}'");
        var line = ship.GetProperty("lines")[0];
        line.TryGetProperty("vendorSku", out _).Should().BeTrue();
        line.TryGetProperty("quantityShipped", out _).Should().BeTrue();
    }

    [Fact]
    public void Invoice_SerializesToM360FieldNames()
    {
        var json = Serialize(new InvoiceUpdateRequest
        {
            EventId = "evt-3",
            PartnerConnectOrderId = 9,
            InvoiceNumber = "INV-1",
            DocumentType = "CreditMemo",
            Currency = "USD",
            Subtotal = 10m,
            Tax = 1m,
            Shipping = 2m,
            Total = 13m,
            Lines = new List<InvoiceLineDto> { new() { LineNumber = 1, VendorSku = "SKU-1", LineTotal = 10m } }
        });

        foreach (var field in new[] { "eventId", "invoiceNumber", "documentType", "subtotal", "tax", "shipping", "total" })
            json.TryGetProperty(field, out _).Should().BeTrue($"M360 expects '{field}'");
        // No legacy names.
        json.TryGetProperty("subTotal", out _).Should().BeFalse();
        json.TryGetProperty("totalAmount", out _).Should().BeFalse();
        var line = json.GetProperty("lines")[0];
        line.TryGetProperty("vendorSku", out _).Should().BeTrue();
        line.TryGetProperty("lineTotal", out _).Should().BeTrue();
    }

    [Fact]
    public void InventorySnapshot_SerializesIntegerSnapshotIdAndRenamedFields()
    {
        var json = Serialize(new SupplierInventorySnapshotNotificationRequest
        {
            EventId = "evt-4",
            TradingPartnerId = 3,
            SnapshotId = 55,
            ItemCount = 100,
            GeneratedAt = DateTime.UtcNow
        });

        foreach (var field in new[] { "eventId", "tradingPartnerId", "snapshotId", "itemCount", "generatedAt" })
            json.TryGetProperty(field, out _).Should().BeTrue($"M360 expects '{field}'");

        // snapshotId must be an integer (M360 binds int?), not a string.
        json.GetProperty("snapshotId").ValueKind.Should().Be(JsonValueKind.Number);
        json.GetProperty("snapshotId").GetInt32().Should().Be(55);
        json.TryGetProperty("totalItemCount", out _).Should().BeFalse();
    }
}
