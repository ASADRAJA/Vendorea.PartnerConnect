using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// The sales-order schema constraints, proven against real PostgreSQL — test matrix 3.1.
/// </summary>
/// <remarks>
/// The unique index on (connection, external order id) is the mechanism behind the central
/// promise of the whole integration: one Amazon order becomes exactly one Merchant360 sales
/// order. The InMemory provider does not enforce unique indexes, so asserting it there would
/// pass without proving anything.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class SalesOrderSchemaTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private int _tenantId;
    private int _otherTenantId;
    private Guid _connectionId;
    private Guid _otherConnectionId;

    public SalesOrderSchemaTests(PostgresFixture pg) => _pg = pg;

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

        var t1 = new Tenant { OrganizationId = org.Id, Code = "D1", Name = "Dealer1" };
        var t2 = new Tenant { OrganizationId = org.Id, Code = "D2", Name = "Dealer2" };
        db.Tenants.AddRange(t1, t2);

        var provider = new IntegrationProvider
        {
            Code = "AMAZON_SP",
            Name = "Amazon Selling Partner",
            Kind = IntegrationProviderKind.Marketplace,
            AuthKind = IntegrationAuthKind.OAuth2,
            Capabilities = IntegrationCapabilities.SalesOrderIngest
        };
        db.IntegrationProviders.Add(provider);
        await db.SaveChangesAsync();

        _tenantId = t1.Id;
        _otherTenantId = t2.Id;

        var c1 = new IntegrationConnection
        {
            OrganizationId = org.Id, TenantId = t1.Id, IntegrationProviderId = provider.Id,
            ExternalAccountId = "SELLER-A", Status = IntegrationConnectionStatus.Connected
        };
        var c2 = new IntegrationConnection
        {
            OrganizationId = org.Id, TenantId = t2.Id, IntegrationProviderId = provider.Id,
            ExternalAccountId = "SELLER-B", Status = IntegrationConnectionStatus.Connected
        };
        db.IntegrationConnections.AddRange(c1, c2);
        await db.SaveChangesAsync();

        _connectionId = c1.Id;
        _otherConnectionId = c2.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private SalesOrder NewOrder(Guid connectionId, int tenantId, string externalOrderId) => new()
    {
        IntegrationConnectionId = connectionId,
        TenantId = tenantId,
        ExternalOrderId = externalOrderId,
        MarketplaceId = "ATVPDKIKX0DER",
        Status = SalesOrderStatus.ReadyToFulfill,
        ExternalStatusRaw = "UNSHIPPED",
        FulfillmentChannel = FulfillmentChannel.MerchantFulfilled,
        MappingStatus = MappingStatus.Mapped,
        ProgramsRawJson = """["PRIME"]""",
        PurchaseDate = DateTime.UtcNow.AddHours(-2),
        SourceLastUpdatedAt = DateTime.UtcNow.AddHours(-1),
        OrderTotalAmount = 48.97m,
        CurrencyCode = "USD",
        BuyerDataAvailable = true
    };

    [Fact]
    public async Task The_same_provider_order_cannot_be_stored_twice_for_one_connection()
    {
        await using (var db = _pg.CreateContext())
        {
            db.SalesOrders.Add(NewOrder(_connectionId, _tenantId, "111-2223334-5556667"));
            await db.SaveChangesAsync();
        }

        await using var second = _pg.CreateContext();
        second.SalesOrders.Add(NewOrder(_connectionId, _tenantId, "111-2223334-5556667"));

        // Not a race the application has to win: the database refuses it. Polling and
        // notifications deliver the same order concurrently by design.
        var act = async () => await second.SaveChangesAsync();
        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Two_connections_may_each_hold_an_order_with_the_same_provider_id()
    {
        await using var db = _pg.CreateContext();

        // Amazon order ids are not globally unique across sellers, and two tenants connecting
        // different seller accounts must not collide.
        db.SalesOrders.Add(NewOrder(_connectionId, _tenantId, "111-2223334-5556667"));
        db.SalesOrders.Add(NewOrder(_otherConnectionId, _otherTenantId, "111-2223334-5556667"));

        await db.SaveChangesAsync();

        (await db.SalesOrders.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_line_cannot_be_duplicated_within_its_order()
    {
        Guid orderId;
        await using (var db = _pg.CreateContext())
        {
            var order = NewOrder(_connectionId, _tenantId, "111-0000001-0000001");
            order.Lines.Add(new SalesOrderLine
            {
                ExternalLineId = "58392014771234", SellerSku = "VND-COPY-8511",
                QuantityOrdered = 2, MappingStatus = MappingStatus.Mapped,
                ItemPrice = 39.98m, CurrencyCode = "USD"
            });
            db.SalesOrders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var second = _pg.CreateContext();
        second.SalesOrderLines.Add(new SalesOrderLine
        {
            SalesOrderId = orderId, ExternalLineId = "58392014771234",
            QuantityOrdered = 2, MappingStatus = MappingStatus.Mapped
        });

        var act = async () => await second.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Money_keeps_its_scale_through_a_round_trip()
    {
        await using (var db = _pg.CreateContext())
        {
            var order = NewOrder(_connectionId, _tenantId, "111-0000002-0000002");
            order.OrderTotalAmount = 1234.5678m;
            order.Lines.Add(new SalesOrderLine
            {
                ExternalLineId = "L1", QuantityOrdered = 1, MappingStatus = MappingStatus.Mapped,
                ItemPrice = 0.0001m, PromotionDiscount = -2.5000m, CurrencyCode = "USD"
            });
            db.SalesOrders.Add(order);
            await db.SaveChangesAsync();
        }

        await using var read = _pg.CreateContext();
        var stored = await read.SalesOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.ExternalOrderId == "111-0000002-0000002");

        // decimal(18,4) end to end. A float column would already have lost this.
        stored.OrderTotalAmount.Should().Be(1234.5678m);
        stored.Lines.Single().ItemPrice.Should().Be(0.0001m);
        stored.Lines.Single().PromotionDiscount.Should().Be(-2.5000m);
    }

    [Fact]
    public async Task The_provider_payload_fields_survive_as_json()
    {
        await using (var db = _pg.CreateContext())
        {
            var order = NewOrder(_connectionId, _tenantId, "111-0000003-0000003");
            order.ProgramsRawJson = """["PRIME","BUSINESS"]""";
            order.Lines.Add(new SalesOrderLine
            {
                ExternalLineId = "L1", QuantityOrdered = 1, MappingStatus = MappingStatus.Mapped,
                ProceedsRawJson = """{"type":"Principal","charges":[{"type":"Principal","amount":"39.98"}]}"""
            });
            db.SalesOrders.Add(order);
            await db.SaveChangesAsync();
        }

        await using var read = _pg.CreateContext();
        var stored = await read.SalesOrders
            .Include(o => o.Lines)
            .SingleAsync(o => o.ExternalOrderId == "111-0000003-0000003");

        stored.ProgramsRawJson.Should().Contain("BUSINESS");
        stored.Lines.Single().ProceedsRawJson.Should().Contain("Principal");
    }

    [Fact]
    public async Task Statuses_are_stored_as_text_not_ordinals()
    {
        await using (var db = _pg.CreateContext())
        {
            db.SalesOrders.Add(NewOrder(_connectionId, _tenantId, "111-0000004-0000004"));
            await db.SaveChangesAsync();
        }

        await using var db2 = _pg.CreateContext();
        var connection = db2.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """SELECT "Status" FROM "SalesOrders" WHERE "ExternalOrderId" = '111-0000004-0000004'""";

        // An integer column would turn a reordered enum into silent corruption of every
        // historical row, and these values are read by operators.
        (await command.ExecuteScalarAsync()).Should().Be("ReadyToFulfill");
    }

    [Fact]
    public async Task An_unrecognised_provider_status_is_stored_as_Unknown_with_its_raw_text()
    {
        await using (var db = _pg.CreateContext())
        {
            var order = NewOrder(_connectionId, _tenantId, "111-0000005-0000005");
            order.Status = SalesOrderStatus.Unknown;
            order.ExternalStatusRaw = "SOME_NEW_AMAZON_STATUS";
            db.SalesOrders.Add(order);
            await db.SaveChangesAsync();
        }

        await using var read = _pg.CreateContext();
        var stored = await read.SalesOrders.SingleAsync(o => o.ExternalOrderId == "111-0000005-0000005");

        // Discarding what the provider actually said leaves nobody able to work out what changed.
        stored.Status.Should().Be(SalesOrderStatus.Unknown);
        stored.ExternalStatusRaw.Should().Be("SOME_NEW_AMAZON_STATUS");
    }

    [Fact]
    public async Task Redacting_PII_clears_the_personal_fields_and_records_that_it_happened()
    {
        Guid orderId;
        await using (var db = _pg.CreateContext())
        {
            var order = NewOrder(_connectionId, _tenantId, "111-0000006-0000006");
            order.BuyerEmailRef = "x1y2z3@marketplace.amazon.com";
            order.BuyerName = "J. Carter";
            order.ShipToName = "J. Carter";
            order.ShipToLine1 = "414 W Main St";
            order.ShipToCity = "Richmond";
            order.ShipToPostalCode = "23220";
            order.ShipToCountryCode = "US";
            db.SalesOrders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        await using (var db = _pg.CreateContext())
        {
            var order = await db.SalesOrders.SingleAsync(o => o.Id == orderId);
            order.RedactPii(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        await using var read = _pg.CreateContext();
        var redacted = await read.SalesOrders.SingleAsync(o => o.Id == orderId);

        redacted.BuyerEmailRef.Should().BeNull();
        redacted.BuyerName.Should().BeNull();
        redacted.ShipToLine1.Should().BeNull();
        redacted.ShipToPostalCode.Should().BeNull();

        // Country survives: not identifying alone, and tax and reporting need it.
        redacted.ShipToCountryCode.Should().Be("US");

        // "Never supplied" and "supplied and since erased" are different facts, and only one of
        // them answers a data-subject request.
        redacted.PiiRedactedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_pending_order_without_buyer_data_is_a_normal_row()
    {
        await using var db = _pg.CreateContext();

        var order = NewOrder(_connectionId, _tenantId, "111-0000007-0000007");
        order.Status = SalesOrderStatus.Pending;
        order.ExternalStatusRaw = "PENDING";
        order.BuyerDataAvailable = false;
        order.BuyerEmailRef = null;
        db.SalesOrders.Add(order);

        // Amazon withholds buyer data on PENDING orders by design. Nothing here is nullable
        // "just in case" — it is nullable because the provider documents that it will be absent.
        await db.SaveChangesAsync();

        (await db.SalesOrders.CountAsync(o => !o.BuyerDataAvailable)).Should().Be(1);
    }

    [Fact]
    public async Task Status_history_is_kept_per_order_in_recorded_order()
    {
        await using var db = _pg.CreateContext();

        var order = NewOrder(_connectionId, _tenantId, "111-0000008-0000008");
        order.StatusHistory.Add(new SalesOrderStatusHistory
        {
            FromStatus = SalesOrderStatus.Pending, ToStatus = SalesOrderStatus.ReadyToFulfill,
            ExternalStatusRaw = "UNSHIPPED", SourceLastUpdatedAt = DateTime.UtcNow.AddMinutes(-10),
            RecordedAt = DateTime.UtcNow.AddMinutes(-10), Source = "poll"
        });
        order.StatusHistory.Add(new SalesOrderStatusHistory
        {
            FromStatus = SalesOrderStatus.ReadyToFulfill, ToStatus = SalesOrderStatus.Fulfilled,
            ExternalStatusRaw = "SHIPPED", SourceLastUpdatedAt = DateTime.UtcNow,
            RecordedAt = DateTime.UtcNow, Source = "notification"
        });
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync();

        var history = await db.SalesOrderStatusHistory
            .Where(h => h.SalesOrderId == order.Id)
            .OrderBy(h => h.RecordedAt)
            .ToListAsync();

        history.Should().HaveCount(2);
        history[0].Source.Should().Be("poll");
        history[1].ToStatus.Should().Be(SalesOrderStatus.Fulfilled);
    }

    [Fact]
    public async Task Deleting_an_order_takes_its_lines_and_history_with_it()
    {
        Guid orderId;
        await using (var db = _pg.CreateContext())
        {
            var order = NewOrder(_connectionId, _tenantId, "111-0000009-0000009");
            order.Lines.Add(new SalesOrderLine
            {
                ExternalLineId = "L1", QuantityOrdered = 1, MappingStatus = MappingStatus.Mapped
            });
            order.StatusHistory.Add(new SalesOrderStatusHistory
            {
                FromStatus = SalesOrderStatus.Pending, ToStatus = SalesOrderStatus.ReadyToFulfill,
                SourceLastUpdatedAt = DateTime.UtcNow
            });
            db.SalesOrders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        await using (var db = _pg.CreateContext())
        {
            db.SalesOrders.Remove(await db.SalesOrders.SingleAsync(o => o.Id == orderId));
            await db.SaveChangesAsync();
        }

        await using var read = _pg.CreateContext();
        (await read.SalesOrderLines.CountAsync(l => l.SalesOrderId == orderId)).Should().Be(0);
        (await read.SalesOrderStatusHistory.CountAsync(h => h.SalesOrderId == orderId)).Should().Be(0);
    }

    [Fact]
    public async Task A_connection_holding_orders_cannot_be_deleted_out_from_under_them()
    {
        await using (var db = _pg.CreateContext())
        {
            db.SalesOrders.Add(NewOrder(_connectionId, _tenantId, "111-0000010-0000010"));
            await db.SaveChangesAsync();
        }

        await using var db2 = _pg.CreateContext();
        db2.IntegrationConnections.Remove(
            await db2.IntegrationConnections.SingleAsync(c => c.Id == _connectionId));

        // Disconnecting revokes a connection; it never deletes the row, precisely so the orders
        // it ingested stay attributable. The FK makes that an invariant rather than a habit.
        var act = async () => await db2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
