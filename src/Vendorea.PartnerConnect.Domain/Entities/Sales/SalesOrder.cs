namespace Vendorea.PartnerConnect.Domain.Entities.Sales;

/// <summary>
/// One marketplace order, as PartnerConnect holds it.
/// </summary>
/// <remarks>
/// Deliberately not a supplier <c>Order</c>. A supplier order is something PartnerConnect sends
/// outward on a dealer's behalf; this is something a marketplace sends inward and a merchant must
/// fulfil. They share a word and nothing else — the lifecycle, the counterparty and the direction
/// of money all differ — and reusing the supplier entity would have forced one state machine to
/// mean two things.
///
/// The identity that matters is (<see cref="IntegrationConnectionId"/>,
/// <see cref="ExternalOrderId"/>), enforced by a unique index. That pairing is what guarantees
/// one Amazon order becomes exactly one Merchant360 sales order no matter how many times it is
/// polled, pushed by notification, or re-read after a reconnect.
///
/// Provider values are kept verbatim alongside the normalised ones. A status we do not recognise
/// becomes <see cref="SalesOrderStatus.Unknown"/> and keeps its raw text, because discarding what
/// the provider actually said leaves nobody able to work out what went wrong.
/// </remarks>
public class SalesOrder
{
    /// <summary>The <c>pcSalesOrderId</c> of the contract.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid IntegrationConnectionId { get; set; }

    /// <summary>
    /// Owning tenant, denormalised from the connection.
    /// </summary>
    /// <remarks>
    /// Carried here so every read can be tenant-scoped without a join, and so isolation is a
    /// column on the row rather than a property of whichever query happens to be running.
    /// </remarks>
    public int TenantId { get; set; }

    /// <summary>Provider order id. Unique within the connection.</summary>
    public string ExternalOrderId { get; set; } = string.Empty;

    public string? MarketplaceId { get; set; }

    /// <summary>PC-normalised status. Never the provider's own vocabulary.</summary>
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Unknown;

    /// <summary>The provider's value, verbatim and uninterpreted.</summary>
    public string? ExternalStatusRaw { get; set; }

    public string? ExternalFulfillmentStatusRaw { get; set; }

    public FulfillmentChannel FulfillmentChannel { get; set; }

    public MappingStatus MappingStatus { get; set; } = MappingStatus.UnmappedProduct;

    /// <summary>
    /// The provider's <c>programs[]</c> as a JSON array, verbatim.
    /// </summary>
    /// <remarks>
    /// Prime, Business and replacement indicators are derived from this on read, never stored as
    /// booleans. Amazon adds programs over time; a boolean column per programme would need a
    /// migration each time and would quietly answer "false" for programmes that did not exist
    /// when the column was added.
    /// </remarks>
    public string? ProgramsRawJson { get; set; }

    public DateTime PurchaseDate { get; set; }

    /// <summary>
    /// The provider's last-modified timestamp.
    /// </summary>
    /// <remarks>
    /// Updates apply monotonically on this value: an arriving snapshot older than the stored one
    /// is discarded. Polling and notifications overlap by design, so out-of-order delivery is
    /// routine rather than exceptional, and without this guard a slow poll response would
    /// overwrite a newer notification.
    /// </remarks>
    public DateTime SourceLastUpdatedAt { get; set; }

    public string? SourceVersion { get; set; }

    public decimal? OrderTotalAmount { get; set; }

    /// <summary>ISO-4217. One currency per order; the marketplace decides it.</summary>
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Whether the provider supplied buyer and address data.
    /// </summary>
    /// <remarks>
    /// False is a normal state, not a failure. Amazon legitimately withholds buyer data on
    /// PENDING orders, and treating that as an integration error produces a permanent stream of
    /// alerts about orders that are behaving exactly as documented.
    /// </remarks>
    public bool BuyerDataAvailable { get; set; }

    /// <summary>Provider-anonymised buyer address where one is supplied.</summary>
    public string? BuyerEmailRef { get; set; }

    public string? BuyerName { get; set; }

    public string? ShipToName { get; set; }
    public string? ShipToLine1 { get; set; }
    public string? ShipToLine2 { get; set; }
    public string? ShipToCity { get; set; }
    public string? ShipToStateOrRegion { get; set; }
    public string? ShipToPostalCode { get; set; }
    public string? ShipToCountryCode { get; set; }

    /// <summary>
    /// When the personal fields above were cleared under the retention policy.
    /// </summary>
    /// <remarks>
    /// Recorded rather than inferred from the fields being null, because "never supplied" and
    /// "supplied and since erased" are different facts and only one of them answers a
    /// data-subject request.
    /// </remarks>
    public DateTime? PiiRedactedAt { get; set; }

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The most recent event published to Merchant360 for this order.</summary>
    public Guid? LastEventId { get; set; }

    public SalesOrderProcessingResult? LastProcessingResult { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// The connection this order came through.
    /// </summary>
    /// <remarks>
    /// Loaded only where it is needed — the read API projects the provider code from it. The
    /// ingestion path never includes it, so the ordinary write never drags a connection and its
    /// collections along behind every order.
    /// </remarks>
    public Integration.IntegrationConnection? Connection { get; set; }

    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
    public ICollection<SalesOrderStatusHistory> StatusHistory { get; set; } = new List<SalesOrderStatusHistory>();

    /// <summary>True when the arriving snapshot is newer than what is stored.</summary>
    public bool IsNewerThanStored(DateTime incomingSourceLastUpdatedAt) =>
        incomingSourceLastUpdatedAt > SourceLastUpdatedAt;

    /// <summary>Clears every personal field and records that it happened.</summary>
    public void RedactPii(DateTime redactedAtUtc)
    {
        BuyerEmailRef = null;
        BuyerName = null;
        ShipToName = null;
        ShipToLine1 = null;
        ShipToLine2 = null;
        ShipToCity = null;
        ShipToStateOrRegion = null;
        ShipToPostalCode = null;
        // Country is retained: it is not identifying on its own and tax and reporting need it.
        PiiRedactedAt = redactedAtUtc;
    }
}

/// <summary>PC-normalised, provider-neutral order status.</summary>
public enum SalesOrderStatus
{
    /// <summary>Placed but not payable or actionable yet; buyer data is often withheld here.</summary>
    Pending,
    ReadyToFulfill,
    PartiallyFulfilled,
    Fulfilled,
    Cancelled,
    OnHold,

    /// <summary>
    /// The provider sent a status we do not map. The raw value is preserved and an operator
    /// signal is raised; the order is never silently treated as one of the known states.
    /// </summary>
    Unknown
}

public enum FulfillmentChannel
{
    /// <summary>The merchant ships it. Phase 5 acts on these.</summary>
    MerchantFulfilled,

    /// <summary>Amazon ships it. PartnerConnect observes but never confirms shipment.</summary>
    AmazonFulfilled
}

/// <summary>Whether every line resolves to a Merchant360 product.</summary>
public enum MappingStatus
{
    Mapped,
    UnmappedProduct,
    Conflict
}

/// <summary>What Merchant360 reported back about its own processing.</summary>
public enum SalesOrderProcessingResult
{
    Processed,
    FailedRetryable,
    FailedPermanent
}
