using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
/// Seller authorization end to end against real PostgreSQL — test matrix 2.1 to 2.10.
/// </summary>
/// <remarks>
/// The provider is stubbed; everything below it is real. The state store's single-use guarantee
/// rests on a unique index and a persisted <c>UsedAt</c>, neither of which an in-memory provider
/// enforces the same way, so these belong here rather than in the unit suite.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class IntegrationAuthorizationTests : IAsyncLifetime
{
    private const string SellerA = "A3EXAMPLE1SELLER";
    private const string SellerB = "A9OTHERSELLER22";
    private const string RefreshToken = "Atzr|SECRET-REFRESH-TOKEN-VALUE";

    private readonly PostgresFixture _pg;
    private int _orgId;
    private int _tenantId;
    private int _otherTenantId;

    public IntegrationAuthorizationTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync(
            "IntegrationAccountDelegations",
            "IntegrationOAuthStates", "IntegrationCredentials", "IntegrationSyncStates",
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

    // ---- 2.1 single use ----

    [Fact]
    public async Task A_state_cannot_be_used_twice()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var state = await h.IssueStateAsync(_tenantId, connectionId);

        var first = await h.Service.CompleteAsync("AMAZON_SP", state, "code-1", SellerA);

        // The replay goes through a fresh harness, so what rejects it is the persisted UsedAt
        // rather than an entity the first call happens to have left tracked in memory.
        var second = await Build().Service.CompleteAsync("AMAZON_SP", state, "code-2", SellerA);

        first.Success.Should().BeTrue();
        second.Success.Should().BeFalse();
        second.Failure.Should().Be(IntegrationAuthorizationFailure.InvalidState);

        // The replay must not have touched the connection: still exactly one, still seller A.
        await using var db = _pg.CreateContext();
        var connections = await db.IntegrationConnections.ToListAsync();
        connections.Should().ContainSingle();
        connections[0].ExternalAccountId.Should().Be(SellerA);
    }

    // ---- start: connection + state issued together ----

    [Fact]
    public async Task Starting_authorization_creates_a_pending_connection_and_a_consent_url()
    {
        var h = Build();

        var start = await h.Service.StartAsync(_orgId, _tenantId, "AMAZON_SP", "Acme US", null);

        start.Success.Should().BeTrue();
        start.ConsentUrl.Should().Contain("state=");
        start.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(10), TimeSpan.FromSeconds(30));

        await using var db = _pg.CreateContext();
        var connection = await db.IntegrationConnections.SingleAsync(c => c.Id == start.ConnectionId);
        connection.Status.Should().Be(IntegrationConnectionStatus.Pending);
        connection.TenantId.Should().Be(_tenantId);
        connection.DisplayName.Should().Be("Acme US");

        // The state exists but only as a hash, and the raw token is nowhere in the row.
        var state = await db.IntegrationOAuthStates.SingleAsync();
        state.TokenHash.Should().HaveLength(64);
        state.TenantId.Should().Be(_tenantId);
        state.IntegrationConnectionId.Should().Be(start.ConnectionId!.Value);
    }

    [Fact]
    public async Task Starting_authorization_refuses_a_return_url_outside_the_allow_list()
    {
        var h = Build(allowedReturnUrls: new[] { "https://app.merchant360.com/integrations" });

        var start = await h.Service.StartAsync(
            _orgId, _tenantId, "AMAZON_SP", null, "https://evil.example.com/steal");

        start.Success.Should().BeFalse();

        // Nothing was created: a refused return URL must not leave a Pending connection behind.
        await using var db = _pg.CreateContext();
        (await db.IntegrationConnections.CountAsync()).Should().Be(0);
        (await db.IntegrationOAuthStates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_state_cannot_be_redeemed_through_another_providers_callback()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var state = await h.IssueStateAsync(_tenantId, connectionId);

        var result = await h.Service.CompleteAsync("AMAZON_BUSINESS", state, "code", SellerA);

        result.Success.Should().BeFalse();
        result.Failure.Should().Be(IntegrationAuthorizationFailure.InvalidState);
    }

    // ---- 2.2 expiry ----

    [Fact]
    public async Task An_expired_state_is_rejected()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var state = await h.IssueStateAsync(_tenantId, connectionId);

        // Eleven minutes old against a ten-minute lifetime.
        await using (var db = _pg.CreateContext())
        {
            await db.IntegrationOAuthStates.ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        // A second harness, because the callback is a different request with its own DbContext.
        // Reusing the issuing one would read the row it still has tracked in memory and never see
        // the expiry at all.
        var result = await Build().Service.CompleteAsync("AMAZON_SP", state, "code", SellerA);

        result.Success.Should().BeFalse();
        result.Failure.Should().Be(IntegrationAuthorizationFailure.InvalidState);
    }

    // ---- 2.3 tampering ----

    [Fact]
    public async Task A_tampered_state_token_attaches_nothing()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var state = await h.IssueStateAsync(_tenantId, connectionId);

        // One character changed. Only the hash is stored, so an attacker cannot work backwards
        // from the row to a token that hashes to it.
        var tampered = state[..^1] + (state[^1] == 'A' ? 'B' : 'A');

        var result = await h.Service.CompleteAsync("AMAZON_SP", tampered, "code", SellerA);

        result.Success.Should().BeFalse();
        await using var db = _pg.CreateContext();
        (await db.IntegrationConnections.SingleAsync()).Status
            .Should().Be(IntegrationConnectionStatus.Pending);
        (await db.IntegrationCredentials.CountAsync()).Should().Be(0);
    }

    // ---- 2.4 cross-tenant attach ----

    [Fact]
    public async Task A_states_tenant_decides_the_connection_not_the_callback()
    {
        var h = Build();
        var tenantAConnection = await h.BeginAsync(_tenantId);
        var tenantBConnection = await h.BeginAsync(_otherTenantId);

        // Tenant A's state, but the attacker wants tenant B's connection authorised. There is no
        // parameter through which to say so: the state carries both the tenant and the connection,
        // and they were issued together.
        var state = await h.IssueStateAsync(_tenantId, tenantAConnection);

        var result = await h.Service.CompleteAsync("AMAZON_SP", state, "code", SellerA);

        result.Success.Should().BeTrue();
        result.ConnectionId.Should().Be(tenantAConnection);

        await using var db = _pg.CreateContext();
        var b = await db.IntegrationConnections.SingleAsync(c => c.Id == tenantBConnection);
        b.Status.Should().Be(IntegrationConnectionStatus.Pending);
        b.ExternalAccountId.Should().BeNull();
    }

    [Fact]
    public async Task A_state_whose_connection_belongs_to_another_tenant_is_refused()
    {
        var h = Build();
        var tenantBConnection = await h.BeginAsync(_otherTenantId);

        // Forging the stored row directly — the strongest form of this attack, equivalent to an
        // attacker who can write state rows but not change the lookup. The tenant on the state and
        // the tenant owning the connection disagree, and the scoped read finds nothing.
        var state = await h.IssueStateAsync(_tenantId, tenantBConnection);

        var result = await h.Service.CompleteAsync("AMAZON_SP", state, "code", SellerA);

        result.Success.Should().BeFalse();
        result.Failure.Should().Be(IntegrationAuthorizationFailure.ConnectionUnavailable);

        await using var db = _pg.CreateContext();
        (await db.IntegrationConnections.SingleAsync(c => c.Id == tenantBConnection))
            .ExternalAccountId.Should().BeNull();
    }

    // ---- 2.5 / 2.6 reconnect reuse ----

    [Fact]
    public async Task Disconnecting_then_reconnecting_reuses_the_same_connection_and_its_watermark()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var first = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, connectionId), "code", SellerA);

        var watermark = DateTime.UtcNow.AddDays(-2);
        await using (var db = _pg.CreateContext())
        {
            await new IntegrationSyncStateRepository(db).AdvanceWatermarkAsync(
                first.ConnectionId!.Value, IntegrationCapabilities.SalesOrderIngest, "ATVPDKIKX0DER",
                watermark, cursor: null);
        }

        await h.Connections.DisconnectAsync(_tenantId, first.ConnectionId!.Value);

        // The user starts over, which creates a second Pending row — exactly what happens in the UI.
        var pendingAgain = await h.BeginAsync(_tenantId);
        var second = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, pendingAgain), "code-2", SellerA);

        second.Success.Should().BeTrue();
        second.ReusedExisting.Should().BeTrue();
        second.ConnectionId.Should().Be(first.ConnectionId);

        await using var verify = _pg.CreateContext();
        var reused = await verify.IntegrationConnections.SingleAsync(c => c.Id == first.ConnectionId);
        reused.Status.Should().Be(IntegrationConnectionStatus.Connected);

        var syncState = await verify.IntegrationSyncStates
            .SingleAsync(s => s.IntegrationConnectionId == first.ConnectionId);
        syncState.Watermark.Should().BeCloseTo(watermark, TimeSpan.FromSeconds(1));

        // The abandoned pending row is retired rather than left to clutter the list.
        var pendingRow = await verify.IntegrationConnections.SingleAsync(c => c.Id == pendingAgain);
        pendingRow.Status.Should().Be(IntegrationConnectionStatus.Revoked);
    }

    // ---- 2.7 one seller account, one tenant ----

    [Fact]
    public async Task A_second_tenant_cannot_claim_an_account_the_first_tenant_owns()
    {
        var h = Build();
        var a = await h.BeginAsync(_tenantId);
        var b = await h.BeginAsync(_otherTenantId);

        var first = await h.Service.CompleteAsync("AMAZON_SP", await h.IssueStateAsync(_tenantId, a), "c1", SellerA);
        first.Success.Should().BeTrue();

        var second = await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_otherTenantId, b), "c2", SellerA);

        // Vendorea is a software provider, not a merchant of record. A dealer's Amazon account —
        // its orders, its customers, its proceeds — belongs to one Merchant360 tenant, and a
        // second tenant authorising the same account must be refused rather than quietly handed
        // a copy of someone else's business.
        second.Success.Should().BeFalse();
        second.Failure.Should().Be(IntegrationAuthorizationFailure.ConnectionUnavailable);

        await using var db = _pg.CreateContext();
        var connected = await db.IntegrationConnections
            .Where(c => c.ExternalAccountId == SellerA
                        && c.Status == IntegrationConnectionStatus.Connected)
            .ToListAsync();

        connected.Should().ContainSingle();
        connected[0].TenantId.Should().Be(_tenantId);
        connected[0].AccountOwnership.Should().Be(IntegrationAccountOwnership.Primary);

        // The refused tenant is left with no credentials at all.
        (await db.IntegrationCredentials.CountAsync(
            c => c.IntegrationConnectionId == b)).Should().Be(0);
    }

    [Fact]
    public async Task A_tenant_may_hold_several_seller_accounts()
    {
        var h = Build();

        var first = await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_tenantId, await h.BeginAsync(_tenantId)), "c1", SellerA);
        var second = await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_tenantId, await h.BeginAsync(_tenantId)), "c2", SellerB);

        // One tenant, two Amazon accounts, is normal — a dealer may sell under more than one.
        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        first.ConnectionId.Should().NotBe(second.ConnectionId!.Value);
    }

    [Fact]
    public async Task A_delegated_tenant_may_connect_an_account_another_tenant_owns()
    {
        var h = Build();

        await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_tenantId, await h.BeginAsync(_tenantId)), "c1", SellerA);

        var grant = await h.Delegations.GrantAsync(
            "AMAZON_SP", SellerA, _otherTenantId, "ops@vendorea.test", "Reseller manages this account");

        grant.Success.Should().BeTrue();

        var delegated = await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_otherTenantId, await h.BeginAsync(_otherTenantId)),
            "c2", SellerA);

        delegated.Success.Should().BeTrue();

        await using var db = _pg.CreateContext();
        var rows = await db.IntegrationConnections
            .Where(c => c.ExternalAccountId == SellerA
                        && c.Status == IntegrationConnectionStatus.Connected)
            .OrderBy(c => c.TenantId)
            .ToListAsync();

        rows.Should().HaveCount(2);
        rows.Single(r => r.TenantId == _tenantId).AccountOwnership
            .Should().Be(IntegrationAccountOwnership.Primary);

        // The delegate holds the account on a different footing, recorded on the row. There is
        // still exactly one owner.
        rows.Single(r => r.TenantId == _otherTenantId).AccountOwnership
            .Should().Be(IntegrationAccountOwnership.Delegated);
    }

    [Fact]
    public async Task A_delegation_cannot_be_granted_for_an_account_nobody_owns()
    {
        var h = Build();

        var grant = await h.Delegations.GrantAsync(
            "AMAZON_SP", SellerA, _otherTenantId, "ops@vendorea.test", null);

        // Pre-authorising would create an orphan grant that silently became meaningful the moment
        // somebody else claimed the account.
        grant.Success.Should().BeFalse();
    }

    [Fact]
    public async Task A_delegation_must_name_who_granted_it()
    {
        var h = Build();
        await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_tenantId, await h.BeginAsync(_tenantId)), "c1", SellerA);

        var grant = await h.Delegations.GrantAsync("AMAZON_SP", SellerA, _otherTenantId, "  ", null);

        // A delegated-access decision nobody is named for is an unexplained exception sitting in
        // a table, not an authorization.
        grant.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Withdrawing_a_delegation_disconnects_the_delegate_and_destroys_its_credentials()
    {
        var h = Build();

        await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_tenantId, await h.BeginAsync(_tenantId)), "c1", SellerA);

        var grant = await h.Delegations.GrantAsync(
            "AMAZON_SP", SellerA, _otherTenantId, "ops@vendorea.test", null);

        var delegated = await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_otherTenantId, await h.BeginAsync(_otherTenantId)),
            "c2", SellerA);

        await h.Delegations.RevokeAsync(grant.Delegation!.Id, "ops@vendorea.test");

        await using var db = _pg.CreateContext();

        // Revocation has to take effect, not merely be recorded: a withdrawn grant whose
        // credentials still worked would be decoration.
        var delegateRow = await db.IntegrationConnections.SingleAsync(c => c.Id == delegated.ConnectionId);
        delegateRow.Status.Should().Be(IntegrationConnectionStatus.Revoked);
        (await db.IntegrationCredentials.CountAsync(
            c => c.IntegrationConnectionId == delegated.ConnectionId)).Should().Be(0);

        // The owner is untouched.
        var ownerRow = await db.IntegrationConnections
            .SingleAsync(c => c.TenantId == _tenantId && c.ExternalAccountId == SellerA);
        ownerRow.Status.Should().Be(IntegrationConnectionStatus.Connected);
        (await db.IntegrationCredentials.CountAsync(
            c => c.IntegrationConnectionId == ownerRow.Id)).Should().Be(1);
    }

    [Fact]
    public async Task A_delegate_cannot_promote_itself_to_owner_by_reconnecting()
    {
        var h = Build();

        await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_tenantId, await h.BeginAsync(_tenantId)), "c1", SellerA);
        var grant = await h.Delegations.GrantAsync(
            "AMAZON_SP", SellerA, _otherTenantId, "ops@vendorea.test", null);
        grant.Success.Should().BeTrue();

        await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_otherTenantId, await h.BeginAsync(_otherTenantId)),
            "c2", SellerA);

        // Re-consent from the delegate, which reuses its existing connection.
        var again = await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_otherTenantId, await h.BeginAsync(_otherTenantId)),
            "c3", SellerA);

        again.ReusedExisting.Should().BeTrue();

        await using var db = _pg.CreateContext();
        var delegateRow = await db.IntegrationConnections
            .SingleAsync(c => c.TenantId == _otherTenantId
                              && c.ExternalAccountId == SellerA
                              && c.Status == IntegrationConnectionStatus.Connected);

        // Reconnecting must not be a route to ownership.
        delegateRow.AccountOwnership.Should().Be(IntegrationAccountOwnership.Delegated);
    }

    [Fact]
    public async Task An_account_released_by_its_owner_may_be_claimed_by_another_tenant()
    {
        var h = Build();

        var first = await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_tenantId, await h.BeginAsync(_tenantId)), "c1", SellerA);
        await h.Connections.DisconnectAsync(_tenantId, first.ConnectionId!.Value);

        var second = await h.Service.CompleteAsync(
            "AMAZON_SP", await h.IssueStateAsync(_otherTenantId, await h.BeginAsync(_otherTenantId)),
            "c2", SellerA);

        // A tenant that disconnected has released the account. The later claim is a transfer, not
        // concurrent sharing — and the first tenant's history stays on its own revoked row.
        second.Success.Should().BeTrue();

        await using var db = _pg.CreateContext();
        var rows = await db.IntegrationConnections
            .Where(c => c.ExternalAccountId == SellerA).ToListAsync();

        rows.Should().HaveCount(2);
        rows.Single(r => r.TenantId == _tenantId).Status
            .Should().Be(IntegrationConnectionStatus.Revoked);
        rows.Single(r => r.TenantId == _otherTenantId).AccountOwnership
            .Should().Be(IntegrationAccountOwnership.Primary);
    }

    // ---- 2.8 account replacement ----

    [Fact]
    public async Task Authorizing_a_different_seller_account_creates_a_new_connection()
    {
        var h = Build();
        var firstConnection = await h.BeginAsync(_tenantId);
        var first = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, firstConnection), "c1", SellerA);

        var secondConnection = await h.BeginAsync(_tenantId);
        var second = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, secondConnection), "c2", SellerB);

        second.ConnectionId.Should().NotBe(first.ConnectionId!.Value);
        second.ReusedExisting.Should().BeFalse();

        // The old connection keeps its account id and its history. Orders already imported under
        // seller A stay attributable; nothing is rewritten to look as though they came from B.
        await using var db = _pg.CreateContext();
        var old = await db.IntegrationConnections.SingleAsync(c => c.Id == first.ConnectionId);
        old.ExternalAccountId.Should().Be(SellerA);
    }

    // ---- re-consent without disconnecting ----

    [Fact]
    public async Task Re_consenting_replaces_the_token_without_creating_a_connection()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        await h.Service.CompleteAsync("AMAZON_SP", await h.IssueStateAsync(_tenantId, connectionId), "c1", SellerA);

        var again = await h.BeginAsync(_tenantId);
        var result = await h.Service.CompleteAsync("AMAZON_SP", await h.IssueStateAsync(_tenantId, again), "c2", SellerA);

        result.ReusedExisting.Should().BeTrue();

        await using var db = _pg.CreateContext();
        var credential = await db.IntegrationCredentials
            .SingleAsync(c => c.IntegrationConnectionId == result.ConnectionId);
        credential.Version.Should().Be(2);
        credential.ProtectedValue.Should().StartWith("enc:v1:");
    }

    // ---- 2.9 revocation ----

    [Fact]
    public async Task Disconnecting_destroys_the_token_and_marks_the_connection_revoked()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var connected = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, connectionId), "c1", SellerA);

        await h.Connections.DisconnectAsync(_tenantId, connected.ConnectionId!.Value);

        await using var db = _pg.CreateContext();
        (await db.IntegrationConnections.SingleAsync(c => c.Id == connected.ConnectionId)).Status
            .Should().Be(IntegrationConnectionStatus.Revoked);
        (await db.IntegrationCredentials
            .CountAsync(c => c.IntegrationConnectionId == connected.ConnectionId))
            .Should().Be(0);
    }

    // ---- 2.10 no token material in logs ----

    [Fact]
    public async Task No_log_written_during_a_full_flow_contains_token_material()
    {
        var recorder = new RecordingLoggerProvider();
        var h = Build(recorder);

        var connectionId = await h.BeginAsync(_tenantId);
        var state = await h.IssueStateAsync(_tenantId, connectionId);
        await h.Service.CompleteAsync("AMAZON_SP", state, "an-authorization-code", SellerA);
        await h.Service.CompleteAsync("AMAZON_SP", state, "an-authorization-code", SellerA); // the rejected replay too

        var written = string.Join("\n", recorder.Messages);
        written.Should().NotContain(RefreshToken);
        written.Should().NotContain("Atzr|");
        written.Should().NotContain("an-authorization-code");
        written.Should().NotContain(state);

        // The point is not that nothing was logged — operators need this trail.
        recorder.Messages.Should().NotBeEmpty();
    }

    // ---- 2.9 revocation stops work / connection health ----

    [Fact]
    public async Task A_connected_connection_yields_an_access_token()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var connected = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, connectionId), "c1", SellerA);

        var token = await h.Tokens.GetAccessTokenAsync(_tenantId, connected.ConnectionId!.Value);

        token.Should().Be("Atza|ACCESS");
    }

    [Fact]
    public async Task A_revoked_connection_yields_no_access_token()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var connected = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, connectionId), "c1", SellerA);

        await h.Connections.DisconnectAsync(_tenantId, connected.ConnectionId!.Value);

        // This is what makes revocation stop work rather than merely look like it has: every
        // adapter asks for a token first, and there is no path around this check.
        (await h.Tokens.GetAccessTokenAsync(_tenantId, connected.ConnectionId!.Value))
            .Should().BeNull();
    }

    [Fact]
    public async Task Another_tenant_cannot_obtain_a_token_for_this_connection()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var connected = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, connectionId), "c1", SellerA);

        (await h.Tokens.GetAccessTokenAsync(_otherTenantId, connected.ConnectionId!.Value))
            .Should().BeNull();
    }

    [Fact]
    public async Task A_rejected_refresh_token_moves_the_connection_to_Expired()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var connected = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, connectionId), "c1", SellerA);

        h.Lwa.RefreshError = "invalid_grant: The request has an invalid grant parameter.";

        var token = await h.Tokens.GetAccessTokenAsync(_tenantId, connected.ConnectionId!.Value);

        token.Should().BeNull();
        await using var db = _pg.CreateContext();
        var row = await db.IntegrationConnections.SingleAsync(c => c.Id == connected.ConnectionId);
        row.Status.Should().Be(IntegrationConnectionStatus.Expired);
        row.LastError.Should().Contain("invalid_grant");
    }

    [Fact]
    public async Task A_transient_refresh_failure_leaves_the_connection_connected()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var connected = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, connectionId), "c1", SellerA);

        h.Lwa.RefreshError = "Amazon could not be reached.";

        (await h.Tokens.GetAccessTokenAsync(_tenantId, connected.ConnectionId!.Value))
            .Should().BeNull();

        // An Amazon outage must not disconnect every healthy seller.
        await using var db = _pg.CreateContext();
        var row = await db.IntegrationConnections.SingleAsync(c => c.Id == connected.ConnectionId);
        row.Status.Should().Be(IntegrationConnectionStatus.Connected);
        row.LastErrorAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_denied_capability_is_cleared_from_the_connection()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var connected = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, connectionId), "c1", SellerA);

        await h.Connections.RecordCapabilityDeniedAsync(
            _tenantId, connected.ConnectionId!.Value,
            IntegrationCapabilities.FulfillmentPush, "403 Unauthorized role");

        await using var db = _pg.CreateContext();
        var row = await db.IntegrationConnections.SingleAsync(c => c.Id == connected.ConnectionId);
        row.GrantedCapabilities.HasFlag(IntegrationCapabilities.FulfillmentPush).Should().BeFalse();
        row.GrantedCapabilities.HasFlag(IntegrationCapabilities.SalesOrderIngest).Should().BeTrue();
    }

    [Fact]
    public async Task A_rejected_credential_on_a_revoked_connection_does_not_resurrect_it()
    {
        var h = Build();
        var connectionId = await h.BeginAsync(_tenantId);
        var connected = await h.Service.CompleteAsync("AMAZON_SP",
            await h.IssueStateAsync(_tenantId, connectionId), "c1", SellerA);

        await h.Connections.DisconnectAsync(_tenantId, connected.ConnectionId!.Value);
        await h.Connections.RecordCredentialRejectedAsync(
            _tenantId, connected.ConnectionId!.Value, "invalid_grant");

        // Expired reads as "reconnect me"; Revoked reads as "this is off". A late in-flight call
        // must not change the second into the first.
        await using var db = _pg.CreateContext();
        (await db.IntegrationConnections.SingleAsync(c => c.Id == connected.ConnectionId)).Status
            .Should().Be(IntegrationConnectionStatus.Revoked);
    }

    // ---- harness ----

    private Harness Build(
        RecordingLoggerProvider? recorder = null, IEnumerable<string>? allowedReturnUrls = null)
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

        var connections = new IntegrationConnectionService(
            new IntegrationConnectionRepository(db),
            new IntegrationProviderRepository(db),
            credentialStore,
            new IntegrationAccountDelegationRepository(db),
            new AuditService(new AuditLogRepository(db), null!, NullLogger<AuditService>.Instance),
            Logger<IntegrationConnectionService>(recorder));

        var states = new IntegrationOAuthStateService(
            new IntegrationOAuthStateRepository(db), Logger<IntegrationOAuthStateService>(recorder));

        var authLwa = new StubLwaClient();
        var service = new IntegrationAuthorizationService(
            states,
            connections,
            new IntegrationProviderRepository(db),
            authLwa,
            new AmazonAccessTokenCache(),
            Options.Create(new IntegrationAuthorizationOptions
            {
                AllowedReturnUrls = allowedReturnUrls?.ToList() ?? new List<string>()
            }),
            Logger<IntegrationAuthorizationService>(recorder));

        var tokenCache = new AmazonAccessTokenCache();
        var lwa = new StubLwaClient();
        var tokens = new AmazonAccessTokenProvider(
            connections, credentialStore, lwa, tokenCache,
            Logger<AmazonAccessTokenProvider>(recorder));

        var delegations = new IntegrationAccountDelegationService(
            new IntegrationAccountDelegationRepository(db),
            new IntegrationConnectionRepository(db),
            connections,
            new IntegrationProviderRepository(db),
            new AuditService(new AuditLogRepository(db), null!, NullLogger<AuditService>.Instance),
            Logger<IntegrationAccountDelegationService>(recorder));

        return new Harness(service, connections, states, tokens, lwa, delegations, db);
    }

    private static ILogger<T> Logger<T>(RecordingLoggerProvider? recorder)
        => recorder is null ? NullLogger<T>.Instance : recorder.CreateLogger<T>();

    private sealed record Harness(
        IIntegrationAuthorizationService Service,
        IIntegrationConnectionService Connections,
        IIntegrationOAuthStateService States,
        IAmazonAccessTokenProvider Tokens,
        StubLwaClient Lwa,
        IIntegrationAccountDelegationService Delegations,
        PartnerConnectDbContext Db)
    {
        public async Task<Guid> BeginAsync(int tenantId)
        {
            // OrganizationId is the same for both test tenants, so it is not what isolates them.
            var result = await Connections.BeginAsync(
                OrgIdOf(Db, tenantId), tenantId, "AMAZON_SP", null);
            return result.Connection!.Id;
        }

        public Task<string> IssueStateAsync(int tenantId, Guid connectionId)
            => States.IssueAsync(OrgIdOf(Db, tenantId), tenantId, connectionId, null);

        private static int OrgIdOf(PartnerConnectDbContext db, int tenantId)
            => db.Tenants.AsNoTracking().Single(t => t.Id == tenantId).OrganizationId;
    }

    /// <summary>Returns a fixed refresh token so the log assertion has something to look for.</summary>
    private sealed class StubLwaClient : IAmazonLwaClient
    {
        /// <summary>Set to make the next refresh fail with a given LWA error code.</summary>
        public string? RefreshError { get; set; }

        public bool IsConfigured => true;

        public Task<LwaTokenResult> ExchangeAuthorizationCodeAsync(
            string authorizationCode, CancellationToken cancellationToken = default)
            => Task.FromResult(LwaTokenResult.Ok(
                "Atza|ACCESS", RefreshToken, DateTime.UtcNow.AddHours(1)));

        public Task<LwaTokenResult> RefreshAccessTokenAsync(
            string refreshToken, CancellationToken cancellationToken = default)
            => Task.FromResult(RefreshError is null
                ? LwaTokenResult.Ok("Atza|ACCESS", refreshToken, DateTime.UtcNow.AddHours(1))
                : LwaTokenResult.Failed(RefreshError));

        public string BuildConsentUrl(string state) =>
            $"https://sellercentral.amazon.com/apps/authorize/consent?application_id=app&state={state}";
    }

    private sealed class RecordingLoggerProvider
    {
        public List<string> Messages { get; } = new();

        public ILogger<T> CreateLogger<T>() => new Recorder<T>(Messages);

        private sealed class Recorder<T> : ILogger<T>
        {
            private readonly List<string> _messages;
            public Recorder(List<string> messages) => _messages = messages;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (_messages)
                {
                    // The formatted message and the exception, which is where a careless
                    // "log the whole response" would surface.
                    _messages.Add(formatter(state, exception) + " " + exception);
                }
            }
        }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
