using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// Ingestion upsert semantics against real PostgreSQL — test matrix 3.2, 3.4 and 3.5.
/// </summary>
/// <remarks>
/// Polling and notifications deliver the same order concurrently and out of order by design, so
/// "the same order arrives twice" and "an older copy arrives late" are the normal cases rather
/// than the edge cases. Both are proven here against the real unique index.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class SalesOrderIngestionTests : IAsyncLifetime
{
    private const string OrderId = "111-2223334-5556667";

    private readonly PostgresFixture _pg;
    private IntegrationConnection _connection = null!;

    public SalesOrderIngestionTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync(
            "SalesOrderStatusHistory", "SalesOrderLines", "SalesOrders",
            "IntegrationOAuthStates", "IntegrationCredentials", "IntegrationSyncStates",
            "IntegrationConnections", "IntegrationProviders",
            "AuditLogs", "Tenants", "Organizations");

        await using var db = _pg.CreateContext();

        var org = new Organization { Code = "ACME", Name = "Acme", Status = OrganizationStatus.Active };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var tenant = new Tenant { OrganizationId = org.Id, Code = "D1", Name = "Dealer1" };
        db.Tenants.Add(tenant);
        var provider = new IntegrationProvider
        {
            Code = "AMAZON_SP", Name = "Amazon", Kind = IntegrationProviderKind.Marketplace,
            AuthKind = IntegrationAuthKind.OAuth2,
            Capabilities = IntegrationCapabilities.SalesOrderIngest
        };
        db.IntegrationProviders.Add(provider);
        await db.SaveChangesAsync();

        var connection = new IntegrationConnection
        {
            OrganizationId = org.Id, TenantId = tenant.Id, IntegrationProviderId = provider.Id,
            ExternalAccountId = "SELLER-A", Status = IntegrationConnectionStatus.Connected,
            GrantedCapabilities = IntegrationCapabilities.SalesOrderIngest
        };
        db.IntegrationConnections.Add(connection);
        await db.SaveChangesAsync();

        _connection = connection;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private SalesOrderIngestionService Build()
        => new(new SalesOrderRepository(_pg.CreateContext()),
            NullLogger<SalesOrderIngestionService>.Instance);

    private static MarketplaceOrderSnapshot Snapshot(
        DateTime lastUpdated,
        SalesOrderStatus status = SalesOrderStatus.ReadyToFulfill,
        string statusRaw = "UNSHIPPED",
        bool buyerData = true,
        string orderId = OrderId)
        => new()
        {
            ExternalOrderId = orderId,
            MarketplaceId = "ATVPDKIKX0DER",
            Status = status,
            ExternalStatusRaw = statusRaw,
            FulfillmentChannel = FulfillmentChannel.MerchantFulfilled,
            Programs = new[] { "PRIME" },
            PurchaseDate = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc),
            SourceLastUpdatedAt = lastUpdated,
            OrderTotalAmount = 48.97m,
            CurrencyCode = "USD",
            BuyerDataAvailable = buyerData,
            BuyerEmailRef = buyerData ? "x1y2z3@marketplace.amazon.com" : null,
            BuyerName = buyerData ? "J. Carter" : null,
            ShipTo = buyerData
                ? new MarketplaceAddress("J. Carter", "414 W Main St", null, "Richmond", "VA", "23220", "US")
                : null,
            Lines = new[]
            {
                new MarketplaceOrderLineSnapshot
                {
                    ExternalLineId = "L1", SellerSku = "VND-COPY-8511", Asin = "B07XYZ1234",
                    QuantityOrdered = 2, QuantityShipped = 0,
                    ItemPrice = 39.98m, CurrencyCode = "USD"
                }
            }
        };

    [Fact]
    public async Task A_new_order_is_created_with_its_lines_and_an_opening_history_entry()
    {
        var result = await Build().IngestAsync(
            _connection, Snapshot(DateTime.UtcNow.AddMinutes(-10)), "poll");

        result.Outcome.Should().Be(SalesOrderIngestOutcome.Created);

        await using var db = _pg.CreateContext();
        var order = await db.SalesOrders
            .Include(o => o.Lines).Include(o => o.StatusHistory)
            .SingleAsync();

        order.TenantId.Should().Be(_connection.TenantId);
        order.Lines.Should().ContainSingle();
        order.StatusHistory.Should().ContainSingle();
        order.StatusHistory.Single().FromStatus.Should().Be(SalesOrderStatus.Unknown);
        order.StatusHistory.Single().ToStatus.Should().Be(SalesOrderStatus.ReadyToFulfill);

        // Merchant360 is authoritative for product identity; nothing is mapped on arrival.
        order.MappingStatus.Should().Be(MappingStatus.UnmappedProduct);
    }

    [Fact]
    public async Task The_same_order_arriving_twice_updates_rather_than_duplicates()
    {
        var first = DateTime.UtcNow.AddMinutes(-10);

        (await Build().IngestAsync(_connection, Snapshot(first), "poll"))
            .Outcome.Should().Be(SalesOrderIngestOutcome.Created);

        var second = await Build().IngestAsync(
            _connection, Snapshot(first.AddMinutes(5), SalesOrderStatus.Fulfilled, "SHIPPED"), "notification");

        second.Outcome.Should().Be(SalesOrderIngestOutcome.Updated);

        await using var db = _pg.CreateContext();
        (await db.SalesOrders.CountAsync()).Should().Be(1);
        (await db.SalesOrders.SingleAsync()).Status.Should().Be(SalesOrderStatus.Fulfilled);
    }

    [Fact]
    public async Task An_older_snapshot_arriving_late_does_not_overwrite_a_newer_one()
    {
        var newer = DateTime.UtcNow.AddMinutes(-5);
        var older = DateTime.UtcNow.AddMinutes(-30);

        await Build().IngestAsync(
            _connection, Snapshot(newer, SalesOrderStatus.Fulfilled, "SHIPPED"), "notification");

        var stale = await Build().IngestAsync(
            _connection, Snapshot(older, SalesOrderStatus.ReadyToFulfill, "UNSHIPPED"), "poll");

        stale.Outcome.Should().Be(SalesOrderIngestOutcome.SkippedStale);

        await using var db = _pg.CreateContext();
        var order = await db.SalesOrders.Include(o => o.StatusHistory).SingleAsync();

        // A slow poll response must not undo a newer notification simply because it finished
        // later — and it must not write a spurious transition back to unshipped either.
        order.Status.Should().Be(SalesOrderStatus.Fulfilled);
        order.StatusHistory.Should().ContainSingle();
    }

    [Fact]
    public async Task A_status_change_appends_history_and_an_unchanged_status_does_not()
    {
        var t0 = DateTime.UtcNow.AddMinutes(-30);

        await Build().IngestAsync(_connection, Snapshot(t0), "poll");
        await Build().IngestAsync(_connection, Snapshot(t0.AddMinutes(5)), "poll");           // same status
        await Build().IngestAsync(
            _connection, Snapshot(t0.AddMinutes(10), SalesOrderStatus.Fulfilled, "SHIPPED"), "poll");

        await using var db = _pg.CreateContext();
        var history = await db.SalesOrderStatusHistory.OrderBy(h => h.Id).ToListAsync();

        // Two entries, not three: a re-poll that changes nothing is not an event.
        history.Should().HaveCount(2);
        history[1].FromStatus.Should().Be(SalesOrderStatus.ReadyToFulfill);
        history[1].ToStatus.Should().Be(SalesOrderStatus.Fulfilled);
    }

    [Fact]
    public async Task Concurrent_ingests_of_a_new_order_produce_exactly_one_row()
    {
        var snapshot = Snapshot(DateTime.UtcNow.AddMinutes(-10));

        // Eight services, eight DbContexts — the poll-and-notification collision, amplified.
        var services = Enumerable.Range(0, 8).Select(_ => Build()).ToList();
        var results = await Task.WhenAll(
            services.Select(s => s.IngestAsync(_connection, snapshot, "poll")));

        results.Should().OnlyContain(r => r.Success);
        results.Count(r => r.Outcome == SalesOrderIngestOutcome.Created).Should().Be(1);

        await using var db = _pg.CreateContext();
        (await db.SalesOrders.CountAsync()).Should().Be(1);

        // Losing the race is the design, not a fault: the loser continues as an update.
        (await db.SalesOrderLines.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_snapshot_without_buyer_data_does_not_blank_out_what_is_held()
    {
        var t0 = DateTime.UtcNow.AddMinutes(-20);

        await Build().IngestAsync(_connection, Snapshot(t0, buyerData: true), "poll");
        await Build().IngestAsync(_connection, Snapshot(t0.AddMinutes(5), buyerData: false), "poll");

        await using var db = _pg.CreateContext();
        var order = await db.SalesOrders.SingleAsync();

        // Absence means "not supplied" — a PENDING order, or one fetched before the BUYER role
        // was granted — never "deleted".
        order.BuyerName.Should().Be("J. Carter");
        order.ShipToPostalCode.Should().Be("23220");
    }

    [Fact]
    public async Task A_redacted_order_is_not_repopulated_by_the_next_poll()
    {
        var t0 = DateTime.UtcNow.AddMinutes(-20);
        await Build().IngestAsync(_connection, Snapshot(t0, buyerData: true), "poll");

        await using (var db = _pg.CreateContext())
        {
            var order = await db.SalesOrders.SingleAsync();
            order.RedactPii(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        await Build().IngestAsync(_connection, Snapshot(t0.AddMinutes(5), buyerData: true), "poll");

        await using var verify = _pg.CreateContext();
        var redacted = await verify.SalesOrders.SingleAsync();

        // Otherwise retention is something the provider can silently undo.
        redacted.BuyerName.Should().BeNull();
        redacted.ShipToLine1.Should().BeNull();
        redacted.BuyerDataAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task A_new_line_on_a_later_snapshot_is_added_and_existing_lines_survive()
    {
        var t0 = DateTime.UtcNow.AddMinutes(-20);
        await Build().IngestAsync(_connection, Snapshot(t0), "poll");

        var withTwoLines = Snapshot(t0.AddMinutes(5)) with
        {
            Lines = new[]
            {
                new MarketplaceOrderLineSnapshot
                {
                    ExternalLineId = "L1", QuantityOrdered = 2, QuantityShipped = 2
                },
                new MarketplaceOrderLineSnapshot
                {
                    ExternalLineId = "L2", SellerSku = "VND-PEN-01", QuantityOrdered = 1
                }
            }
        };

        await Build().IngestAsync(_connection, withTwoLines, "poll");

        await using var db = _pg.CreateContext();
        var lines = await db.SalesOrderLines.OrderBy(l => l.ExternalLineId).ToListAsync();

        lines.Should().HaveCount(2);
        lines[0].QuantityShipped.Should().Be(2);

        // The first line keeps what the thinner snapshot did not carry.
        lines[0].SellerSku.Should().Be("VND-COPY-8511");
        lines[0].Asin.Should().Be("B07XYZ1234");
    }

    [Fact]
    public async Task A_thinner_snapshot_never_deletes_lines()
    {
        var t0 = DateTime.UtcNow.AddMinutes(-20);
        await Build().IngestAsync(_connection, Snapshot(t0), "poll");

        // What arrives when a role grant is missing, or from a lighter notification payload.
        await Build().IngestAsync(
            _connection, Snapshot(t0.AddMinutes(5)) with { Lines = Array.Empty<MarketplaceOrderLineSnapshot>() },
            "notification");

        await using var db = _pg.CreateContext();

        // A delete-and-recreate would discard the Merchant360 mapping attached to each line.
        (await db.SalesOrderLines.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_mapping_resolved_elsewhere_survives_a_re_poll()
    {
        var t0 = DateTime.UtcNow.AddMinutes(-20);
        await Build().IngestAsync(_connection, Snapshot(t0), "poll");

        await using (var db = _pg.CreateContext())
        {
            var order = await db.SalesOrders.Include(o => o.Lines).SingleAsync();
            order.MappingStatus = MappingStatus.Mapped;
            order.Lines.Single().MappingStatus = MappingStatus.Mapped;
            order.Lines.Single().M360CatalogItemId = "ITEM-44821";
            await db.SaveChangesAsync();
        }

        await Build().IngestAsync(_connection, Snapshot(t0.AddMinutes(5)), "poll");

        await using var verify = _pg.CreateContext();
        var reread = await verify.SalesOrders.Include(o => o.Lines).SingleAsync();

        // Mapping belongs to the Merchant360 exchange, not to the provider. A re-poll
        // overwriting it would undo a mapping an operator had just resolved.
        reread.MappingStatus.Should().Be(MappingStatus.Mapped);
        reread.Lines.Single().M360CatalogItemId.Should().Be("ITEM-44821");
    }

    [Fact]
    public async Task A_snapshot_with_no_order_id_is_refused()
    {
        var result = await Build().IngestAsync(
            _connection, Snapshot(DateTime.UtcNow, orderId: "") , "poll");

        result.Outcome.Should().Be(SalesOrderIngestOutcome.Failed);

        await using var db = _pg.CreateContext();
        (await db.SalesOrders.CountAsync()).Should().Be(0);
    }
}
