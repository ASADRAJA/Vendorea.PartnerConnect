using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// The reconciliation reads, against real PostgreSQL — test matrix 3.8.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SalesOrderReconciliationRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private IntegrationConnection _connection = null!;
    private IntegrationConnection _otherConnection = null!;
    private int _tenantId;

    private static readonly SalesOrderStatus[] Terminal =
        { SalesOrderStatus.Fulfilled, SalesOrderStatus.Cancelled };

    public SalesOrderReconciliationRepositoryTests(PostgresFixture pg) => _pg = pg;

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

        var tenant = new Tenant { OrganizationId = org.Id, Code = "D1", Name = "Dealer One" };
        db.Tenants.Add(tenant);
        var provider = new IntegrationProvider
        {
            Code = "AMAZON_SP", Name = "Amazon", Kind = IntegrationProviderKind.Marketplace,
            AuthKind = IntegrationAuthKind.OAuth2
        };
        db.IntegrationProviders.Add(provider);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;

        var a = new IntegrationConnection
        {
            OrganizationId = org.Id, TenantId = tenant.Id, IntegrationProviderId = provider.Id,
            ExternalAccountId = "SELLER-A", Status = IntegrationConnectionStatus.Connected,
            GrantedCapabilities = IntegrationCapabilities.SalesOrderIngest
        };
        var b = new IntegrationConnection
        {
            OrganizationId = org.Id, TenantId = tenant.Id, IntegrationProviderId = provider.Id,
            ExternalAccountId = "SELLER-B", Status = IntegrationConnectionStatus.Connected,
            GrantedCapabilities = IntegrationCapabilities.SalesOrderIngest
        };
        db.IntegrationConnections.AddRange(a, b);
        await db.SaveChangesAsync();

        _connection = a;
        _otherConnection = b;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Only_stale_non_terminal_orders_come_back_oldest_first()
    {
        await SeedAsync("fresh", SalesOrderStatus.ReadyToFulfill, DateTime.UtcNow.AddMinutes(-5));
        await SeedAsync("stale-newer", SalesOrderStatus.Pending, DateTime.UtcNow.AddHours(-8));
        await SeedAsync("stale-older", SalesOrderStatus.ReadyToFulfill, DateTime.UtcNow.AddHours(-30));
        await SeedAsync("shipped", SalesOrderStatus.Fulfilled, DateTime.UtcNow.AddHours(-30));
        await SeedAsync("cancelled", SalesOrderStatus.Cancelled, DateTime.UtcNow.AddHours(-30));

        await using var db = _pg.CreateContext();
        var repository = new SalesOrderReconciliationRepository(db);

        var stale = await repository.GetStaleAsync(
            _connection.Id, DateTime.UtcNow.AddHours(-6), Terminal, limit: 10);

        // Terminal orders excluded, fresh ones excluded, and the longest-stuck one first — it is
        // the most likely to be genuinely wrong.
        stale.Select(o => o.ExternalOrderId)
            .Should().BeEquivalentTo(
                new[] { "stale-older", "stale-newer" },
                options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task An_unknown_status_is_treated_as_non_terminal_and_re_read()
    {
        await SeedAsync("unmapped", SalesOrderStatus.Unknown, DateTime.UtcNow.AddHours(-30));

        await using var db = _pg.CreateContext();
        var repository = new SalesOrderReconciliationRepository(db);

        var stale = await repository.GetStaleAsync(
            _connection.Id, DateTime.UtcNow.AddHours(-6), Terminal, limit: 10);

        // A status we could not map is exactly the case worth looking at again: it may have been
        // a transient provider value, and either way somebody should know.
        stale.Should().ContainSingle();
        stale[0].Status.Should().Be(SalesOrderStatus.Unknown);
    }

    [Fact]
    public async Task The_batch_is_bounded()
    {
        for (var i = 0; i < 10; i++)
        {
            await SeedAsync($"stale-{i}", SalesOrderStatus.ReadyToFulfill, DateTime.UtcNow.AddHours(-30));
        }

        await using var db = _pg.CreateContext();
        var repository = new SalesOrderReconciliationRepository(db);

        var stale = await repository.GetStaleAsync(
            _connection.Id, DateTime.UtcNow.AddHours(-6), Terminal, limit: 4);

        // Bounded so a sweep does not eat the getOrder budget the live path needs.
        stale.Should().HaveCount(4);
    }

    [Fact]
    public async Task Staleness_is_scoped_to_one_connection()
    {
        await SeedAsync("mine", SalesOrderStatus.ReadyToFulfill, DateTime.UtcNow.AddHours(-30));
        await SeedAsync("theirs", SalesOrderStatus.ReadyToFulfill, DateTime.UtcNow.AddHours(-30),
            _otherConnection);

        await using var db = _pg.CreateContext();
        var repository = new SalesOrderReconciliationRepository(db);

        var stale = await repository.GetStaleAsync(
            _connection.Id, DateTime.UtcNow.AddHours(-6), Terminal, limit: 10);

        // Each seller account has its own rate-limit budget, so reconciliation is per connection.
        stale.Should().ContainSingle();
        stale[0].ExternalOrderId.Should().Be("mine");
    }

    [Fact]
    public async Task Known_order_ids_come_back_in_one_query()
    {
        await SeedAsync("A", SalesOrderStatus.Fulfilled, DateTime.UtcNow);
        await SeedAsync("B", SalesOrderStatus.Fulfilled, DateTime.UtcNow);

        await using var db = _pg.CreateContext();
        var repository = new SalesOrderReconciliationRepository(db);

        var known = await repository.GetKnownExternalIdsAsync(
            _connection.Id, new[] { "A", "B", "MISSING-1", "MISSING-2" });

        // A page is up to a hundred orders and the gap search runs per connection, so one query
        // rather than a lookup per order is a hundredfold difference.
        known.Should().BeEquivalentTo(new[] { "A", "B" });
    }

    [Fact]
    public async Task Known_ids_do_not_leak_between_connections()
    {
        await SeedAsync("SHARED-ID", SalesOrderStatus.Fulfilled, DateTime.UtcNow, _otherConnection);

        await using var db = _pg.CreateContext();
        var repository = new SalesOrderReconciliationRepository(db);

        var known = await repository.GetKnownExternalIdsAsync(_connection.Id, new[] { "SHARED-ID" });

        // Amazon order ids are not globally unique across sellers. Treating another connection's
        // order as already-ingested would make the gap search skip a genuinely missing order.
        known.Should().BeEmpty();
    }

    [Fact]
    public async Task An_empty_candidate_list_does_not_query_at_all()
    {
        await using var db = _pg.CreateContext();
        var repository = new SalesOrderReconciliationRepository(db);

        (await repository.GetKnownExternalIdsAsync(_connection.Id, Array.Empty<string>()))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task The_reconciliation_job_has_a_schedule_bound_to_it()
    {
        await using var db = _pg.CreateContext();

        var job = await db.ScheduledJobs.AsNoTracking()
            .SingleOrDefaultAsync(j => j.JobKey == SalesOrderReconciliationJobHandler.Key);

        // Registered without a row would mean a sweep that never runs — no error, no log.
        job.Should().NotBeNull();
        job!.IsEnabled.Should().BeTrue();

        // Staggered after the three retention sweeps so nightly housekeeping does not contend.
        job.CronExpression.Should().Be("15 4 * * *");
    }

    private async Task SeedAsync(
        string reference, SalesOrderStatus status, DateTime lastSyncedAt,
        IntegrationConnection? connection = null)
    {
        connection ??= _connection;

        await using var db = _pg.CreateContext();
        db.SalesOrders.Add(new SalesOrder
        {
            IntegrationConnectionId = connection.Id,
            TenantId = _tenantId,
            ExternalOrderId = reference,
            Status = status,
            FulfillmentChannel = FulfillmentChannel.MerchantFulfilled,
            MappingStatus = MappingStatus.Mapped,
            PurchaseDate = DateTime.UtcNow.AddDays(-3),
            SourceLastUpdatedAt = DateTime.UtcNow.AddDays(-3),
            LastSyncedAt = lastSyncedAt
        });
        await db.SaveChangesAsync();
    }
}
