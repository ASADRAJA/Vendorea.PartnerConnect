using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// The operator's view of marketplace order ingestion — backs the admin portal's
/// "Marketplace Orders" page.
/// </summary>
/// <remarks>
/// Separate from the org-facing <c>/api/v1/org/tenants/{tenantId}/sales-orders</c>, which is the
/// published contract Merchant360 consumes and is tenant-scoped by design. This one reads across
/// tenants, because the question an operator asks is "is ingestion working?" rather than "what
/// are this dealer's orders?", and it is reachable only with an admin key.
///
/// Unlike the org-facing API, this one **does** expose whether buyer data is present and the
/// shipping city — an operator diagnosing why an order will not ship needs to know whether the
/// address arrived. It still never returns the street, the name or the email: knowing that the
/// data is there is a different thing from reading it.
/// </remarks>
[ApiController]
[Route("api/admin/marketplace-orders")]
public class AdminMarketplaceOrdersController : ControllerBase
{
    private readonly ISalesOrderAdminRepository _orders;

    public AdminMarketplaceOrdersController(ISalesOrderAdminRepository orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int? organizationId,
        [FromQuery] int? tenantId,
        [FromQuery] Guid? connectionId,
        [FromQuery] string? status,
        [FromQuery] string? mappingStatus,
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var page = await _orders.SearchAsync(
            new SalesOrderAdminQuery
            {
                OrganizationId = organizationId,
                TenantId = tenantId,
                ConnectionId = connectionId,
                Status = ParseOrNull<SalesOrderStatus>(status),
                MappingStatus = ParseOrNull<MappingStatus>(mappingStatus),
                Search = search,
                Skip = skip,
                Take = take
            },
            cancellationToken);

        return Ok(new
        {
            items = page.Items.Select(MapRow).ToList(),
            total = page.Total,
            skip = page.Skip,
            take = page.Take,
            unmappedCount = page.UnmappedCount,

            // Keyed by name so the portal does not depend on enum ordinals.
            countsByStatus = page.CountsByStatus.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orders.GetAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            row = MapRow(order),
            lines = order.Lines.OrderBy(l => l.ExternalLineId).Select(l => new
            {
                externalLineId = l.ExternalLineId,
                sellerSku = l.SellerSku,
                asin = l.Asin,
                title = l.Title,
                quantityOrdered = l.QuantityOrdered,
                quantityShipped = l.QuantityShipped,
                mappingStatus = l.MappingStatus.ToString(),
                m360CatalogItemId = l.M360CatalogItemId,
                itemPrice = l.ItemPrice,
                itemTax = l.ItemTax,
                shippingPrice = l.ShippingPrice,
                promotionDiscount = l.PromotionDiscount,
                currencyCode = l.CurrencyCode
            }).ToList(),
            history = order.StatusHistory.OrderBy(h => h.RecordedAt).Select(h => new
            {
                fromStatus = h.FromStatus.ToString(),
                toStatus = h.ToStatus.ToString(),
                externalStatusRaw = h.ExternalStatusRaw,
                sourceLastUpdatedAt = h.SourceLastUpdatedAt,
                recordedAt = h.RecordedAt,
                source = h.Source
            }).ToList()
        });
    }

    private static object MapRow(SalesOrder o) => new
    {
        id = o.Id,
        tenantId = o.TenantId,
        connectionId = o.IntegrationConnectionId,
        provider = o.Connection?.Provider?.Code,
        sellerAccountId = o.Connection?.ExternalAccountId,
        externalOrderId = o.ExternalOrderId,
        marketplaceId = o.MarketplaceId,
        status = o.Status.ToString(),
        externalStatusRaw = o.ExternalStatusRaw,
        fulfillmentChannel = o.FulfillmentChannel.ToString(),
        mappingStatus = o.MappingStatus.ToString(),
        purchaseDate = o.PurchaseDate,
        sourceLastUpdatedAt = o.SourceLastUpdatedAt,
        firstSeenAt = o.FirstSeenAt,
        lastSyncedAt = o.LastSyncedAt,
        orderTotalAmount = o.OrderTotalAmount,
        currencyCode = o.CurrencyCode,
        lineCount = o.Lines.Count,

        // Enough to diagnose a non-shipping order without disclosing the recipient. "Is there an
        // address?" and "what is the address?" are different questions, and an operator view only
        // needs the first.
        buyerDataAvailable = o.BuyerDataAvailable,
        hasShippingAddress = !string.IsNullOrEmpty(o.ShipToLine1),
        shipToCity = o.ShipToCity,
        shipToStateOrRegion = o.ShipToStateOrRegion,
        shipToCountryCode = o.ShipToCountryCode,
        piiRedactedAt = o.PiiRedactedAt
    };

    private static TEnum? ParseOrNull<TEnum>(string? value) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;
}
