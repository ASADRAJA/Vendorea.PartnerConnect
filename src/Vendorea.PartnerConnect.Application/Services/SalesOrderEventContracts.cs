using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// The published event envelope, exactly as the contract schema defines it.
/// </summary>
/// <remarks>
/// Hand-written rather than generated, and deliberately provider-neutral: no Amazon type, enum or
/// field name appears anywhere in it. Property names are lowerCamelCase on the wire to match the
/// schema, set explicitly rather than by a serializer convention — a convention applies to
/// everything in the process and this contract has to hold even if that convention changes.
/// </remarks>
public class IntegrationEventEnvelope
{
    [JsonPropertyName("eventId")] public Guid EventId { get; set; }
    [JsonPropertyName("eventType")] public string EventType { get; set; } = string.Empty;
    [JsonPropertyName("eventVersion")] public string EventVersion { get; set; } = "v1";
    [JsonPropertyName("occurredAt")] public DateTime OccurredAt { get; set; }
    [JsonPropertyName("publishedAt")] public DateTime PublishedAt { get; set; }
    [JsonPropertyName("organizationId")] public int OrganizationId { get; set; }
    [JsonPropertyName("tenantId")] public int TenantId { get; set; }
    [JsonPropertyName("integrationConnectionId")] public Guid IntegrationConnectionId { get; set; }
    [JsonPropertyName("provider")] public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("marketplaceId")] public string? MarketplaceId { get; set; }
    [JsonPropertyName("correlationId")] public string CorrelationId { get; set; } = string.Empty;
    [JsonPropertyName("deduplicationKey")] public string DeduplicationKey { get; set; } = string.Empty;

    /// <summary>v1 publishes full snapshots only. A consumer that misses one converges on the next.</summary>
    [JsonPropertyName("payloadKind")] public string PayloadKind { get; set; } = "snapshot";

    [JsonPropertyName("publicationReason")] public string PublicationReason { get; set; } = "ProviderChange";

    [JsonPropertyName("payload")] public JsonNode? Payload { get; set; }
}

/// <summary>
/// The canonical key: exactly one consumer sales order per tuple, for the life of the order.
/// </summary>
public class OrderIdentityDto
{
    [JsonPropertyName("provider")] public string Provider { get; set; } = string.Empty;
    [JsonPropertyName("integrationConnectionId")] public Guid IntegrationConnectionId { get; set; }
    [JsonPropertyName("externalOrderId")] public string ExternalOrderId { get; set; } = string.Empty;
}

/// <summary>Money as a decimal string. Never a JSON number — float rounding corrupts money.</summary>
public class EventMoneyDto
{
    [JsonPropertyName("amount")] public string Amount { get; set; } = "0.00";
    [JsonPropertyName("currency")] public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Formats to the schema's pattern: at least two decimal places, at most four.
    /// </summary>
    /// <remarks>
    /// The schema requires <c>^-?[0-9]+\.[0-9]{2,4}$</c>, so a whole number has to carry its
    /// cents. An amount of 12 published as "12" fails validation at the consumer, and a
    /// contract violation discovered there is a delivery that cannot be retried into working.
    /// </remarks>
    public static EventMoneyDto? From(decimal? amount, string? currency)
    {
        if (amount is null || string.IsNullOrEmpty(currency))
        {
            return null;
        }

        var rounded = decimal.Round(amount.Value, 4, MidpointRounding.ToEven);
        var text = rounded.ToString("0.00##", System.Globalization.CultureInfo.InvariantCulture);

        return new EventMoneyDto { Amount = text, Currency = currency };
    }
}

/// <summary>The full order snapshot carried by created and updated events.</summary>
public class SalesOrderSnapshotDto
{
    [JsonPropertyName("identity")] public OrderIdentityDto Identity { get; set; } = new();
    [JsonPropertyName("pcSalesOrderId")] public Guid PcSalesOrderId { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("externalStatusRaw")] public string ExternalStatusRaw { get; set; } = string.Empty;
    [JsonPropertyName("externalFulfillmentStatusRaw")] public string? ExternalFulfillmentStatusRaw { get; set; }
    [JsonPropertyName("fulfillmentChannel")] public string FulfillmentChannel { get; set; } = string.Empty;
    [JsonPropertyName("mappingStatus")] public string MappingStatus { get; set; } = string.Empty;
    [JsonPropertyName("programsRaw")] public List<string> ProgramsRaw { get; set; } = new();
    [JsonPropertyName("purchaseDate")] public DateTime PurchaseDate { get; set; }
    [JsonPropertyName("sourceLastUpdatedAt")] public DateTime SourceLastUpdatedAt { get; set; }
    [JsonPropertyName("sourceVersion")] public string? SourceVersion { get; set; }
    [JsonPropertyName("orderTotal")] public EventMoneyDto? OrderTotal { get; set; }
    [JsonPropertyName("buyerDataAvailable")] public bool BuyerDataAvailable { get; set; }

    /// <summary>
    /// Least-necessary PII, and omitted entirely unless the consumer needs it for a capability
    /// whose role was granted.
    /// </summary>
    [JsonPropertyName("buyer")] public EventBuyerDto? Buyer { get; set; }

    [JsonPropertyName("shipTo")] public EventAddressDto? ShipTo { get; set; }

    [JsonPropertyName("lines")] public List<SalesOrderLineSnapshotDto> Lines { get; set; } = new();
}

public class EventBuyerDto
{
    [JsonPropertyName("emailRef")] public string? EmailRef { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public class EventAddressDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("line1")] public string? Line1 { get; set; }
    [JsonPropertyName("line2")] public string? Line2 { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("stateOrRegion")] public string? StateOrRegion { get; set; }
    [JsonPropertyName("postalCode")] public string? PostalCode { get; set; }
    [JsonPropertyName("countryCode")] public string? CountryCode { get; set; }
}

public class SalesOrderLineSnapshotDto
{
    [JsonPropertyName("externalLineId")] public string ExternalLineId { get; set; } = string.Empty;
    [JsonPropertyName("sellerSku")] public string? SellerSku { get; set; }
    [JsonPropertyName("asin")] public string? Asin { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("quantityOrdered")] public int QuantityOrdered { get; set; }
    [JsonPropertyName("quantityShipped")] public int QuantityShipped { get; set; }

    /// <summary>
    /// An unmapped line must still create a visible consumer order line for resolution.
    /// </summary>
    [JsonPropertyName("mappingStatus")] public string MappingStatus { get; set; } = string.Empty;

    [JsonPropertyName("m360CatalogItemIdHint")] public string? M360CatalogItemIdHint { get; set; }
    [JsonPropertyName("itemPrice")] public EventMoneyDto? ItemPrice { get; set; }
    [JsonPropertyName("itemTax")] public EventMoneyDto? ItemTax { get; set; }
    [JsonPropertyName("shippingPrice")] public EventMoneyDto? ShippingPrice { get; set; }
    [JsonPropertyName("promotionDiscount")] public EventMoneyDto? PromotionDiscount { get; set; }
}

/// <summary>What Merchant360 reports back after its handler runs.</summary>
public class ProcessingResultDto
{
    [JsonPropertyName("sourceEventId")] public Guid SourceEventId { get; set; }
    [JsonPropertyName("outcome")] public string Outcome { get; set; } = string.Empty;
    [JsonPropertyName("m360SalesOrderId")] public string? M360SalesOrderId { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("attemptedAt")] public DateTime? AttemptedAt { get; set; }
}
