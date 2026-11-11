using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// The sales-order listing query — test matrix 3.11.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SalesOrderSearchTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private int _tenantId;
    private int _otherTenantId;
    private IntegrationConnection _connectionA = null!;
    private IntegrationConnection _connectionB = null!;

    public SalesOrderSearchTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync(
            "IntegrationPayloadArchives",
            "SalesOrderStatusHistory", "SalesOrderLines", "SalesOrders",
            "ProviderRateLimitBuckets", "IntegrationAccountDelegations",
            "IntegrationOAuthStates", "IntegrationCredentials", "IntegrationSyncStates",
            "IntegrationConnections", "IntegrationProviders",
            "AuditLogs", "Tenants", "Organizations");

        await using var db = _pg.CreateContext();

        var org = new Organization { Code = "ACME", Name = "Acme", Status = OrganizationStatus.Active };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var t1 = new Tenant { OrganizationId = org.Id, Code = "D1", Name = "Dealer One" };
        var t2 = new Tenant { OrganizationId = org.Id, Code = "D2", Name = "Dealer Two" };
        db.Tenants.AddRange(t1, t2);

        var provider = new IntegrationProvider
        {
            Code = "AMAZON_SP", Name = "Amazon", Kind = IntegrationProviderKind.Marketplace,
            AuthKind = IntegrationAuthKind.OAuth2
        };
        db.IntegrationProviders.Add(provider);
        await db.SaveChangesAsync();

        _tenantId = t1.Id;
        _otherTenantId = t2.Id;

        var a = new IntegrationConnection
        {
            OrganizationId = org.Id, TenantId = t1.Id, IntegrationProviderId = provider.Id,
            ExternalAccountId = "SELLER-A", Status = IntegrationConnectionStatus.Connected
        };
        var b = new IntegrationConnection
        {
            OrganizationId = org.Id, TenantId = t1.Id, IntegrationProviderId = provider.Id,
            ExternalAccountId = "SELLER-B", Status = IntegrationConnectionStatus.Connected
        };
        db.IntegrationConnections.AddRange(a, b);
        await db.SaveChangesAsync();

        _connectionA = a;
        _connectionB = b;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private SalesOrderRepository Repository() => new(_pg.CreateContext());

    [Fact]
    public async Task A_listing_is_scoped_to_one_tenant()
    {
        await SeedAsync("mine", tenantId: _tenantId);
        await SeedAsync("theirs", tenantId: _otherTenantId, connection: _connectionA);

        var page = await Repository().SearchAsync(_tenantId, new SalesOrderQuery());

        page.Items.Should().ContainSingle();
        page.Items[0].ExternalOrderId.Should().Be("mine");
        page.Total.Should().Be(1);
    }

    [Fact]
    public async Task Orders_come_back_newest_purchase_first()
    {
        await SeedAsync("oldest", purchaseDate: DateTime.UtcNow.AddDays(-10));
        await SeedAsync("newest", purchaseDate: DateTime.UtcNow.AddDays(-1));
        await SeedAsync("middle", purchaseDate: DateTime.UtcNow.AddDays(-5));

        var page = await Repository().SearchAsync(_tenantId, new SalesOrderQuery());

        page.Items.Select(o => o.ExternalOrderId)
            .Should().BeEquivalentTo(
                new[] { "newest", "middle", "oldest" },
                options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Paging_reports_the_total_alongside_the_page()
    {
        for (var i = 0; i < 12; i++)
        {
            await SeedAsync($"order-{i:00}", purchaseDate: DateTime.UtcNow.AddHours(-i));
        }

        var page = await Repository().SearchAsync(
            _tenantId, new SalesOrderQuery { Skip = 5, Take = 4 });

        // A caller showing "6-9 of 12" otherwise has to ask twice for one answer.
        page.Items.Should().HaveCount(4);
        page.Total.Should().Be(12);
        page.Skip.Should().Be(5);
        page.Take.Should().Be(4);
    }

    [Fact]
    public async Task Paging_with_identical_purchase_dates_neither_repeats_nor_skips_a_row()
    {
        var sameInstant = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 10; i++)
        {
            await SeedAsync($"order-{i:00}", purchaseDate: sameInstant);
        }

        var first = await Repository().SearchAsync(_tenantId, new SalesOrderQuery { Skip = 0, Take = 5 });
        var second = await Repository().SearchAsync(_tenantId, new SalesOrderQuery { Skip = 5, Take = 5 });

        // Ordering by purchase date alone is non-deterministic when dates tie, and paging over a
        // non-deterministic order silently shows some rows twice and others never. The id is the
        // tiebreak that makes the sequence stable.
        var all = first.Items.Concat(second.Items).Select(o => o.ExternalOrderId).ToList();
        all.Should().HaveCount(10);
        all.Distinct().Should().HaveCount(10);
    }

    [Fact]
    public async Task Take_is_clamped_to_the_documented_maximum()
    {
        await SeedAsync("one");

        var page = await Repository().SearchAsync(
            _tenantId, new SalesOrderQuery { Take = 100_000 });

        // The contract caps take at 200. A caller asking for everything gets a page, not a
        // query that reads the whole table.
        page.Take.Should().Be(200);
    }

    [Fact]
    public async Task A_negative_skip_is_treated_as_zero()
    {
        await SeedAsync("one");

        var page = await Repository().SearchAsync(_tenantId, new SalesOrderQuery { Skip = -5 });

        page.Skip.Should().Be(0);
        page.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Filtering_by_connection_narrows_within_the_tenant()
    {
        await SeedAsync("from-a", connection: _connectionA);
        await SeedAsync("from-b", connection: _connectionB);

        var page = await Repository().SearchAsync(
            _tenantId, new SalesOrderQuery { ConnectionId = _connectionB.Id });

        page.Items.Should().ContainSingle();
        page.Items[0].ExternalOrderId.Should().Be("from-b");
        page.Total.Should().Be(1);
    }

    [Fact]
    public async Task Filtering_by_status_and_mapping_status_works_together()
    {
        await SeedAsync("a", status: SalesOrderStatus.ReadyToFulfill, mapping: MappingStatus.Mapped);
        await SeedAsync("b", status: SalesOrderStatus.ReadyToFulfill, mapping: MappingStatus.UnmappedProduct);
        await SeedAsync("c", status: SalesOrderStatus.Fulfilled, mapping: MappingStatus.UnmappedProduct);

        var page = await Repository().SearchAsync(
            _tenantId,
            new SalesOrderQuery
            {
                Status = SalesOrderStatus.ReadyToFulfill,
                MappingStatus = MappingStatus.UnmappedProduct
            });

        // The work queue a merchant actually cares about: ready to ship but not yet mapped.
        page.Items.Should().ContainSingle();
        page.Items[0].ExternalOrderId.Should().Be("b");
    }

    [Fact]
    public async Task UpdatedSince_filters_on_the_providers_timestamp()
    {
        await SeedAsync("stale", sourceLastUpdatedAt: DateTime.UtcNow.AddDays(-5));
        await SeedAsync("recent", sourceLastUpdatedAt: DateTime.UtcNow.AddHours(-1));

        var page = await Repository().SearchAsync(
            _tenantId, new SalesOrderQuery { UpdatedSince = DateTime.UtcNow.AddHours(-2) });

        // The provider's timestamp, not ours. A consumer polling this endpoint for changes needs
        // the same clock the orders themselves are ordered by.
        page.Items.Should().ContainSingle();
        page.Items[0].ExternalOrderId.Should().Be("recent");
    }

    [Fact]
    public async Task The_provider_code_comes_back_with_each_order()
    {
        await SeedAsync("one");

        var page = await Repository().SearchAsync(_tenantId, new SalesOrderQuery());

        // Projected in the same query rather than resolved per row — a page is up to 200 orders.
        page.Items[0].Connection!.Provider!.Code.Should().Be("AMAZON_SP");
    }

    [Fact]
    public async Task Detail_includes_lines_and_the_provider_and_stays_tenant_scoped()
    {
        var id = await SeedAsync("with-lines", withLine: true);

        var order = await Repository().GetAsync(_tenantId, id);

        order.Should().NotBeNull();
        order!.Lines.Should().ContainSingle();
        order.Connection!.Provider!.Code.Should().Be("AMAZON_SP");

        // Another tenant does not find it, rather than finding it and being refused.
        (await Repository().GetAsync(_otherTenantId, id)).Should().BeNull();
    }

    [Fact]
    public async Task An_empty_result_is_an_empty_page_not_an_error()
    {
        var page = await Repository().SearchAsync(
            _tenantId, new SalesOrderQuery { Status = SalesOrderStatus.Cancelled });

        page.Items.Should().BeEmpty();
        page.Total.Should().Be(0);
    }

    private async Task<Guid> SeedAsync(
        string reference,
        int? tenantId = null,
        IntegrationConnection? connection = null,
        SalesOrderStatus status = SalesOrderStatus.ReadyToFulfill,
        MappingStatus mapping = MappingStatus.Mapped,
        DateTime? purchaseDate = null,
        DateTime? sourceLastUpdatedAt = null,
        bool withLine = false)
    {
        await using var db = _pg.CreateContext();

        var order = new SalesOrder
        {
            IntegrationConnectionId = (connection ?? _connectionA).Id,
            TenantId = tenantId ?? _tenantId,
            ExternalOrderId = reference,
            MarketplaceId = "ATVPDKIKX0DER",
            Status = status,
            ExternalStatusRaw = "UNSHIPPED",
            FulfillmentChannel = FulfillmentChannel.MerchantFulfilled,
            MappingStatus = mapping,
            ProgramsRawJson = """["PRIME"]""",
            PurchaseDate = purchaseDate ?? DateTime.UtcNow.AddDays(-1),
            SourceLastUpdatedAt = sourceLastUpdatedAt ?? DateTime.UtcNow.AddHours(-1),
            OrderTotalAmount = 48.97m,
            CurrencyCode = "USD"
        };

        if (withLine)
        {
            order.Lines.Add(new SalesOrderLine
            {
                ExternalLineId = "L1", SellerSku = "SKU-1", QuantityOrdered = 1,
                MappingStatus = MappingStatus.Mapped, ItemPrice = 48.97m, CurrencyCode = "USD"
            });
        }

        db.SalesOrders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}
