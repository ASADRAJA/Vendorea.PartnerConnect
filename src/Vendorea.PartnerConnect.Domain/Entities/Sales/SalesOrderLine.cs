namespace Vendorea.PartnerConnect.Domain.Entities.Sales;

/// <summary>
/// One line of a marketplace order.
/// </summary>
/// <remarks>
/// Identity is (<see cref="SalesOrderId"/>, <see cref="ExternalLineId"/>), enforced by a unique
/// index, so a re-polled order updates its lines rather than duplicating them.
/// </remarks>
public class SalesOrderLine
{
    /// <summary>
    /// Left unset until EF assigns it.
    /// </summary>
    /// <remarks>
    /// Deliberately not initialised to <c>Guid.NewGuid()</c>. Lines are added to the navigation
    /// collection of an order that is already tracked, and EF decides Added-versus-Modified for
    /// such an entity by whether its key is set. A pre-populated key makes a brand-new line look
    /// like an existing one, and EF issues an UPDATE that matches no row — the insert is lost
    /// and the save throws a concurrency error that says nothing about the real cause.
    /// </remarks>
    public Guid Id { get; set; }

    public Guid SalesOrderId { get; set; }

    /// <summary>Provider line id, unique within the order.</summary>
    public string ExternalLineId { get; set; } = string.Empty;

    public string? SellerSku { get; set; }
    public string? Asin { get; set; }
    public string? Title { get; set; }

    public int QuantityOrdered { get; set; }
    public int QuantityShipped { get; set; }

    public MappingStatus MappingStatus { get; set; } = MappingStatus.UnmappedProduct;

    /// <summary>
    /// The Merchant360 product this line resolves to, or null.
    /// </summary>
    /// <remarks>
    /// Null when unmapped, and PartnerConnect never invents one. Merchant360 is authoritative for
    /// product identity; a guessed mapping here would decrement the wrong inventory and ship the
    /// wrong item, which is worse than refusing to guess.
    /// </remarks>
    public string? M360CatalogItemId { get; set; }

    public decimal? ItemPrice { get; set; }
    public decimal? ItemTax { get; set; }
    public decimal? ShippingPrice { get; set; }
    public decimal? PromotionDiscount { get; set; }

    /// <summary>ISO-4217, matching the order's currency.</summary>
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// The provider's financial breakdown, preserved as returned.
    /// </summary>
    /// <remarks>
    /// Stored whole rather than decomposed into columns. Line totals must not be assumed to sum
    /// to proceeds — fees, refunds and settlements break that identity — so any column set that
    /// looked like it balanced would be lying.
    /// </remarks>
    public string? ProceedsRawJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public SalesOrder? Order { get; set; }
}
