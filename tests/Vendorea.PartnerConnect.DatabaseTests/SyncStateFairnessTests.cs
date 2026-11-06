using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// Scheduler fairness across connections — test matrix 3.9 (finding A4).
/// </summary>
/// <remarks>
/// The original scheduler claims jobs, not connections, so one tenant's backlog could hold the
/// worker while everyone else waited. With a provider budget of roughly one call every three
/// minutes, a starved connection is one whose orders never arrive at all.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class SyncStateFairnessTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private readonly List<Guid> _connectionIds = new();

    public SyncStateFairnessTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync(
            "SalesOrderStatusHistory", "SalesOrderLines", "SalesOrders",
            "IntegrationOAuthStates", "IntegrationCredentials", "IntegrationSyncStates",
            "IntegrationConnections", "IntegrationProviders", "Tenants", "Organizations");

        await using var db = _pg.CreateContext();

        var org = new Organization { Code = "ACME", Name = "Acme", Status = OrganizationStatus.Active };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var tenant = new Tenant { OrganizationId = org.Id, Code = "D1", Name = "Dealer1" };
        db.Tenants.Add(tenant);
        var provider = new IntegrationProvider
        {
            Code = "AMAZON_SP", Name = "Amazon", Kind = IntegrationProviderKind.Marketplace,
            AuthKind = IntegrationAuthKind.OAuth2
        };
        db.IntegrationProviders.Add(provider);
        await db.SaveChangesAsync();

        _connectionIds.Clear();
        for (var i = 0; i < 3; i++)
        {
            var connection = new IntegrationConnection
            {
                OrganizationId = org.Id, TenantId = tenant.Id, IntegrationProviderId = provider.Id,
                ExternalAccountId = $"SELLER-{i}", Status = IntegrationConnectionStatus.Connected,
                GrantedCapabilities = IntegrationCapabilities.SalesOrderIngest
            };
            db.IntegrationConnections.Add(connection);
            _connectionIds.Add(connection.Id);
        }

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Connections_are_served_least_recently_attempted_first()
    {
        await using var db = _pg.CreateContext();
        var repository = new IntegrationSyncStateRepository(db);

        // Two have been served; the third never has.
        await repository.RecordAttemptAsync(
            _connectionIds[0], IntegrationCapabilities.SalesOrderIngest, null);
        await Task.Delay(20);
        await repository.RecordAttemptAsync(
            _connectionIds[1], IntegrationCapabilities.SalesOrderIngest, null);

        var order = await repository.OrderByStalenessAsync(
            IntegrationCapabilities.SalesOrderIngest, _connectionIds);

        // The never-attempted one leads: a new seller must not wait behind an established one's
        // backlog. Then the stalest of the rest.
        order[0].Should().Be(_connectionIds[2]);
        order[1].Should().Be(_connectionIds[0]);
        order[2].Should().Be(_connectionIds[1]);
    }

    [Fact]
    public async Task Being_served_moves_a_connection_to_the_back()
    {
        await using var db = _pg.CreateContext();
        var repository = new IntegrationSyncStateRepository(db);

        foreach (var id in _connectionIds)
        {
            await repository.RecordAttemptAsync(id, IntegrationCapabilities.SalesOrderIngest, null);
            await Task.Delay(20);
        }

        var first = (await repository.OrderByStalenessAsync(
            IntegrationCapabilities.SalesOrderIngest, _connectionIds))[0];

        await repository.RecordAttemptAsync(first, IntegrationCapabilities.SalesOrderIngest, null);

        var next = await repository.OrderByStalenessAsync(
            IntegrationCapabilities.SalesOrderIngest, _connectionIds);

        // Without this the same connection is picked every cycle and the rest starve.
        next[0].Should().NotBe(first);
        next[^1].Should().Be(first);
    }

    [Fact]
    public async Task An_attempt_is_recorded_even_when_nothing_was_fetched()
    {
        await using var db = _pg.CreateContext();
        var repository = new IntegrationSyncStateRepository(db);

        await repository.RecordAttemptAsync(
            _connectionIds[0], IntegrationCapabilities.SalesOrderIngest, null);

        var state = await db.IntegrationSyncStates.SingleAsync();

        // A rate-limited connection has still had its turn. Recording only successful runs would
        // put a throttled connection at the head of the queue for ever.
        state.LastAttemptAt.Should().NotBeNull();
        state.Watermark.Should().BeNull();
        state.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task Ordering_is_per_capability()
    {
        await using var db = _pg.CreateContext();
        var repository = new IntegrationSyncStateRepository(db);

        await repository.RecordAttemptAsync(
            _connectionIds[0], IntegrationCapabilities.ListingSync, null);

        var order = await repository.OrderByStalenessAsync(
            IntegrationCapabilities.SalesOrderIngest, _connectionIds);

        // A listing sync must not count as an order poll; the budgets and the work are separate.
        order.Should().HaveCount(3);
        order[0].Should().Be(_connectionIds[0]);
    }

    [Fact]
    public async Task An_empty_candidate_list_is_handled_without_a_query()
    {
        await using var db = _pg.CreateContext();
        var repository = new IntegrationSyncStateRepository(db);

        (await repository.OrderByStalenessAsync(
            IntegrationCapabilities.SalesOrderIngest, Array.Empty<Guid>()))
            .Should().BeEmpty();
    }
}
