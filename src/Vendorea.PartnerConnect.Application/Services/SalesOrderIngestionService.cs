using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Application.Services;

/// <inheritdoc />
public class SalesOrderIngestionService : ISalesOrderIngestionService
{
    private readonly ISalesOrderRepository _orders;
    private readonly ISalesOrderEventPublisher? _publisher;
    private readonly ILogger<SalesOrderIngestionService> _logger;

    public SalesOrderIngestionService(
        ISalesOrderRepository orders,
        ILogger<SalesOrderIngestionService> logger,
        ISalesOrderEventPublisher? publisher = null)
    {
        _orders = orders;
        _logger = logger;

        // Optional so ingestion can be exercised on its own. In every host it is registered, and
        // when it is absent that is logged on each publication rather than passing silently —
        // ingesting orders nobody is told about is the failure this phase exists to prevent.
        _publisher = publisher;
    }

    public async Task<SalesOrderIngestResult> IngestAsync(
        IntegrationConnection connection, MarketplaceOrderSnapshot snapshot, string source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(snapshot.ExternalOrderId))
        {
            return SalesOrderIngestResult.Failed("The snapshot carries no provider order id.");
        }

        var existing = await _orders.FindAsync(
            connection.Id, snapshot.ExternalOrderId, cancellationToken);

        if (existing is null)
        {
            return await CreateAsync(connection, snapshot, source, cancellationToken);
        }

        return await UpdateAsync(existing, connection, snapshot, source, cancellationToken);
    }

    private async Task<SalesOrderIngestResult> CreateAsync(
        IntegrationConnection connection, MarketplaceOrderSnapshot snapshot, string source,
        CancellationToken cancellationToken)
    {
        var order = new SalesOrder
        {
            IntegrationConnectionId = connection.Id,
            TenantId = connection.TenantId,
            ExternalOrderId = snapshot.ExternalOrderId,
            FirstSeenAt = DateTime.UtcNow,

            // Every line arrives unmapped. Merchant360 is authoritative for product identity,
            // and inventing a mapping here would decrement the wrong stock and ship the wrong
            // item — worse than refusing to guess.
            MappingStatus = MappingStatus.UnmappedProduct
        };

        Apply(order, snapshot);

        order.StatusHistory.Add(new SalesOrderStatusHistory
        {
            FromStatus = SalesOrderStatus.Unknown,
            ToStatus = snapshot.Status,
            ExternalStatusRaw = snapshot.ExternalStatusRaw,
            SourceLastUpdatedAt = snapshot.SourceLastUpdatedAt,
            Source = source
        });

        foreach (var line in snapshot.Lines)
        {
            order.Lines.Add(NewLine(line));
        }

        StageEvent(order, connection, IntegrationEventType.SalesOrderCreated);

        // One SaveChanges commits the order, its lines, its opening history entry, the
        // integration event and the outbox message. An order that committed without its event
        // would never reach Merchant360, and nothing would record that it had not.
        if (await _orders.TryAddAsync(order, cancellationToken))
        {
            _logger.LogInformation(
                "Ingested new sales order {SalesOrderId} ({ExternalOrderId}) for tenant {TenantId} from {Source}",
                order.Id, order.ExternalOrderId, order.TenantId, source);

            return SalesOrderIngestResult.Created(order.Id);
        }

        // Lost the race to the unique index — polling and a notification reached the same order
        // at once, which is the design rather than an accident. Whoever arrived first created it;
        // this call continues as an update against what they wrote.
        var winner = await _orders.FindAsync(connection.Id, snapshot.ExternalOrderId, cancellationToken);
        if (winner is null)
        {
            return SalesOrderIngestResult.Failed(
                "The order could not be created and could not be found afterwards.");
        }

        _logger.LogDebug(
            "Concurrent ingest of {ExternalOrderId}; continuing as an update.", snapshot.ExternalOrderId);

        return await UpdateAsync(winner, connection, snapshot, source, cancellationToken);
    }

    private async Task<SalesOrderIngestResult> UpdateAsync(
        SalesOrder order, IntegrationConnection connection, MarketplaceOrderSnapshot snapshot,
        string source, CancellationToken cancellationToken)
    {
        // Monotonic on the provider's own timestamp. A slow poll response must not overwrite a
        // newer notification simply because it finished later.
        if (!order.IsNewerThanStored(snapshot.SourceLastUpdatedAt))
        {
            order.LastSyncedAt = DateTime.UtcNow;
            await _orders.SaveAsync(cancellationToken);
            return SalesOrderIngestResult.Stale(order.Id);
        }

        var previousStatus = order.Status;

        Apply(order, snapshot);
        MergeLines(order, snapshot);

        if (previousStatus != snapshot.Status)
        {
            order.StatusHistory.Add(new SalesOrderStatusHistory
            {
                FromStatus = previousStatus,
                ToStatus = snapshot.Status,
                ExternalStatusRaw = snapshot.ExternalStatusRaw,
                SourceLastUpdatedAt = snapshot.SourceLastUpdatedAt,
                Source = source
            });
        }

        StageEvent(
            order, connection,
            previousStatus != snapshot.Status
                ? IntegrationEventType.SalesOrderFulfillmentStatusChanged
                : IntegrationEventType.SalesOrderUpdated);

        await _orders.SaveAsync(cancellationToken);
        return SalesOrderIngestResult.Updated(order.Id);
    }

    /// <summary>
    /// Stages the event for a change, in the caller's transaction.
    /// </summary>
    /// <remarks>
    /// Nothing is sent here. The event row and its outbox message are staged beside the order so
    /// one <c>SaveChanges</c> commits all three; delivery happens later from the outbox. A stale
    /// snapshot never reaches this method, because an update that changed nothing is not a
    /// transition and republishing it would make the consumer dedupe work it should not have had.
    /// </remarks>
    private void StageEvent(
        SalesOrder order, IntegrationConnection connection, IntegrationEventType eventType)
    {
        if (_publisher is null)
        {
            _logger.LogWarning(
                "No event publisher is registered; sales order {SalesOrderId} was ingested but "
                + "nothing will be published to Merchant360.",
                order.Id);
            return;
        }

        var providerCode = connection.Provider?.Code ?? "AMAZON_SP";

        _publisher.StageForOrder(
            order, connection, providerCode, eventType,
            EventPublicationReason.ProviderChange, correlationId: null);
    }

    /// <summary>Copies the snapshot onto the order. Does not touch mapping or event state.</summary>
    private static void Apply(SalesOrder order, MarketplaceOrderSnapshot snapshot)
    {
        order.MarketplaceId = snapshot.MarketplaceId ?? order.MarketplaceId;
        order.Status = snapshot.Status;
        order.ExternalStatusRaw = snapshot.ExternalStatusRaw;
        order.FulfillmentChannel = snapshot.FulfillmentChannel;
        order.ProgramsRawJson = snapshot.Programs.Count > 0
            ? JsonSerializer.Serialize(snapshot.Programs)
            : order.ProgramsRawJson;

        order.PurchaseDate = snapshot.PurchaseDate == default ? order.PurchaseDate : snapshot.PurchaseDate;
        order.SourceLastUpdatedAt = snapshot.SourceLastUpdatedAt;

        order.OrderTotalAmount = snapshot.OrderTotalAmount ?? order.OrderTotalAmount;
        order.CurrencyCode = snapshot.CurrencyCode ?? order.CurrencyCode;

        order.LastSyncedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        // MappingStatus is deliberately untouched. It is set by the Merchant360 exchange, and a
        // re-poll overwriting it would undo a mapping an operator had just resolved.

        ApplyBuyerData(order, snapshot);
    }

    /// <summary>
    /// Writes buyer and address data, except where it must not be written.
    /// </summary>
    /// <remarks>
    /// Two cases are deliberately refused. A snapshot without buyer data — a PENDING order, or
    /// one fetched before the BUYER role was granted — must not blank out details already held,
    /// because absence there means "not supplied", not "deleted". And an order already redacted
    /// under the retention policy must not have its personal data restored by the next poll,
    /// which would make retention something the provider could silently undo.
    /// </remarks>
    private static void ApplyBuyerData(SalesOrder order, MarketplaceOrderSnapshot snapshot)
    {
        if (order.PiiRedactedAt.HasValue)
        {
            order.BuyerDataAvailable = false;
            return;
        }

        if (!snapshot.BuyerDataAvailable)
        {
            // Record only that this snapshot had none; keep whatever an earlier one supplied.
            order.BuyerDataAvailable = order.BuyerDataAvailable || false;
            return;
        }

        order.BuyerDataAvailable = true;
        order.BuyerEmailRef = snapshot.BuyerEmailRef ?? order.BuyerEmailRef;
        order.BuyerName = snapshot.BuyerName ?? order.BuyerName;

        if (snapshot.ShipTo is { } address)
        {
            order.ShipToName = address.Name ?? order.ShipToName;
            order.ShipToLine1 = address.Line1 ?? order.ShipToLine1;
            order.ShipToLine2 = address.Line2 ?? order.ShipToLine2;
            order.ShipToCity = address.City ?? order.ShipToCity;
            order.ShipToStateOrRegion = address.StateOrRegion ?? order.ShipToStateOrRegion;
            order.ShipToPostalCode = address.PostalCode ?? order.ShipToPostalCode;
            order.ShipToCountryCode = address.CountryCode ?? order.ShipToCountryCode;
        }
    }

    /// <summary>
    /// Updates lines in place by provider line id, and adds ones not seen before.
    /// </summary>
    /// <remarks>
    /// Lines are never deleted here. A snapshot fetched without the FULFILLMENT or PROCEEDS role
    /// can legitimately arrive thinner than the one before it, and a delete-and-recreate would
    /// discard the Merchant360 mapping attached to each line along with it.
    /// </remarks>
    private static void MergeLines(SalesOrder order, MarketplaceOrderSnapshot snapshot)
    {
        foreach (var incoming in snapshot.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.ExternalLineId == incoming.ExternalLineId);

            if (line is null)
            {
                order.Lines.Add(NewLine(incoming));
                continue;
            }

            line.SellerSku = incoming.SellerSku ?? line.SellerSku;
            line.Asin = incoming.Asin ?? line.Asin;
            line.Title = incoming.Title ?? line.Title;
            line.QuantityOrdered = incoming.QuantityOrdered > 0 ? incoming.QuantityOrdered : line.QuantityOrdered;
            line.QuantityShipped = Math.Max(line.QuantityShipped, incoming.QuantityShipped);

            line.ItemPrice = incoming.ItemPrice ?? line.ItemPrice;
            line.ItemTax = incoming.ItemTax ?? line.ItemTax;
            line.ShippingPrice = incoming.ShippingPrice ?? line.ShippingPrice;
            line.PromotionDiscount = incoming.PromotionDiscount ?? line.PromotionDiscount;
            line.CurrencyCode = incoming.CurrencyCode ?? line.CurrencyCode;
            line.ProceedsRawJson = incoming.ProceedsRawJson ?? line.ProceedsRawJson;

            line.UpdatedAt = DateTime.UtcNow;

            // M360CatalogItemId and MappingStatus are left alone: they belong to the mapping
            // exchange, not to the provider.
        }
    }

    private static SalesOrderLine NewLine(MarketplaceOrderLineSnapshot snapshot) => new()
    {
        ExternalLineId = snapshot.ExternalLineId,
        SellerSku = snapshot.SellerSku,
        Asin = snapshot.Asin,
        Title = snapshot.Title,
        QuantityOrdered = snapshot.QuantityOrdered,
        QuantityShipped = snapshot.QuantityShipped,
        MappingStatus = MappingStatus.UnmappedProduct,
        ItemPrice = snapshot.ItemPrice,
        ItemTax = snapshot.ItemTax,
        ShippingPrice = snapshot.ShippingPrice,
        PromotionDiscount = snapshot.PromotionDiscount,
        CurrencyCode = snapshot.CurrencyCode,
        ProceedsRawJson = snapshot.ProceedsRawJson
    };
}
