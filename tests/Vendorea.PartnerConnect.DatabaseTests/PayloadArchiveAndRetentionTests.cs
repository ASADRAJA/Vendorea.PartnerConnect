using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// The payload archive and the buyer-data retention sweep — test matrix 3.15 (finding A2).
/// </summary>
[Collection(PostgresCollection.Name)]
public class PayloadArchiveAndRetentionTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private IntegrationConnection _connection = null!;
    private int _tenantId;

    public PayloadArchiveAndRetentionTests(PostgresFixture pg) => _pg = pg;

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

    private IntegrationPayloadArchiveService Archive(PayloadArchiveOptions? options = null)
        => new(new IntegrationPayloadArchiveRepository(_pg.CreateContext()),
            Options.Create(options ?? new PayloadArchiveOptions()),
            NullLogger<IntegrationPayloadArchiveService>.Instance);

    private const string OrderJson = """
    {
      "orderId": "111-2223334-5556667",
      "fulfillment": { "fulfillmentStatus": "UNSHIPPED" },
      "buyer": { "buyerName": "J. Carter", "buyerEmail": "x1y2z3@marketplace.amazon.com" },
      "recipient": { "deliveryAddress": { "addressLine1": "414 W Main St", "city": "Richmond", "postalCode": "23220" } }
    }
    """;

    // ---- the archive never holds PII ----

    [Fact]
    public async Task What_lands_in_the_table_contains_no_personal_data()
    {
        await Archive().CaptureAsync(
            _connection, IntegrationPayloadKind.OrderSnapshot, "111-2223334-5556667", OrderJson);

        await using var db = _pg.CreateContext();
        var row = await db.IntegrationPayloadArchives.SingleAsync();

        // Redaction happens before the write, so there is no window in which the table holds an
        // unredacted copy — not even briefly.
        row.Content.Should().NotContain("J. Carter");
        row.Content.Should().NotContain("x1y2z3@marketplace.amazon.com");
        row.Content.Should().NotContain("414 W Main St");
        row.Content.Should().NotContain("23220");

        // Diagnosable, still.
        row.Content.Should().Contain("UNSHIPPED");
        row.Content.Should().Contain("Richmond");
        row.ExternalReference.Should().Be("111-2223334-5556667");
        row.RedactedPaths.Should().Contain("buyer.buyerEmail");
    }

    [Fact]
    public async Task Every_row_is_written_with_an_expiry()
    {
        await Archive(new PayloadArchiveOptions { Retention = TimeSpan.FromDays(30) })
            .CaptureAsync(_connection, IntegrationPayloadKind.OrderSnapshot, "A", OrderJson);

        await using var db = _pg.CreateContext();
        var row = await db.IntegrationPayloadArchives.SingleAsync();

        // Non-nullable on purpose: a nullable expiry is how an archive becomes permanent.
        row.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    // ---- dedupe ----

    [Fact]
    public async Task An_unchanged_payload_is_not_captured_twice()
    {
        var archive = Archive();

        await archive.CaptureAsync(_connection, IntegrationPayloadKind.OrderSnapshot, "A", OrderJson);
        await Archive().CaptureAsync(_connection, IntegrationPayloadKind.OrderSnapshot, "A", OrderJson);
        await Archive().CaptureAsync(_connection, IntegrationPayloadKind.OrderSnapshot, "A", OrderJson);

        await using var db = _pg.CreateContext();

        // Polling re-reads an overlap window every cycle on purpose. Without dedupe the archive
        // would fill with identical copies of unchanged orders faster than with anything useful.
        (await db.IntegrationPayloadArchives.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_changed_payload_is_captured_again()
    {
        await Archive().CaptureAsync(_connection, IntegrationPayloadKind.OrderSnapshot, "A", OrderJson);
        await Archive().CaptureAsync(
            _connection, IntegrationPayloadKind.OrderSnapshot, "A",
            OrderJson.Replace("UNSHIPPED", "SHIPPED"));

        await using var db = _pg.CreateContext();
        var rows = await db.IntegrationPayloadArchives.OrderBy(a => a.CapturedAt).ToListAsync();

        rows.Should().HaveCount(2);
        rows[1].Content.Should().Contain("SHIPPED");
    }

    [Fact]
    public async Task Dedupe_is_per_payload_kind()
    {
        await Archive().CaptureAsync(_connection, IntegrationPayloadKind.OrderSnapshot, "A", OrderJson);
        await Archive().CaptureAsync(_connection, IntegrationPayloadKind.OrderNotification, "A", OrderJson);

        await using var db = _pg.CreateContext();

        // The same order arriving by poll and by notification is two different observations.
        (await db.IntegrationPayloadArchives.CountAsync()).Should().Be(2);
    }

    // ---- guards ----

    [Fact]
    public async Task An_oversized_payload_is_skipped_rather_than_stored()
    {
        var huge = "{\"orderId\":\"A\",\"filler\":\"" + new string('x', 5_000) + "\"}";

        await Archive(new PayloadArchiveOptions { MaxPayloadBytes = 1_000 })
            .CaptureAsync(_connection, IntegrationPayloadKind.OrderSnapshot, "A", huge);

        await using var db = _pg.CreateContext();
        (await db.IntegrationPayloadArchives.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Capturing_is_a_no_op_when_disabled()
    {
        await Archive(new PayloadArchiveOptions { Enabled = false })
            .CaptureAsync(_connection, IntegrationPayloadKind.OrderSnapshot, "A", OrderJson);

        await using var db = _pg.CreateContext();
        (await db.IntegrationPayloadArchives.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_failure_to_archive_never_surfaces_to_the_caller()
    {
        // A repository whose write always fails, standing in for a full disk or a lock timeout.
        var service = new IntegrationPayloadArchiveService(
            new ThrowingArchiveRepository(),
            Options.Create(new PayloadArchiveOptions()),
            NullLogger<IntegrationPayloadArchiveService>.Instance);

        var act = async () => await service.CaptureAsync(
            _connection, IntegrationPayloadKind.OrderSnapshot, "A", OrderJson);

        // The archive observes an ingestion. Failing the ingestion because the observation failed
        // would be the wrong trade every time.
        await act.Should().NotThrowAsync();
    }

    // ---- purge ----

    [Fact]
    public async Task Expired_captures_are_purged_and_live_ones_are_not()
    {
        await using (var db = _pg.CreateContext())
        {
            for (var i = 0; i < 5; i++)
            {
                db.IntegrationPayloadArchives.Add(Capture($"old-{i}", DateTime.UtcNow.AddDays(-1)));
            }
            db.IntegrationPayloadArchives.Add(Capture("fresh", DateTime.UtcNow.AddDays(10)));
            await db.SaveChangesAsync();
        }

        await using var purgeContext = _pg.CreateContext();
        var handler = new IntegrationPayloadArchivePurgeJobHandler(
            new IntegrationPayloadArchiveRepository(purgeContext),
            Options.Create(new PayloadArchiveOptions { PurgeBatchSize = 2 }),
            NullLogger<IntegrationPayloadArchivePurgeJobHandler>.Instance);

        var result = await handler.ExecuteAsync(new ScheduledJob());

        result.Success.Should().BeTrue();

        await using var verify = _pg.CreateContext();
        var remaining = await verify.IntegrationPayloadArchives.ToListAsync();

        // Batched deletion still drains the backlog; a batch size of 2 against 5 expired rows
        // proves the loop continues rather than stopping after one pass.
        remaining.Should().ContainSingle();
        remaining[0].ExternalReference.Should().Be("fresh");
    }

    private IntegrationPayloadArchive Capture(string reference, DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        IntegrationConnectionId = _connection.Id,
        TenantId = _tenantId,
        PayloadKind = IntegrationPayloadKind.OrderSnapshot,
        ExternalReference = reference,
        ContentHash = Guid.NewGuid().ToString("n"),
        Content = "{}",
        SizeBytes = 2,
        CapturedAt = DateTime.UtcNow.AddDays(-40),
        ExpiresAt = expiresAt
    };

    // ---- buyer-data retention on the order itself ----

    [Fact]
    public async Task A_settled_order_past_the_window_is_redacted()
    {
        await SeedOrderAsync("A", SalesOrderStatus.Fulfilled, DateTime.UtcNow.AddDays(-120));

        var result = await RunRetentionAsync();

        result.Success.Should().BeTrue();

        await using var db = _pg.CreateContext();
        var order = await db.SalesOrders.SingleAsync();
        order.PiiRedactedAt.Should().NotBeNull();
        order.BuyerName.Should().BeNull();
        order.ShipToLine1.Should().BeNull();

        // Coarse geography survives: not identifying, and tax and reporting need it.
        order.ShipToCountryCode.Should().Be("US");
    }

    [Fact]
    public async Task A_settled_order_inside_the_window_is_left_alone()
    {
        await SeedOrderAsync("A", SalesOrderStatus.Fulfilled, DateTime.UtcNow.AddDays(-10));

        await RunRetentionAsync();

        await using var db = _pg.CreateContext();
        (await db.SalesOrders.SingleAsync()).PiiRedactedAt.Should().BeNull();
    }

    [Fact]
    public async Task An_unshipped_order_past_the_ordinary_window_keeps_its_address()
    {
        await SeedOrderAsync("A", SalesOrderStatus.ReadyToFulfill, DateTime.UtcNow.AddDays(-120));

        await RunRetentionAsync();

        await using var db = _pg.CreateContext();
        var order = await db.SalesOrders.SingleAsync();

        // Redacting an order still awaiting shipment destroys the address it needs — correct
        // privacy behaviour producing an undeliverable order.
        order.PiiRedactedAt.Should().BeNull();
        order.ShipToLine1.Should().Be("414 W Main St");
    }

    [Fact]
    public async Task An_unshipped_order_past_the_hard_limit_is_redacted_anyway()
    {
        await SeedOrderAsync("A", SalesOrderStatus.ReadyToFulfill, DateTime.UtcNow.AddDays(-200));

        await RunRetentionAsync();

        await using var db = _pg.CreateContext();

        // Without the outer limit an order stuck outside a terminal state would hold personal
        // data for ever, and the policy would silently depend on a state machine completing.
        (await db.SalesOrders.SingleAsync()).PiiRedactedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task An_already_redacted_order_is_not_revisited()
    {
        await SeedOrderAsync("A", SalesOrderStatus.Fulfilled, DateTime.UtcNow.AddDays(-120));
        await RunRetentionAsync();

        DateTime first;
        await using (var db = _pg.CreateContext())
        {
            first = (await db.SalesOrders.SingleAsync()).PiiRedactedAt!.Value;
        }

        await Task.Delay(50);
        await RunRetentionAsync();

        await using var verify = _pg.CreateContext();

        // The timestamp records when data was erased. A sweep that rewrote it on every pass would
        // destroy the only evidence of when that happened.
        (await verify.SalesOrders.SingleAsync()).PiiRedactedAt.Should().Be(first);
    }

    [Fact]
    public async Task Retention_runs_across_tenants()
    {
        await SeedOrderAsync("A", SalesOrderStatus.Fulfilled, DateTime.UtcNow.AddDays(-120));
        await SeedOrderAsync("B", SalesOrderStatus.Cancelled, DateTime.UtcNow.AddDays(-150));

        await RunRetentionAsync();

        await using var db = _pg.CreateContext();

        // Retention is a platform obligation. Scoping it per tenant would make it an obligation
        // nobody was responsible for discharging.
        (await db.SalesOrders.CountAsync(o => o.PiiRedactedAt != null)).Should().Be(2);
    }

    [Fact]
    public async Task Retention_is_a_no_op_when_disabled()
    {
        await SeedOrderAsync("A", SalesOrderStatus.Fulfilled, DateTime.UtcNow.AddDays(-120));

        await RunRetentionAsync(new BuyerDataRetentionOptions { Enabled = false });

        await using var db = _pg.CreateContext();
        (await db.SalesOrders.SingleAsync()).PiiRedactedAt.Should().BeNull();
    }

    private async Task<JobExecutionResult> RunRetentionAsync(BuyerDataRetentionOptions? options = null)
    {
        await using var db = _pg.CreateContext();
        var handler = new BuyerDataRetentionJobHandler(
            new SalesOrderRetentionRepository(db),
            Options.Create(options ?? new BuyerDataRetentionOptions()),
            NullLogger<BuyerDataRetentionJobHandler>.Instance);

        return await handler.ExecuteAsync(new ScheduledJob());
    }

    private async Task SeedOrderAsync(string reference, SalesOrderStatus status, DateTime purchaseDate)
    {
        await using var db = _pg.CreateContext();
        db.SalesOrders.Add(new SalesOrder
        {
            IntegrationConnectionId = _connection.Id,
            TenantId = _tenantId,
            ExternalOrderId = reference,
            Status = status,
            FulfillmentChannel = FulfillmentChannel.MerchantFulfilled,
            MappingStatus = MappingStatus.Mapped,
            PurchaseDate = purchaseDate,
            SourceLastUpdatedAt = purchaseDate,
            BuyerDataAvailable = true,
            BuyerName = "J. Carter",
            BuyerEmailRef = "x1y2z3@marketplace.amazon.com",
            ShipToName = "J. Carter",
            ShipToLine1 = "414 W Main St",
            ShipToCity = "Richmond",
            ShipToPostalCode = "23220",
            ShipToCountryCode = "US"
        });
        await db.SaveChangesAsync();
    }

    private sealed class ThrowingArchiveRepository : Vendorea.PartnerConnect.Application.Interfaces.IIntegrationPayloadArchiveRepository
    {
        public Task<string?> GetLatestHashAsync(
            Guid connectionId, IntegrationPayloadKind kind, string externalReference,
            CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task AddAsync(IntegrationPayloadArchive archive, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("disk full");

        public Task<IReadOnlyList<IntegrationPayloadArchive>> GetForReferenceAsync(
            int tenantId, string externalReference, int limit, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> PurgeExpiredAsync(DateTime asOfUtc, int batchSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
