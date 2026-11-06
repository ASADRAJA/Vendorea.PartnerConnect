using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Vendorea.PartnerConnect.Infrastructure.Security;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// Seller-account ownership and tenant isolation, enforced by the schema.
/// </summary>
/// <remarks>
/// Vendorea is a software and integration provider. It is never the merchant of record, the
/// purchasing entity, or an intermediary receiving a dealer's Amazon proceeds — each dealer
/// transacts through its own authorized account, mapped to its own Merchant360 tenant.
///
/// These tests assert the invariants at the database level rather than through a service, because
/// a service check is a habit and an index is a guarantee. Shared infrastructure — notably the
/// rate-limit bucket, which is keyed per seller account by necessity — is checked here for the
/// one property that matters: that it holds no tenant data.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class SellerAccountOwnershipTests : IAsyncLifetime
{
    private const string SellerA = "A3EXAMPLE1SELLER";

    private readonly PostgresFixture _pg;
    private int _tenantA;
    private int _tenantB;
    private int _providerId;
    private int _orgId;

    public SellerAccountOwnershipTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync(
            "SalesOrderStatusHistory", "SalesOrderLines", "SalesOrders",
            "ProviderRateLimitBuckets", "IntegrationAccountDelegations",
            "IntegrationOAuthStates", "IntegrationCredentials", "IntegrationSyncStates",
            "IntegrationConnections", "IntegrationProviders",
            "AuditLogs", "Tenants", "Organizations");

        await using var db = _pg.CreateContext();

        var org = new Organization { Code = "ACME", Name = "Acme", Status = OrganizationStatus.Active };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        _orgId = org.Id;

        var a = new Tenant { OrganizationId = org.Id, Code = "D1", Name = "Dealer One" };
        var b = new Tenant { OrganizationId = org.Id, Code = "D2", Name = "Dealer Two" };
        db.Tenants.AddRange(a, b);

        var provider = new IntegrationProvider
        {
            Code = "AMAZON_SP", Name = "Amazon", Kind = IntegrationProviderKind.Marketplace,
            AuthKind = IntegrationAuthKind.OAuth2,
            Capabilities = IntegrationCapabilities.SalesOrderIngest
        };
        db.IntegrationProviders.Add(provider);
        await db.SaveChangesAsync();

        _tenantA = a.Id;
        _tenantB = b.Id;
        _providerId = provider.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private IntegrationConnection Connection(
        int tenantId,
        string? account = SellerA,
        IntegrationConnectionStatus status = IntegrationConnectionStatus.Connected,
        IntegrationAccountOwnership ownership = IntegrationAccountOwnership.Primary) => new()
    {
        OrganizationId = _orgId,
        TenantId = tenantId,
        IntegrationProviderId = _providerId,
        ExternalAccountId = account,
        Status = status,
        AccountOwnership = ownership,
        GrantedCapabilities = IntegrationCapabilities.SalesOrderIngest
    };

    // ---- one tenant per seller account ----

    [Fact]
    public async Task The_database_refuses_a_second_primary_owner_for_one_seller_account()
    {
        await using (var db = _pg.CreateContext())
        {
            db.IntegrationConnections.Add(Connection(_tenantA));
            await db.SaveChangesAsync();
        }

        await using var second = _pg.CreateContext();
        second.IntegrationConnections.Add(Connection(_tenantB));

        // Not a service-layer habit: an index. A dealer's Amazon account, its orders and its
        // proceeds belong to one Merchant360 tenant, and the schema will not record otherwise
        // however the write arrives.
        var act = async () => await second.SaveChangesAsync();
        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task A_delegated_row_may_coexist_with_the_owner()
    {
        await using var db = _pg.CreateContext();

        db.IntegrationConnections.Add(Connection(_tenantA));
        db.IntegrationConnections.Add(
            Connection(_tenantB, ownership: IntegrationAccountOwnership.Delegated));

        // The index covers Primary rows only, so the deliberate exception is expressible while
        // "exactly one owner" stays guaranteed.
        await db.SaveChangesAsync();

        (await db.IntegrationConnections.CountAsync(
            c => c.ExternalAccountId == SellerA
                 && c.AccountOwnership == IntegrationAccountOwnership.Primary))
            .Should().Be(1);
    }

    [Fact]
    public async Task A_revoked_owner_releases_the_account()
    {
        await using var db = _pg.CreateContext();

        db.IntegrationConnections.Add(
            Connection(_tenantA, status: IntegrationConnectionStatus.Revoked));
        db.IntegrationConnections.Add(Connection(_tenantB));

        // A tenant that disconnected has released the account; a later claim is a transfer, and
        // the revoked row keeps the first tenant's history where it belongs.
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task An_expired_connection_still_holds_its_account()
    {
        await using (var db = _pg.CreateContext())
        {
            db.IntegrationConnections.Add(
                Connection(_tenantA, status: IntegrationConnectionStatus.Expired));
            await db.SaveChangesAsync();
        }

        await using var second = _pg.CreateContext();
        second.IntegrationConnections.Add(Connection(_tenantB));

        // Expired means "reconnect me", not "I have let go". A lapsed token must not be an
        // opening for another tenant to take over the account.
        var act = async () => await second.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task One_tenant_may_own_several_seller_accounts()
    {
        await using var db = _pg.CreateContext();

        db.IntegrationConnections.Add(Connection(_tenantA, "SELLER-ONE"));
        db.IntegrationConnections.Add(Connection(_tenantA, "SELLER-TWO"));

        // A dealer selling under more than one Amazon account is ordinary.
        await db.SaveChangesAsync();

        (await db.IntegrationConnections.CountAsync(c => c.TenantId == _tenantA)).Should().Be(2);
    }

    [Fact]
    public async Task Several_pending_connections_can_coexist_before_any_account_is_named()
    {
        await using var db = _pg.CreateContext();

        db.IntegrationConnections.Add(
            Connection(_tenantA, account: null, status: IntegrationConnectionStatus.Pending));
        db.IntegrationConnections.Add(
            Connection(_tenantB, account: null, status: IntegrationConnectionStatus.Pending));

        // A user who abandons consent and tries again is normal, and nothing is claimed until
        // the provider says which account was authorised.
        await db.SaveChangesAsync();
    }

    // ---- one active delegation per delegate ----

    [Fact]
    public async Task Only_one_active_delegation_can_exist_per_delegate_per_account()
    {
        await using (var db = _pg.CreateContext())
        {
            db.IntegrationConnections.Add(Connection(_tenantA));
            db.IntegrationAccountDelegations.Add(new IntegrationAccountDelegation
            {
                IntegrationProviderId = _providerId, ExternalAccountId = SellerA,
                OwnerTenantId = _tenantA, DelegateTenantId = _tenantB,
                GrantedBy = "ops@vendorea.test"
            });
            await db.SaveChangesAsync();
        }

        await using var second = _pg.CreateContext();
        second.IntegrationAccountDelegations.Add(new IntegrationAccountDelegation
        {
            IntegrationProviderId = _providerId, ExternalAccountId = SellerA,
            OwnerTenantId = _tenantA, DelegateTenantId = _tenantB,
            GrantedBy = "someone-else@vendorea.test"
        });

        // Two active grants would make "is this delegated, and by whom?" ambiguous.
        var act = async () => await second.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task A_withdrawn_grant_is_retained_and_a_new_one_may_be_issued()
    {
        await using var db = _pg.CreateContext();

        db.IntegrationAccountDelegations.Add(new IntegrationAccountDelegation
        {
            IntegrationProviderId = _providerId, ExternalAccountId = SellerA,
            OwnerTenantId = _tenantA, DelegateTenantId = _tenantB,
            GrantedBy = "ops@vendorea.test",
            RevokedAt = DateTime.UtcNow.AddDays(-1), RevokedBy = "ops@vendorea.test"
        });
        db.IntegrationAccountDelegations.Add(new IntegrationAccountDelegation
        {
            IntegrationProviderId = _providerId, ExternalAccountId = SellerA,
            OwnerTenantId = _tenantA, DelegateTenantId = _tenantB,
            GrantedBy = "ops@vendorea.test"
        });

        // The index is filtered on RevokedAt so history survives rather than being overwritten —
        // who shared what, when and why is exactly what an audit needs.
        await db.SaveChangesAsync();

        (await db.IntegrationAccountDelegations.CountAsync()).Should().Be(2);
    }

    // ---- shared rate-limit infrastructure carries no tenant data ----

    [Fact]
    public async Task The_rate_limit_bucket_holds_no_tenant_credential_or_order_data()
    {
        await using var db = _pg.CreateContext();

        var limiter = new ProviderRateLimiter(
            db, new FixedPolicy(), NullLogger<ProviderRateLimiter>.Instance);

        await limiter.TryAcquireAsync($"AMAZON_SP:{SellerA}", "searchOrders");

        var columns = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_name = 'ProviderRateLimitBuckets'
                """)
            .ToListAsync();

        // The bucket is keyed per seller account by necessity — Amazon meters that way. What it
        // must never become is a place where two tenants meet: there is no tenant id, no token,
        // no order and no customer field here, and this test exists so none is ever added.
        columns.Should().BeEquivalentTo(new[]
        {
            "Id", "BucketKey", "Operation", "Tokens", "LastRefillAt", "CreatedAt"
        });
    }

    [Fact]
    public async Task Two_tenants_on_one_seller_account_keep_separate_credentials_and_orders()
    {
        var protector = new AesCredentialProtector(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["CredentialEncryption:EncryptionKey"] = "test-key" }).Build(),
            new TestEnvironment(), NullLogger<AesCredentialProtector>.Instance);

        Guid ownerId, delegateId;

        await using (var db = _pg.CreateContext())
        {
            var owner = Connection(_tenantA);
            var delegated = Connection(_tenantB, ownership: IntegrationAccountOwnership.Delegated);
            db.IntegrationConnections.AddRange(owner, delegated);
            await db.SaveChangesAsync();

            ownerId = owner.Id;
            delegateId = delegated.Id;

            var writeStore = new IntegrationCredentialStore(
                new IntegrationCredentialRepository(db), protector,
                NullLogger<IntegrationCredentialStore>.Instance);

            await writeStore.PutAsync(ownerId, IntegrationCredentialType.RefreshToken, "owner-token");
            await writeStore.PutAsync(delegateId, IntegrationCredentialType.RefreshToken, "delegate-token");

            db.SalesOrders.Add(new SalesOrder
            {
                IntegrationConnectionId = ownerId, TenantId = _tenantA,
                ExternalOrderId = "111-0000001-0000001",
                Status = SalesOrderStatus.ReadyToFulfill,
                FulfillmentChannel = FulfillmentChannel.MerchantFulfilled,
                MappingStatus = MappingStatus.UnmappedProduct,
                PurchaseDate = DateTime.UtcNow, SourceLastUpdatedAt = DateTime.UtcNow,
                BuyerName = "Owner's buyer"
            });
            await db.SaveChangesAsync();
        }

        await using var verify = _pg.CreateContext();

        // Sharing an Amazon account through a delegation shares the right to connect it. It does
        // not share a credential, an order or a customer: the delegate authorises the account
        // itself and gets its own everything.
        var credentials = await verify.IntegrationCredentials.ToListAsync();
        credentials.Should().HaveCount(2);
        credentials.Select(c => c.ProtectedValue).Distinct().Should().HaveCount(2);

        var readStore = new IntegrationCredentialStore(
            new IntegrationCredentialRepository(verify), protector,
            NullLogger<IntegrationCredentialStore>.Instance);

        (await readStore.GetAsync(ownerId, IntegrationCredentialType.RefreshToken))
            .Should().Be("owner-token");
        (await readStore.GetAsync(delegateId, IntegrationCredentialType.RefreshToken))
            .Should().Be("delegate-token");

        // The owner's order is reachable only through the owner's tenant.
        var repository = new SalesOrderRepository(verify);
        (await repository.ListAsync(_tenantA, 0, 50)).Should().ContainSingle();
        (await repository.ListAsync(_tenantB, 0, 50)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_credential_cannot_exist_without_a_connection_to_own_it()
    {
        await using var db = _pg.CreateContext();

        db.IntegrationCredentials.Add(new IntegrationCredential
        {
            IntegrationConnectionId = Guid.NewGuid(),   // belongs to nothing
            CredentialType = IntegrationCredentialType.RefreshToken,
            ProtectedValue = "enc:v1:whatever"
        });

        // Credentials hang off a connection, which hangs off exactly one tenant. There is no
        // platform-level or provider-level credential anywhere in this schema — no row through
        // which Vendorea could hold a seller's Amazon access on its own behalf.
        var act = async () => await db.SaveChangesAsync();
        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Finding_an_accounts_owner_returns_a_tenant_id_and_nothing_else()
    {
        await using var db = _pg.CreateContext();
        db.IntegrationConnections.Add(Connection(_tenantA));
        await db.SaveChangesAsync();

        var repository = new IntegrationConnectionRepository(db);

        (await repository.FindOwningTenantAsync(_providerId, SellerA)).Should().Be(_tenantA);
        (await repository.FindOwningTenantAsync(_providerId, "NOBODY-OWNS-THIS")).Should().BeNull();

        // The only cross-tenant lookup in the repository, and it yields an integer. Returning the
        // connection would hand a caller another tenant's row and its navigations.
        (await repository.GetAsync(_tenantB, (await db.IntegrationConnections.FirstAsync()).Id))
            .Should().BeNull();
    }

    private sealed class FixedPolicy : IProviderRateLimitPolicyProvider
    {
        public RateLimitPolicy GetPolicy(string operation) => new(0.0056, 20);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
