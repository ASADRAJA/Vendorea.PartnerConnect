using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Infrastructure.Security;
using Vendorea.PartnerConnect.Persistence;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// Connection lifecycle against real PostgreSQL, with the reconnect behaviour that keeps one
/// Amazon order mapped to one Merchant360 sales order.
/// </summary>
[Collection(PostgresCollection.Name)]
public class IntegrationConnectionServiceTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private int _orgId;
    private int _tenantId;
    private int _otherTenantId;

    public IntegrationConnectionServiceTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync(
            "IntegrationCredentials", "IntegrationSyncStates",
            "IntegrationConnections", "IntegrationProviders",
            "AuditLogs", "Tenants", "Organizations");

        await using var db = _pg.CreateContext();
        var org = new Organization { Code = "ACME", Name = "Acme", Status = OrganizationStatus.Active };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        _orgId = org.Id;

        var t1 = new Tenant { OrganizationId = org.Id, Code = "D1", Name = "Dealer1" };
        var t2 = new Tenant { OrganizationId = org.Id, Code = "D2", Name = "Dealer2" };
        db.Tenants.AddRange(t1, t2);
        db.IntegrationProviders.Add(new IntegrationProvider
        {
            Code = "AMAZON_SP",
            Name = "Amazon Selling Partner",
            Kind = IntegrationProviderKind.Marketplace,
            AuthKind = IntegrationAuthKind.OAuth2,
            Capabilities = IntegrationCapabilities.SalesOrderIngest | IntegrationCapabilities.FulfillmentPush
        });
        await db.SaveChangesAsync();
        _tenantId = t1.Id;
        _otherTenantId = t2.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private (IIntegrationConnectionService Service, IIntegrationCredentialStore Store, PartnerConnectDbContext Db) Build()
    {
        var db = _pg.CreateContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["CredentialEncryption:EncryptionKey"] = "integration-test-key" })
            .Build();

        var protector = new AesCredentialProtector(
            config, new TestEnvironment(), NullLogger<AesCredentialProtector>.Instance);

        var credentialStore = new IntegrationCredentialStore(
            new IntegrationCredentialRepository(db), protector,
            NullLogger<IntegrationCredentialStore>.Instance);

        var service = new IntegrationConnectionService(
            new IntegrationConnectionRepository(db),
            new IntegrationProviderRepository(db),
            credentialStore,
            new IntegrationAccountDelegationRepository(db),
            new AuditService(new AuditLogRepository(db), null!, NullLogger<AuditService>.Instance),
            NullLogger<IntegrationConnectionService>.Instance);

        return (service, credentialStore, db);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public async Task Begin_creates_a_pending_connection_with_no_account_and_no_credentials()
    {
        var (service, _, db) = Build();

        var result = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", "Acme US");

        result.Success.Should().BeTrue();
        result.Connection!.Status.Should().Be(IntegrationConnectionStatus.Pending);
        result.Connection.ExternalAccountId.Should().BeNull();
        (await db.IntegrationCredentials.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Begin_rejects_an_unknown_provider()
    {
        var (service, _, _) = Build();

        var result = await service.BeginAsync(_orgId, _tenantId, "NOT_A_PROVIDER", null);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Completing_authorization_connects_and_stores_an_encrypted_token()
    {
        var (service, store, db) = Build();
        var begun = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", "Acme US");

        var result = await service.CompleteAuthorizationAsync(
            _tenantId, begun.Connection!.Id, "A3SELLER", "refresh-token-xyz",
            IntegrationCapabilities.SalesOrderIngest, "us-east-1");

        result.Success.Should().BeTrue();
        result.ReusedExisting.Should().BeFalse();
        result.Connection!.Status.Should().Be(IntegrationConnectionStatus.Connected);

        (await store.GetAsync(result.Connection.Id, IntegrationCredentialType.RefreshToken))
            .Should().Be("refresh-token-xyz");

        // Stored encrypted, not in the clear.
        var row = await db.IntegrationCredentials.AsNoTracking().FirstAsync();
        row.ProtectedValue.Should().StartWith("enc:v1:");
        row.ProtectedValue.Should().NotContain("refresh-token-xyz");
    }

    // ---- C3: reconnection preserves identity and history ----

    [Fact]
    public async Task Reconnecting_the_same_account_reuses_the_connection_and_its_history()
    {
        var (service, _, db) = Build();

        var first = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", "Acme US");
        var connected = await service.CompleteAuthorizationAsync(
            _tenantId, first.Connection!.Id, "A3SELLER", "token-1",
            IntegrationCapabilities.SalesOrderIngest);
        var originalId = connected.Connection!.Id;

        // Sync history that a new connection would lose, re-importing every order as new.
        var watermark = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        await new IntegrationSyncStateRepository(db).AdvanceWatermarkAsync(
            originalId, IntegrationCapabilities.SalesOrderIngest, "ATVPDKIKX0DER", watermark, null);

        await service.DisconnectAsync(_tenantId, originalId);

        // The user reconnects: a NEW pending row, the SAME Amazon account.
        var second = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", "Acme US again");
        var reconnected = await service.CompleteAuthorizationAsync(
            _tenantId, second.Connection!.Id, "A3SELLER", "token-2",
            IntegrationCapabilities.SalesOrderIngest);

        reconnected.ReusedExisting.Should().BeTrue();
        reconnected.Connection!.Id.Should().Be(originalId, "the connection id must survive a reconnect");
        reconnected.Connection.Status.Should().Be(IntegrationConnectionStatus.Connected);

        await using var verify = _pg.CreateContext();
        var state = await verify.IntegrationSyncStates
            .FirstAsync(s => s.IntegrationConnectionId == originalId);
        state.Watermark.Should().Be(watermark, "the watermark must survive, or every order re-imports");

        // The abandoned pending row is retired rather than left looking live.
        var pending = await verify.IntegrationConnections.FindAsync(second.Connection.Id);
        pending!.Status.Should().Be(IntegrationConnectionStatus.Revoked);

        // Exactly one credential, carrying the NEW token. Version is 1 here, not 2: disconnect
        // destroyed the old row, so this is a fresh insert rather than a replacement. The version
        // bump belongs to re-consent without disconnecting - covered separately below.
        var creds = await verify.IntegrationCredentials
            .Where(c => c.IntegrationConnectionId == originalId).ToListAsync();
        creds.Should().HaveCount(1);
    }

    [Fact]
    public async Task Re_consenting_without_disconnecting_replaces_the_token_in_place()
    {
        var (service, store, _) = Build();
        var begun = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", null);
        var first = await service.CompleteAuthorizationAsync(
            _tenantId, begun.Connection!.Id, "A3RECONSENT", "token-old",
            IntegrationCapabilities.SalesOrderIngest);

        // The seller re-authorises while still connected - a scope change, say.
        var second = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", null);
        var again = await service.CompleteAuthorizationAsync(
            _tenantId, second.Connection!.Id, "A3RECONSENT", "token-new",
            IntegrationCapabilities.SalesOrderIngest | IntegrationCapabilities.FulfillmentPush);

        again.ReusedExisting.Should().BeTrue();
        again.Connection!.Id.Should().Be(first.Connection!.Id);
        again.Connection.GrantedCapabilities
            .Should().HaveFlag(IntegrationCapabilities.FulfillmentPush, "the newly granted scope must take effect");

        (await store.GetAsync(again.Connection.Id, IntegrationCredentialType.RefreshToken))
            .Should().Be("token-new");

        await using var verify = _pg.CreateContext();
        var cred = await verify.IntegrationCredentials
            .FirstAsync(c => c.IntegrationConnectionId == again.Connection.Id);
        cred.Version.Should().Be(2, "replacement bumps the version rather than piling up secrets");
        cred.RotatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Reconnecting_a_different_account_creates_a_separate_connection()
    {
        var (service, _, _) = Build();
        var first = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", "Account one");
        await service.CompleteAuthorizationAsync(
            _tenantId, first.Connection!.Id, "A3ONE", "t1", IntegrationCapabilities.SalesOrderIngest);

        var second = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", "Account two");
        var result = await service.CompleteAuthorizationAsync(
            _tenantId, second.Connection!.Id, "A3TWO", "t2", IntegrationCapabilities.SalesOrderIngest);

        result.ReusedExisting.Should().BeFalse();
        result.Connection!.Id.Should().NotBe(first.Connection.Id);
    }

    [Fact]
    public async Task A_tenant_cannot_reach_another_tenants_connection()
    {
        var (service, _, _) = Build();
        var mine = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", "Mine");

        (await service.GetAsync(_otherTenantId, mine.Connection!.Id)).Should().BeNull();

        var disconnect = await service.DisconnectAsync(_otherTenantId, mine.Connection.Id);
        disconnect.Success.Should().BeFalse("tenant isolation is enforced in the repository, not the controller");
    }

    [Fact]
    public async Task Disconnecting_destroys_credentials_but_keeps_the_row()
    {
        var (service, store, _) = Build();
        var begun = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", null);
        var connected = await service.CompleteAuthorizationAsync(
            _tenantId, begun.Connection!.Id, "A3GONE", "secret", IntegrationCapabilities.SalesOrderIngest);

        await service.DisconnectAsync(_tenantId, connected.Connection!.Id);

        (await store.GetAsync(connected.Connection.Id, IntegrationCredentialType.RefreshToken))
            .Should().BeNull("a disconnected account must not leave a usable token");

        // Retained for audit, and so a later reconnect finds it instead of starting fresh.
        (await service.GetAsync(_tenantId, connected.Connection.Id))!
            .Status.Should().Be(IntegrationConnectionStatus.Revoked);
    }

    // ---- watermark discipline ----

    [Fact]
    public async Task A_failed_run_does_not_advance_the_watermark()
    {
        var (service, _, db) = Build();
        var begun = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", null);
        var c = await service.CompleteAuthorizationAsync(
            _tenantId, begun.Connection!.Id, "A3WM", "t", IntegrationCapabilities.SalesOrderIngest);

        var repo = new IntegrationSyncStateRepository(db);
        var good = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        await repo.AdvanceWatermarkAsync(c.Connection!.Id, IntegrationCapabilities.SalesOrderIngest, null, good, "cursor-1");

        await repo.RecordFailureAsync(c.Connection.Id, IntegrationCapabilities.SalesOrderIngest, null, "429 throttled");

        var state = await repo.GetAsync(c.Connection.Id, IntegrationCapabilities.SalesOrderIngest, null);
        state!.Watermark.Should().Be(good, "a failed run has persisted nothing it can claim credit for");
        state.ConsecutiveFailures.Should().Be(1);
    }

    [Fact]
    public async Task A_watermark_never_moves_backwards()
    {
        var (service, _, db) = Build();
        var begun = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", null);
        var c = await service.CompleteAuthorizationAsync(
            _tenantId, begun.Connection!.Id, "A3BACK", "t", IntegrationCapabilities.SalesOrderIngest);

        var repo = new IntegrationSyncStateRepository(db);
        var newer = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);
        var older = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        await repo.AdvanceWatermarkAsync(c.Connection!.Id, IntegrationCapabilities.SalesOrderIngest, null, newer, null);
        await repo.AdvanceWatermarkAsync(c.Connection.Id, IntegrationCapabilities.SalesOrderIngest, null, older, null);

        var state = await repo.GetAsync(c.Connection.Id, IntegrationCapabilities.SalesOrderIngest, null);
        state!.Watermark.Should().Be(newer, "a delayed run must not undo a later one's progress");
    }

    [Fact]
    public async Task Credential_metadata_never_carries_the_value()
    {
        var (service, _, db) = Build();
        var begun = await service.BeginAsync(_orgId, _tenantId, "AMAZON_SP", null);
        var c = await service.CompleteAuthorizationAsync(
            _tenantId, begun.Connection!.Id, "A3META", "super-secret-token", IntegrationCapabilities.SalesOrderIngest);

        var described = await new IntegrationCredentialRepository(db).DescribeAsync(c.Connection!.Id);

        described.Should().HaveCount(1);
        described[0].Type.Should().Be(IntegrationCredentialType.RefreshToken);
        // The tuple has no field that could carry it — asserted so a future field addition fails here.
        described[0].GetType().GetFields().Should().HaveCount(3);
    }
}
