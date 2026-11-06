namespace Vendorea.PartnerConnect.Domain.Entities.Integration;

/// <summary>
/// What a provider can do, and what a given connection was actually granted.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="PartnerCapability"/>, which is supplier-shaped
/// (PriceFeed, InvoiceReceive…) and describes the SPR-style pipeline. Marketplace selling and
/// procurement are different directions of trade, and overloading one enum to cover both would
/// make every consumer check which kind it was holding.
///
/// Note that the two Amazon capabilities share no flag: Selling and Business are independent
/// programmes that happen to use the same substrate.
/// </remarks>
[Flags]
public enum IntegrationCapabilities
{
    None = 0,

    /// <summary>Inbound marketplace demand (Amazon SP-API orders).</summary>
    SalesOrderIngest = 1 << 0,

    /// <summary>Outbound shipment confirmation for merchant-fulfilled orders.</summary>
    FulfillmentPush = 1 << 1,

    /// <summary>Outbound listing, price and inventory publication.</summary>
    ListingSync = 1 << 2,

    /// <summary>Inbound settlements and financial events.</summary>
    FinanceIngest = 1 << 3,

    /// <summary>Outbound purchase orders (Amazon Business).</summary>
    PurchaseOrderPush = 1 << 4,

    /// <summary>Inbound purchase-order status and shipment notices.</summary>
    PurchaseOrderStatusIngest = 1 << 5,

    /// <summary>Live catalogue search against the provider.</summary>
    CatalogSearch = 1 << 6
}
