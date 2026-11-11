using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Vendorea.PartnerConnect.Persistence;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// Transactional event publication — test matrix 4.1.
/// </summary>
/// <remarks>
/// The guarantee under test is that an order, its event row and its outbox message commit
/// together or not at all. An order that committed without its event would never reach
/// Merchant360 and nothing would record that it had not — which is the failure this phase exists
/// to make impossible rather than unlikely.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class IntegrationEventPublishingTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private IntegrationConnection _connection = null!;
    private int _tenantId;
    private int _orgId;

    public IntegrationEventPublishingTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync(
            "IntegrationEvents", "OutboxMessages", "IntegrationPayloadArchives",
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
            GrantedCapabilities = IntegrationCapabilities.SalesOrderIngest,
            Provider = provider
        };
        db.IntegrationConnections.Add(connection);
        await db.SaveChangesAsync();
        _connection = connection;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>One context shared by the repositories, as a request scope would be.</summary>
    private (SalesOrderIngestionService Ingestion, PartnerConnectDbContext Db) Build(
        bool includeBuyerData = false)
    {
        var db = _pg.CreateContext();

        var outbox = new OutboxService(
            new OutboxRepository(db),
            new NoOpProcessor(),
            NullLogger<OutboxService>.Instance);

        var publisher = new SalesOrderEventPublisher(
            new IntegrationEventRepository(db),
            outbox,
            Options.Create(new IntegrationDeliveryOptions
            {
                EndpointUrl = "https://m360.test/api/v1/partner-connect/integration-events",
                IncludeBuyerData = includeBuyerData
            }),
            NullLogger<SalesOrderEventPublisher>.Instance);

        var ingestion = new SalesOrderIngestionService(
            new SalesOrderRepository(db),
            NullLogger<SalesOrderIngestionService>.Instance,
            publisher);

        return (ingestion, db);
    }

    private static MarketplaceOrderSnapshot Snapshot(
        DateTime lastUpdated,
        SalesOrderStatus status = SalesOrderStatus.ReadyToFulfill,
        string statusRaw = "UNSHIPPED",
        string orderId = "111-2223334-5556667")
        => new()
        {
            ExternalOrderId = orderId,
            MarketplaceId = "ATVPDKIKX0DER",
            Status = status,
            ExternalStatusRaw = statusRaw,
            FulfillmentChannel = FulfillmentChannel.MerchantFulfilled,
            Programs = new[] { "PRIME" },
            PurchaseDate = DateTime.UtcNow.AddHours(-3),
            SourceLastUpdatedAt = lastUpdated,
            OrderTotalAmount = 48.97m,
            CurrencyCode = "USD",
            BuyerDataAvailable = true,
            BuyerEmailRef = "x1y2z3@marketplace.amazon.com",
            BuyerName = "J. Carter",
            ShipTo = new MarketplaceAddress("J. Carter", "414 W Main St", null, "Richmond", "VA", "23220", "US"),
            Lines = new[]
            {
                new MarketplaceOrderLineSnapshot
                {
                    ExternalLineId = "L1", SellerSku = "VND-COPY-8511", Asin = "B07XYZ1234",
                    QuantityOrdered = 2, ItemPrice = 39.98m, ItemTax = 2.4m, CurrencyCode = "USD"
                }
            }
        };

    // ---- 4.1 the transactional guarantee ----

    [Fact]
    public async Task Ingesting_an_order_commits_the_order_its_event_and_its_outbox_message_together()
    {
        var (ingestion, db) = Build();

        var result = await ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");
        result.Outcome.Should().Be(SalesOrderIngestOutcome.Created);

        await using var verify = _pg.CreateContext();

        var order = await verify.SalesOrders.SingleAsync();
        var published = await verify.IntegrationEvents.SingleAsync();
        var message = await verify.OutboxMessages.SingleAsync();

        published.SalesOrderId.Should().Be(order.Id);
        published.EventType.Should().Be(IntegrationEventType.SalesOrderCreated);
        published.OrganizationId.Should().Be(_orgId);
        published.TenantId.Should().Be(_tenantId);
        published.OutboxMessageId.Should().Be(message.Id);
        message.MessageType.Should().Be(SalesOrderEventPublisher.OutboxMessageType);

        // Nothing has been delivered yet — that happens later, from the outbox.
        published.DeliveryAcknowledgedAt.Should().BeNull();
        published.ProcessingResult.Should().BeNull();
        message.Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task Losing_the_race_to_create_leaves_no_orphan_event_or_message()
    {
        var snapshot = Snapshot(DateTime.UtcNow.AddMinutes(-5));

        // Eight concurrent ingests of the same new order — the poll-and-notification collision.
        var services = Enumerable.Range(0, 8).Select(_ => Build().Ingestion).ToList();
        var results = await Task.WhenAll(
            services.Select(s => s.IngestAsync(_connection, snapshot, "poll")));

        results.Should().OnlyContain(r => r.Success);
        results.Count(r => r.Outcome == SalesOrderIngestOutcome.Created).Should().Be(1);

        await using var verify = _pg.CreateContext();

        (await verify.SalesOrders.CountAsync()).Should().Be(1);

        // The seven that lost discarded everything they staged. An event for an order that was
        // never created would describe something that does not exist, and its outbox message
        // would be retried for ever against it.
        var events = await verify.IntegrationEvents.ToListAsync();
        events.Should().OnlyContain(e => e.SalesOrderId != null);
        (await verify.OutboxMessages.CountAsync()).Should().Be(events.Count);
    }

    [Fact]
    public async Task A_stale_snapshot_publishes_nothing()
    {
        var newer = DateTime.UtcNow.AddMinutes(-5);
        var (first, _) = Build();
        await first.IngestAsync(_connection, Snapshot(newer, SalesOrderStatus.Fulfilled, "SHIPPED"), "notification");

        var (second, _) = Build();
        var stale = await second.IngestAsync(
            _connection, Snapshot(DateTime.UtcNow.AddHours(-2)), "poll");

        stale.Outcome.Should().Be(SalesOrderIngestOutcome.SkippedStale);

        await using var verify = _pg.CreateContext();

        // One event, from the first ingest. An update that changed nothing is not a transition,
        // and republishing it would make the consumer dedupe work it should never have received.
        (await verify.IntegrationEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_status_change_publishes_a_fulfillment_status_changed_event()
    {
        var t0 = DateTime.UtcNow.AddHours(-2);
        var (first, _) = Build();
        await first.IngestAsync(_connection, Snapshot(t0), "poll");

        var (second, _) = Build();
        await second.IngestAsync(
            _connection, Snapshot(t0.AddMinutes(30), SalesOrderStatus.Fulfilled, "SHIPPED"), "poll");

        await using var verify = _pg.CreateContext();
        var events = await verify.IntegrationEvents.OrderBy(e => e.PublishedAt).ToListAsync();

        events.Should().HaveCount(2);
        events[0].EventType.Should().Be(IntegrationEventType.SalesOrderCreated);
        events[1].EventType.Should().Be(IntegrationEventType.SalesOrderFulfillmentStatusChanged);
    }

    // ---- the deduplication key ----

    [Fact]
    public async Task Different_transitions_get_different_deduplication_keys()
    {
        var t0 = DateTime.UtcNow.AddHours(-2);
        var (first, _) = Build();
        await first.IngestAsync(_connection, Snapshot(t0), "poll");

        var (second, _) = Build();
        await second.IngestAsync(
            _connection, Snapshot(t0.AddMinutes(30), SalesOrderStatus.Fulfilled, "SHIPPED"), "poll");

        await using var verify = _pg.CreateContext();
        var keys = await verify.IntegrationEvents.Select(e => e.DeduplicationKey).ToListAsync();

        // A consumer has to tell "I have seen this change" from "this is a new change". One key
        // for both would make the second indistinguishable from a retry of the first.
        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task A_redelivery_keeps_the_order_identity_but_takes_a_new_event_id()
    {
        var (ingestion, db) = Build();
        await ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");

        Guid originalEventId;
        SalesOrder order;
        await using (var read = _pg.CreateContext())
        {
            originalEventId = (await read.IntegrationEvents.SingleAsync()).EventId;
            order = await read.SalesOrders.Include(o => o.Lines).SingleAsync();
        }

        await using var publishContext = _pg.CreateContext();
        var publisher = new SalesOrderEventPublisher(
            new IntegrationEventRepository(publishContext),
            new OutboxService(new OutboxRepository(publishContext), new NoOpProcessor(),
                NullLogger<OutboxService>.Instance),
            Options.Create(new IntegrationDeliveryOptions { EndpointUrl = "https://m360.test/events" }),
            NullLogger<SalesOrderEventPublisher>.Instance);

        var newEventId = await publisher.RepublishAsync(
            order, _connection, "AMAZON_SP", EventPublicationReason.ManualRedelivery, "corr-1");

        newEventId.Should().NotBe(originalEventId);

        await using var verify = _pg.CreateContext();
        var republished = await verify.IntegrationEvents.SingleAsync(e => e.EventId == newEventId);

        republished.PublicationReason.Should().Be(EventPublicationReason.ManualRedelivery);

        // Always an update, never a second creation: the consumer keys on order identity, so a
        // second "created" for an order it already holds would be a contradiction.
        republished.EventType.Should().Be(IntegrationEventType.SalesOrderUpdated);
        republished.SalesOrderId.Should().Be(order.Id);
        republished.EnvelopeJson.Should().Contain(order.ExternalOrderId);
    }

    // ---- the envelope ----

    [Fact]
    public async Task The_envelope_matches_the_contract_schema_shape()
    {
        var (ingestion, _) = Build();
        await ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");

        await using var verify = _pg.CreateContext();
        var json = (await verify.IntegrationEvents.SingleAsync()).EnvelopeJson;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        foreach (var required in new[]
                 {
                     "eventId", "eventType", "eventVersion", "occurredAt", "publishedAt",
                     "organizationId", "tenantId", "integrationConnectionId", "provider",
                     "deduplicationKey", "correlationId", "payload"
                 })
        {
            root.TryGetProperty(required, out _).Should().BeTrue($"the schema requires '{required}'");
        }

        root.GetProperty("eventType").GetString().Should().Be("sales_order.created.v1");
        root.GetProperty("payloadKind").GetString().Should().Be("snapshot");

        var payload = root.GetProperty("payload");
        payload.GetProperty("identity").GetProperty("externalOrderId").GetString()
            .Should().Be("111-2223334-5556667");

        // Money as a decimal string with at least two places, as the schema's pattern requires.
        payload.GetProperty("orderTotal").GetProperty("amount").GetString().Should().Be("48.97");
        payload.GetProperty("orderTotal").GetProperty("currency").GetString().Should().Be("USD");
    }

    [Fact]
    public async Task Buyer_data_is_withheld_unless_it_has_been_deliberately_enabled()
    {
        var (ingestion, _) = Build(includeBuyerData: false);
        await ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");

        await using var verify = _pg.CreateContext();
        var json = (await verify.IntegrationEvents.SingleAsync()).EnvelopeJson;

        // Sending personal data to another system is a decision, not something to inherit from a
        // default. The flag still reports that the data exists.
        json.Should().NotContain("J. Carter");
        json.Should().NotContain("414 W Main St");
        json.Should().Contain("\"buyerDataAvailable\":true");
    }

    [Fact]
    public async Task Buyer_data_is_included_when_enabled()
    {
        var (ingestion, _) = Build(includeBuyerData: true);
        await ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");

        await using var verify = _pg.CreateContext();
        var json = (await verify.IntegrationEvents.SingleAsync()).EnvelopeJson;

        // A merchant-fulfilled order cannot be shipped at the consumer without it.
        json.Should().Contain("414 W Main St");
    }

    [Fact]
    public async Task A_redacted_order_is_never_republished_with_its_address_restored()
    {
        var (ingestion, _) = Build(includeBuyerData: true);
        await ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");

        SalesOrder order;
        await using (var db = _pg.CreateContext())
        {
            order = await db.SalesOrders.Include(o => o.Lines).SingleAsync();
            order.RedactPii(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        await using var publishContext = _pg.CreateContext();
        var publisher = new SalesOrderEventPublisher(
            new IntegrationEventRepository(publishContext),
            new OutboxService(new OutboxRepository(publishContext), new NoOpProcessor(),
                NullLogger<OutboxService>.Instance),
            Options.Create(new IntegrationDeliveryOptions
            {
                EndpointUrl = "https://m360.test/events", IncludeBuyerData = true
            }),
            NullLogger<SalesOrderEventPublisher>.Instance);

        var eventId = await publisher.RepublishAsync(
            order, _connection, "AMAZON_SP", EventPublicationReason.ManualRedelivery, null);

        await using var verify = _pg.CreateContext();
        var json = (await verify.IntegrationEvents.SingleAsync(e => e.EventId == eventId)).EnvelopeJson;

        // Otherwise retention would be something a redelivery could undo.
        json.Should().NotContain("J. Carter");
        json.Should().NotContain("414 W Main St");
    }

    private sealed class NoOpProcessor : IOutboxMessageProcessor
    {
        public Task ProcessAsync(OutboxMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
