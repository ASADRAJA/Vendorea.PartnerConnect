using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Money on the wire: a decimal **string** with its currency.
/// </summary>
/// <remarks>
/// A string, not a number, for the same reason Amazon sends it that way — a JSON number is a
/// double to most parsers, and a consumer that reads 24.99 as a float has already lost the value
/// before it does anything with it. The currency travels with the amount because an order's
/// marketplace decides it, not the reader.
/// </remarks>
public class MoneyDto
{
    public string Amount { get; set; } = "0";
    public string Currency { get; set; } = string.Empty;

    public static MoneyDto? From(decimal? amount, string? currency)
        => amount is null ? null : new MoneyDto
        {
            // Invariant culture: the wire format is not the server's locale's business.
            Amount = amount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Currency = currency ?? string.Empty
        };
}

/// <summary>
/// One marketplace sales order.
/// </summary>
/// <remarks>
/// Deliberately carries **no buyer identity and no address**. The order row holds them because a
/// merchant-fulfilled order cannot be shipped without them, but this API is a read surface for
/// operators and for Merchant360's own views — neither needs a buyer's street, and an endpoint
/// that returns PII because it happened to be in the row is how PII ends up in a log, a cache or
/// a screenshot. <see cref="BuyerDataAvailable"/> says whether the data exists without disclosing it.
/// </remarks>
public class SalesOrderDto
{
    public Guid PcSalesOrderId { get; set; }
    public Guid IntegrationConnectionId { get; set; }

    /// <summary>Provider code, e.g. <c>AMAZON_SP</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    public string ExternalOrderId { get; set; } = string.Empty;
    public string? MarketplaceId { get; set; }

    /// <summary>PC-normalised status. Never the provider's own vocabulary.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The provider's value, verbatim — kept even when it maps cleanly.</summary>
    public string? ExternalStatusRaw { get; set; }

    public string? ExternalFulfillmentStatusRaw { get; set; }
    public string FulfillmentChannel { get; set; } = string.Empty;
    public string MappingStatus { get; set; } = string.Empty;

    /// <summary>
    /// The provider's programme list, verbatim.
    /// </summary>
    /// <remarks>
    /// Prime, Business and replacement indicators are derived by the consumer from this. No
    /// <c>isPrime</c> boolean crosses the wire — Amazon adds programmes, and a boolean per
    /// programme would answer "false" for every one that did not exist when it was added.
    /// </remarks>
    public List<string> ProgramsRaw { get; set; } = new();

    public DateTime PurchaseDate { get; set; }

    /// <summary>The provider's timestamp. Updates apply monotonically on this.</summary>
    public DateTime SourceLastUpdatedAt { get; set; }

    public string? SourceVersion { get; set; }
    public MoneyDto? OrderTotal { get; set; }

    /// <summary>
    /// Whether the provider supplied buyer data. False is normal, not a failure.
    /// </summary>
    public bool BuyerDataAvailable { get; set; }
}

/// <summary>One line of a marketplace order.</summary>
public class SalesOrderLineDto
{
    public string ExternalLineId { get; set; } = string.Empty;
    public string? SellerSku { get; set; }
    public string? Asin { get; set; }
    public string? Title { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityShipped { get; set; }
    public string MappingStatus { get; set; } = string.Empty;

    /// <summary>Null when unmapped. PartnerConnect never invents one.</summary>
    public string? M360CatalogItemId { get; set; }

    public MoneyDto? ItemPrice { get; set; }
    public MoneyDto? ItemTax { get; set; }
    public MoneyDto? ShippingPrice { get; set; }
    public MoneyDto? PromotionDiscount { get; set; }

    /// <summary>
    /// The provider's financial breakdown, as returned.
    /// </summary>
    /// <remarks>
    /// Emitted as an object rather than a quoted string, so a consumer reads it as JSON instead
    /// of parsing it twice. Line totals must not be assumed to sum to proceeds — fees, refunds
    /// and settlements break that identity.
    /// </remarks>
    public JsonNode? ProceedsRaw { get; set; }
}

/// <summary>How recently, and how, PartnerConnect last touched this order.</summary>
public class SalesOrderSyncMetadataDto
{
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public Guid? LastEventId { get; set; }
    public string? LastProcessingResult { get; set; }
}

/// <summary>An order with its lines and sync metadata.</summary>
public class SalesOrderDetailDto : SalesOrderDto
{
    public List<SalesOrderLineDto> Lines { get; set; } = new();
    public SalesOrderSyncMetadataDto SyncMetadata { get; set; } = new();
}

/// <summary>A page of orders, with the total so a caller need not ask twice.</summary>
public class SalesOrderPageDto
{
    public List<SalesOrderDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

/// <summary>Per-capability watermarks and recent job failures for one connection.</summary>
public class SyncHealthDto
{
    public Guid ConnectionId { get; set; }
    public List<SyncWatermarkDto> Watermarks { get; set; } = new();
    public List<SyncFailedJobDto> FailedJobs { get; set; } = new();
}

/// <summary>
/// One capability's position on one marketplace.
/// </summary>
/// <remarks>
/// Per capability and per marketplace, never one figure for the connection. Order ingestion and
/// listing sync advance independently, and a single number would report whichever happened to
/// write last as though it spoke for both.
/// </remarks>
public class SyncWatermarkDto
{
    public string Capability { get; set; } = string.Empty;
    public string? MarketplaceId { get; set; }
    public DateTime? Watermark { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public int ConsecutiveFailures { get; set; }

    /// <summary>True while a paged walk is part-way through.</summary>
    public bool WalkInProgress { get; set; }
}

public class SyncFailedJobDto
{
    public string JobKey { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
    public string Error { get; set; } = string.Empty;
}
