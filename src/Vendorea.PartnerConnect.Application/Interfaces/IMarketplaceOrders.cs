using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Reads orders from a marketplace, in PartnerConnect's own vocabulary.
/// </summary>
/// <remarks>
/// Everything crossing this interface is provider-neutral. No Amazon type, enum or field name
/// appears in the results, so the ingestion pipeline above it never learns which marketplace it
/// is talking to — which is what will make a second marketplace an adapter rather than a rewrite.
/// </remarks>
public interface IMarketplaceOrdersClient
{
    /// <summary>
    /// One page of orders changed within a window.
    /// </summary>
    /// <remarks>
    /// Deliberately returns a page, not an enumerable of every order. The provider's rate limit
    /// makes each page an expensive, individually schedulable unit of work, and hiding paging
    /// behind an iterator would make the caller unable to stop between pages or to persist
    /// progress when it does.
    /// </remarks>
    Task<MarketplaceOrderPage> SearchOrdersAsync(
        int tenantId, Guid connectionId, MarketplaceOrderQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>One order by its provider id.</summary>
    Task<MarketplaceOrderResult> GetOrderAsync(
        int tenantId, Guid connectionId, string externalOrderId,
        CancellationToken cancellationToken = default);
}

/// <summary>What to search for. Exactly one of the "after" bounds must be set.</summary>
public sealed record MarketplaceOrderQuery
{
    /// <summary>Orders changed since this instant. The normal incremental query.</summary>
    public DateTime? LastUpdatedAfter { get; init; }

    /// <summary>Orders created since this instant. Used for a first backfill.</summary>
    public DateTime? CreatedAfter { get; init; }

    public DateTime? LastUpdatedBefore { get; init; }
    public DateTime? CreatedBefore { get; init; }

    public IReadOnlyList<string>? MarketplaceIds { get; init; }

    public int? MaxResultsPerPage { get; init; }

    /// <summary>
    /// Continuation from a previous page.
    /// </summary>
    /// <remarks>
    /// Expires 24 hours after it is issued. Every other value in the follow-up request must match
    /// the request that produced it.
    /// </remarks>
    public string? PaginationToken { get; init; }

    public bool IsValid =>
        (LastUpdatedAfter.HasValue ^ CreatedAfter.HasValue) || PaginationToken is not null;
}

/// <summary>Why a provider call did not return data.</summary>
public enum MarketplaceCallOutcome
{
    Ok,

    /// <summary>Throttled. Retry after a delay; nothing is wrong.</summary>
    RateLimited,

    /// <summary>
    /// The continuation token is no longer valid — almost always the 24-hour expiry.
    /// </summary>
    /// <remarks>
    /// Distinguished from other bad requests because the recovery differs: the walk restarts from
    /// the last persisted watermark rather than failing. Treating it as a generic error would
    /// strand a long backfill permanently.
    /// </remarks>
    PaginationTokenExpired,

    /// <summary>The access token was rejected. The connection may need reauthorization.</summary>
    Unauthorized,

    /// <summary>Authenticated, but the application lacks the role for what was asked.</summary>
    Forbidden,

    /// <summary>A network fault or a provider 5xx. Retry.</summary>
    Transient,

    /// <summary>A request the provider will never accept. Retrying cannot help.</summary>
    Permanent
}

public sealed record MarketplaceOrderPage(
    MarketplaceCallOutcome Outcome,
    IReadOnlyList<MarketplaceOrderSnapshot> Orders,
    string? NextToken,
    /// <summary>
    /// The provider's own upper bound for this page set.
    /// </summary>
    /// <remarks>
    /// The correct watermark, and not the same thing as the newest row returned. An order
    /// modified while the walk was in progress may never appear on any page; advancing to the
    /// maximum timestamp seen would skip it for ever, whereas advancing to the boundary the
    /// provider declared will not.
    /// </remarks>
    DateTime? Boundary,
    string? Error = null,
    TimeSpan? RetryAfter = null)
{
    public bool Success => Outcome == MarketplaceCallOutcome.Ok;

    public static MarketplaceOrderPage Ok(
        IReadOnlyList<MarketplaceOrderSnapshot> orders, string? nextToken, DateTime? boundary)
        => new(MarketplaceCallOutcome.Ok, orders, nextToken, boundary);

    public static MarketplaceOrderPage Failed(
        MarketplaceCallOutcome outcome, string error, TimeSpan? retryAfter = null)
        => new(outcome, Array.Empty<MarketplaceOrderSnapshot>(), null, null, error, retryAfter);
}

public sealed record MarketplaceOrderResult(
    MarketplaceCallOutcome Outcome,
    MarketplaceOrderSnapshot? Order,
    string? Error = null,
    TimeSpan? RetryAfter = null)
{
    public bool Success => Outcome == MarketplaceCallOutcome.Ok && Order is not null;

    public static MarketplaceOrderResult Ok(MarketplaceOrderSnapshot order)
        => new(MarketplaceCallOutcome.Ok, order);

    public static MarketplaceOrderResult Failed(
        MarketplaceCallOutcome outcome, string error, TimeSpan? retryAfter = null)
        => new(outcome, null, error, retryAfter);
}

/// <summary>One order as the provider currently describes it, in canonical form.</summary>
public sealed record MarketplaceOrderSnapshot
{
    public required string ExternalOrderId { get; init; }
    public string? MarketplaceId { get; init; }

    public SalesOrderStatus Status { get; init; } = SalesOrderStatus.Unknown;

    /// <summary>The provider's status string, verbatim — kept even when it maps cleanly.</summary>
    public string? ExternalStatusRaw { get; init; }

    public FulfillmentChannel FulfillmentChannel { get; init; }

    /// <summary>The provider's programme list, verbatim. Prime and Business are derived from it.</summary>
    public IReadOnlyList<string> Programs { get; init; } = Array.Empty<string>();

    public DateTime PurchaseDate { get; init; }
    public DateTime SourceLastUpdatedAt { get; init; }

    public decimal? OrderTotalAmount { get; init; }
    public string? CurrencyCode { get; init; }

    /// <summary>False when the provider withheld buyer data. A normal state, not a failure.</summary>
    public bool BuyerDataAvailable { get; init; }

    public string? BuyerEmailRef { get; init; }
    public string? BuyerName { get; init; }

    public MarketplaceAddress? ShipTo { get; init; }

    public IReadOnlyList<MarketplaceOrderLineSnapshot> Lines { get; init; }
        = Array.Empty<MarketplaceOrderLineSnapshot>();
}

public sealed record MarketplaceAddress(
    string? Name, string? Line1, string? Line2, string? City,
    string? StateOrRegion, string? PostalCode, string? CountryCode);

public sealed record MarketplaceOrderLineSnapshot
{
    public required string ExternalLineId { get; init; }
    public string? SellerSku { get; init; }
    public string? Asin { get; init; }
    public string? Title { get; init; }

    public int QuantityOrdered { get; init; }
    public int QuantityShipped { get; init; }

    public decimal? ItemPrice { get; init; }
    public decimal? ItemTax { get; init; }
    public decimal? ShippingPrice { get; init; }
    public decimal? PromotionDiscount { get; init; }
    public string? CurrencyCode { get; init; }

    /// <summary>The provider's financial breakdown as returned, preserved whole.</summary>
    public string? ProceedsRawJson { get; init; }
}
