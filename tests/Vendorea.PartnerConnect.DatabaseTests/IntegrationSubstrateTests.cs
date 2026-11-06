using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// Phase 1 substrate constraints, proven against real PostgreSQL.
/// </summary>
/// <remarks>
/// These cannot be tested on the InMemory provider: it does not enforce unique indexes, filtered
/// indexes or transactions, so every assertion here would pass vacuously. The uniqueness rules
/// are the mechanism that keeps one Amazon order mapped to one Merchant360 sales order, so a
/// vacuous pass would be worse than no test.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class IntegrationSubstrateTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private int _orgId;
    private int _tenantId;
    private int _otherTenantId;
    private int _providerId;

    public IntegrationSubstrateTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync(
            "IntegrationCredentials", "IntegrationSyncStates",
            "IntegrationConnections", "IntegrationProviders",
            "Tenants", "Organizations");

        await using var db = _pg.CreateContext();

        var org = new Organization { Code = "ACME", Name = "Acme", Status = OrganizationStatus.Active };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        _orgId = org.Id;

        var tenant = new Tenant { OrganizationId = org.Id, Code = "D1", Name = "Dealer1" };
        var other = new Tenant { OrganizationId = org.Id, Code = "D2", Name = "Dealer2" };
        db.Tenants.AddRange(tenant, other);

        var provider = new IntegrationProvider
        {
            Code = "AMAZON_SP",
            Name = "Amazon Selling Partner",
            Kind = IntegrationProviderKind.Marketplace,
            AuthKind = IntegrationAuthKind.OAuth2,
            Capabilities = IntegrationCapabilities.SalesOrderIngest | IntegrationCapabilities.FulfillmentPush
        };
        db.IntegrationProviders.Add(provider);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _otherTenantId = other.Id;
        _providerId = provider.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private IntegrationConnection NewConnection(int tenantId, string? externalAccountId) => new()
    {
        OrganizationId = _orgId,
        TenantId = tenantId,
        IntegrationProviderId = _providerId,
        ExternalAccountId = externalAccountId,
        Status = externalAccountId is null
            ? IntegrationConnectionStatus.Pending
            : IntegrationConnectionStatus.Connected
    };

    // ---- Matrix 1.1: the filtered unique index ----

    [Fact]
    public async Task Several_pending_connections_are_allowed()
    {
        // A user who starts the consent flow twice leaves two Pending rows, both with a null
        // external account id. An unfiltered unique index would reject the second.
        await using var db = _pg.CreateContext();
        db.IntegrationConnections.Add(NewConnection(_tenantId, null));
        db.IntegrationConnections.Add(NewConnection(_tenantId, null));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Same_account_twice_for_one_tenant_is_rejected()
    {
        // This is what stops a reconnect minting a second connection, losing the watermark and
        // re-importing every order as new.
        await using var db = _pg.CreateContext();
        db.IntegrationConnections.Add(NewConnection(_tenantId, "A3SELLER001"));
        await db.SaveChangesAsync();

        db.IntegrationConnections.Add(NewConnection(_tenantId, "A3SELLER001"));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Same_account_on_a_different_tenant_is_allowed()
    {
        // Legitimate: two dealers under one org may each authorise the same Amazon account.
        await using var db = _pg.CreateContext();
        db.IntegrationConnections.Add(NewConnection(_tenantId, "A3SHARED"));
        await db.SaveChangesAsync();

        db.IntegrationConnections.Add(NewConnection(_otherTenantId, "A3SHARED"));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    // ---- Matrix 1.2 ----

    [Fact]
    public async Task Connection_requires_a_tenant()
    {
        await using var db = _pg.CreateContext();
        var sql = "INSERT INTO \"IntegrationConnections\" " +
                  "(\"Id\",\"OrganizationId\",\"TenantId\",\"IntegrationProviderId\",\"Status\",\"GrantedCapabilities\",\"CreatedAt\") " +
                  "VALUES (gen_random_uuid(), {0}, NULL, {1}, 'Pending', 0, now())";

        var act = async () => await db.Database.ExecuteSqlRawAsync(sql, _orgId, _providerId);

        await act.Should().ThrowAsync<Exception>("tenant isolation is a schema invariant, not a convention");
    }

    // ---- Sync state key ----

    [Fact]
    public async Task Watermarks_are_per_capability_and_per_marketplace()
    {
        await using var db = _pg.CreateContext();
        var conn = NewConnection(_tenantId, "A3WATERMARK");
        db.IntegrationConnections.Add(conn);
        await db.SaveChangesAsync();

        db.IntegrationSyncStates.AddRange(
            new IntegrationSyncState { IntegrationConnectionId = conn.Id, Capability = IntegrationCapabilities.SalesOrderIngest, MarketplaceId = "ATVPDKIKX0DER" },
            new IntegrationSyncState { IntegrationConnectionId = conn.Id, Capability = IntegrationCapabilities.SalesOrderIngest, MarketplaceId = "A2EUQ1WTGCTBG2" },
            new IntegrationSyncState { IntegrationConnectionId = conn.Id, Capability = IntegrationCapabilities.ListingSync, MarketplaceId = "ATVPDKIKX0DER" });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync("orders and listings advance independently, per marketplace");
    }

    [Fact]
    public async Task Duplicate_watermark_for_one_capability_and_marketplace_is_rejected()
    {
        await using var db = _pg.CreateContext();
        var conn = NewConnection(_tenantId, "A3DUPE");
        db.IntegrationConnections.Add(conn);
        await db.SaveChangesAsync();

        db.IntegrationSyncStates.Add(new IntegrationSyncState
        { IntegrationConnectionId = conn.Id, Capability = IntegrationCapabilities.SalesOrderIngest, MarketplaceId = null });
        await db.SaveChangesAsync();

        // NULLS NOT DISTINCT: a second null-marketplace row for the same capability must collide.
        db.IntegrationSyncStates.Add(new IntegrationSyncState
        { IntegrationConnectionId = conn.Id, Capability = IntegrationCapabilities.SalesOrderIngest, MarketplaceId = null });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ---- Credentials ----

    [Fact]
    public async Task Deleting_a_connection_removes_its_credentials()
    {
        await using var db = _pg.CreateContext();
        var conn = NewConnection(_tenantId, "A3CASCADE");
        db.IntegrationConnections.Add(conn);
        await db.SaveChangesAsync();

        db.IntegrationCredentials.Add(new IntegrationCredential
        {
            IntegrationConnectionId = conn.Id,
            CredentialType = IntegrationCredentialType.RefreshToken,
            ProtectedValue = "enc:v1:stub"
        });
        await db.SaveChangesAsync();

        db.IntegrationConnections.Remove(conn);
        await db.SaveChangesAsync();

        // A disconnected account must not leave a usable refresh token behind.
        (await db.IntegrationCredentials.CountAsync()).Should().Be(0);
    }

    // ---- Matrix R0.16-R0.18: transactional outbox ----

    [Fact]
    public async Task Staged_outbox_message_commits_with_the_caller_transaction()
    {
        await using var db = _pg.CreateContext();
        var repo = new OutboxRepository(db);

        await using var tx = await db.Database.BeginTransactionAsync();
        var conn = NewConnection(_tenantId, "A3TXCOMMIT");
        db.IntegrationConnections.Add(conn);
        repo.Stage(new OutboxMessage { MessageType = "Test", Payload = "{}" });
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        await using var verify = _pg.CreateContext();
        (await verify.IntegrationConnections.CountAsync(c => c.ExternalAccountId == "A3TXCOMMIT")).Should().Be(1);
        (await verify.OutboxMessages.CountAsync(m => m.MessageType == "Test")).Should().Be(1);
    }

    [Fact]
    public async Task Rollback_leaves_neither_the_snapshot_nor_the_event()
    {
        // The guarantee that matters: no orphan event for an order that does not exist, and no
        // order whose event was lost. Incidental shared-DbContext behaviour cannot promise this.
        await using var db = _pg.CreateContext();
        var repo = new OutboxRepository(db);

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            db.IntegrationConnections.Add(NewConnection(_tenantId, "A3TXROLLBACK"));
            repo.Stage(new OutboxMessage { MessageType = "RolledBack", Payload = "{}" });
            await db.SaveChangesAsync();
            await tx.RollbackAsync();
        }

        await using var verify = _pg.CreateContext();
        (await verify.IntegrationConnections.CountAsync(c => c.ExternalAccountId == "A3TXROLLBACK")).Should().Be(0);
        (await verify.OutboxMessages.CountAsync(m => m.MessageType == "RolledBack")).Should().Be(0);
    }

    [Fact]
    public async Task Legacy_AddAsync_still_commits_on_its_own()
    {
        // Every existing caller - the SPR pipeline, the M360 callbacks - relies on this.
        await using var db = _pg.CreateContext();
        var repo = new OutboxRepository(db);

        await repo.AddAsync(new OutboxMessage { MessageType = "LegacyPath", Payload = "{}" });

        await using var verify = _pg.CreateContext();
        (await verify.OutboxMessages.CountAsync(m => m.MessageType == "LegacyPath")).Should().Be(1);
    }
}
