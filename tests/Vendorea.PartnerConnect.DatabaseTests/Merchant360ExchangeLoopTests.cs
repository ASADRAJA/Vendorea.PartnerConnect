using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Vendorea.PartnerConnect.Infrastructure.Security;
using Vendorea.PartnerConnect.Infrastructure.Services;
using Vendorea.PartnerConnect.Persistence;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// The whole Merchant360 handoff, end to end — test matrix 4.10.
/// </summary>
/// <remarks>
/// Ingest → stage event and outbox in one transaction → sign → deliver → a receiver that
/// verifies properly → record the acknowledgement → report the business outcome → the five
/// observable states line up.
///
/// Until now every part of Phase 4 was tested against a stub that accepted whatever it was given.
/// The receiver here verifies the signature with its **own** HMAC implementation, so what these
/// tests prove is that two independent implementations agree — which is the only form of this
/// proof worth having before the real consumer exists.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class Merchant360ExchangeLoopTests : IAsyncLifetime
{
    private const string KeyId = "2026-11";
    private const string Secret = "a-shared-signing-secret";
    private const string Endpoint = "https://m360.test/api/v1/partner-connect/integration-events";

    private readonly PostgresFixture _pg;
    private IntegrationConnection _connection = null!;
    private int _tenantId;
    private int _orgId;

    public Merchant360ExchangeLoopTests(PostgresFixture pg) => _pg = pg;

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

    // ---- the harness: PC's real pipeline, pointed at the reference receiver ----

    private sealed record Harness(
        SalesOrderIngestionService Ingestion,
        Func<Task<int>> RunOutbox,
        Func<Task<int>> RunOutboxRetries,
        ReferenceConsumerReceiver Receiver,
        Func<Guid, IntegrationEventProcessingResult, string?, string?, Task<ProcessingResultOutcome>> ReportResult);

    private Harness Build(
        ReferenceConsumerReceiver? receiver = null,
        bool includeBuyerData = false,
        string signingKeyId = KeyId,
        string signingSecret = Secret,
        Func<string, string>? tamperInTransit = null)
    {
        receiver ??= new ReferenceConsumerReceiver((KeyId, Secret));

        // Sits between PC's signing and the receiver's verification, which is where the signature
        // is actually meant to protect the body. Modifying the stored envelope instead would just
        // get it re-signed at delivery — the signature covers the request, not the row at rest.
        HttpMessageHandler transport = tamperInTransit is null
            ? receiver
            : new TamperingTransport(receiver, tamperInTransit);

        var options = Options.Create(new IntegrationDeliveryOptions
        {
            EndpointUrl = Endpoint,
            IncludeBuyerData = includeBuyerData,
            SigningKeys = { new SigningKeySetting
                { KeyId = signingKeyId, Secret = signingSecret, IsPrimary = true } }
        });

        var keys = new IntegrationSigningKeyProvider(
            options, NullLogger<IntegrationSigningKeyProvider>.Instance);

        // The ingest scope: one DbContext, as a request or a poll cycle would have.
        var ingestDb = _pg.CreateContext();
        var publisher = new SalesOrderEventPublisher(
            new IntegrationEventRepository(ingestDb),
            new OutboxService(new OutboxRepository(ingestDb), new NoOpProcessor(),
                NullLogger<OutboxService>.Instance),
            options,
            NullLogger<SalesOrderEventPublisher>.Instance);

        var ingestion = new SalesOrderIngestionService(
            new SalesOrderRepository(ingestDb),
            NullLogger<SalesOrderIngestionService>.Instance,
            publisher);

        // The delivery worker runs in its own scope, as the real one does.
        async Task<int> RunOutbox()
        {
            await using var workerDb = _pg.CreateContext();

            var delivery = new IntegrationEventDeliveryService(
                new HttpClient(transport),
                new IntegrationEventRepository(workerDb),
                keys,
                options,
                NullLogger<IntegrationEventDeliveryService>.Instance);

            var outboxService = new OutboxService(
                new OutboxRepository(workerDb),
                new DefaultOutboxMessageProcessor(
                    new StubHttpClientFactory(),
                    UnusedMerchant360Client(),
                    Options.Create(new SprSimulationOptions()),
                    NullLogger<DefaultOutboxMessageProcessor>.Instance,
                    delivery),
                NullLogger<OutboxService>.Instance);

            return await outboxService.ProcessPendingAsync(batchSize: 50);
        }

        async Task<int> RunOutboxRetries()
        {
            await using var workerDb = _pg.CreateContext();

            var delivery = new IntegrationEventDeliveryService(
                new HttpClient(transport),
                new IntegrationEventRepository(workerDb),
                keys,
                options,
                NullLogger<IntegrationEventDeliveryService>.Instance);

            var outboxService = new OutboxService(
                new OutboxRepository(workerDb),
                new DefaultOutboxMessageProcessor(
                    new StubHttpClientFactory(),
                    UnusedMerchant360Client(),
                    Options.Create(new SprSimulationOptions()),
                    NullLogger<DefaultOutboxMessageProcessor>.Instance,
                    delivery),
                NullLogger<OutboxService>.Instance);

            return await outboxService.ProcessRetriesAsync(batchSize: 50);
        }

        // The consumer's callback. It goes through the same recorder the HTTP endpoint uses, so
        // the ladder is the real one; the controller's steps 1-3 (credential, org owns tenant,
        // order belongs to tenant) are covered separately and are not exercised here.
        async Task<ProcessingResultOutcome> ReportResult(
            Guid eventId, IntegrationEventProcessingResult outcome, string? consumerOrderId, string? error)
        {
            await using var callbackDb = _pg.CreateContext();
            var order = await callbackDb.SalesOrders.SingleAsync(o => o.TenantId == _tenantId);

            var recorder = new ProcessingResultRecorder(
                new IntegrationEventRepository(callbackDb),
                new SalesOrderRepository(callbackDb),
                NullLogger<ProcessingResultRecorder>.Instance);

            return await recorder.RecordAsync(
                _tenantId, order, eventId, outcome, consumerOrderId, error, null);
        }

        return new Harness(ingestion, RunOutbox, RunOutboxRetries, receiver, ReportResult);
    }

    private static MarketplaceOrderSnapshot Snapshot(
        DateTime lastUpdated,
        SalesOrderStatus status = SalesOrderStatus.ReadyToFulfill,
        string statusRaw = "UNSHIPPED")
        => new()
        {
            ExternalOrderId = "111-2223334-5556667",
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
            BuyerName = "J. Carter",
            BuyerEmailRef = "x1y2z3@marketplace.amazon.com",
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

    // ---- the happy path ----

    [Fact]
    public async Task An_ingested_order_travels_the_whole_way_to_a_business_outcome()
    {
        var h = Build();

        var ingested = await h.Ingestion.IngestAsync(
            _connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");
        ingested.Outcome.Should().Be(SalesOrderIngestOutcome.Created);

        var processed = await h.RunOutbox();
        processed.Should().Be(1);

        // A receiver that verifies the signature with its own HMAC accepted it. That is the
        // thing that was never actually proven before: PC emits what a strict consumer takes.
        var received = h.Receiver.Received.Should().ContainSingle().Subject;
        h.Receiver.Rejections.Should().BeEmpty();

        received.EventType.Should().Be("sales_order.created.v1");
        received.TenantId.Should().Be(_tenantId);
        received.OrganizationId.Should().Be(_orgId);
        received.Provider.Should().Be("AMAZON_SP");
        received.ExternalOrderId.Should().Be("111-2223334-5556667");
        received.Status.Should().Be("ReadyToFulfill");

        await using (var db = _pg.CreateContext())
        {
            var published = await db.IntegrationEvents.SingleAsync();

            // Acknowledged, and nothing more than that yet.
            published.DeliveryAcknowledgedAt.Should().NotBeNull();
            published.SigningKeyId.Should().Be(KeyId);
            published.State.Should().Be(IntegrationEventState.DeliveryAcknowledged);
            published.ProcessingResult.Should().BeNull();
        }

        var callback = await h.ReportResult(
            received.EventId, IntegrationEventProcessingResult.Processed, "M360-44821", null);
        callback.Decision.Should().Be(ProcessingResultDecision.Recorded);

        await using var verify = _pg.CreateContext();
        var final = await verify.IntegrationEvents.SingleAsync();
        final.State.Should().Be(IntegrationEventState.ProcessingComplete);
        final.ConsumerOrderId.Should().Be("M360-44821");

        var order = await verify.SalesOrders.SingleAsync();
        order.LastEventId.Should().Be(received.EventId);
        order.LastProcessingResult.Should().Be(SalesOrderProcessingResult.Processed);
    }

    [Fact]
    public async Task The_outbox_message_is_retired_once_the_delivery_is_acknowledged()
    {
        var h = Build();
        await h.Ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");

        await h.RunOutbox();

        await using var db = _pg.CreateContext();
        var message = await db.OutboxMessages.SingleAsync();

        message.Status.Should().Be(OutboxMessageStatus.Delivered);
    }

    // ---- what the receiver would reject, and PC does not send ----

    [Fact]
    public async Task A_body_altered_in_transit_fails_verification()
    {
        // Changed after PC signed it and before the receiver sees it — a man in the middle, which
        // is the threat the signature exists for. (Altering the stored envelope instead proves
        // nothing: it would simply be re-signed at delivery, because the signature covers the
        // request in flight rather than the database row at rest.)
        var h = Build(tamperInTransit: body => body.Replace("ReadyToFulfill", "Fulfilled"));

        await h.Ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");
        await h.RunOutbox();

        h.Receiver.Received.Should().BeEmpty();
        h.Receiver.Rejections.Should().Contain(r => r.Contains("signature does not match"));

        await using var verify = _pg.CreateContext();
        (await verify.IntegrationEvents.SingleAsync()).DeliveryAcknowledgedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_signature_from_an_unknown_key_is_refused()
    {
        var receiver = new ReferenceConsumerReceiver(("2026-11", Secret));

        // PC signs with a key the consumer has never been told about.
        var h = Build(receiver, signingKeyId: "2027-01-rotated-early", signingSecret: "a-new-secret");

        await h.Ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");
        await h.RunOutbox();

        // An unknown key id is a 401, never a fallback to some default — a fallback is how a
        // retired secret keeps working for ever.
        receiver.Rejections.Should().Contain(r => r.Contains("Unknown signing key id"));
        receiver.Received.Should().BeEmpty();
    }

    [Fact]
    public async Task A_rotation_overlap_lets_a_new_key_sign_while_the_old_one_still_verifies()
    {
        // The consumer accepts both for the overlap window.
        var receiver = new ReferenceConsumerReceiver(
            ("2026-08-old", "old-secret"), ("2026-11-new", "new-secret"));

        var h = Build(receiver, signingKeyId: "2026-11-new", signingSecret: "new-secret");

        await h.Ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");
        await h.RunOutbox();

        // No redeploy and no delivery gap — the whole reason the key id is on the wire.
        receiver.Received.Should().ContainSingle();
        receiver.Rejections.Should().BeEmpty();
    }

    [Fact]
    public async Task A_body_re_serialised_in_transit_fails_even_though_it_means_the_same_thing()
    {
        // Semantically identical, byte-different: a proxy that parsed and re-emitted the JSON.
        var h = Build(tamperInTransit: body =>
            System.Text.Json.Nodes.JsonNode.Parse(body)!.ToJsonString(
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        await h.Ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");
        await h.RunOutbox();

        // The trap the contract warns about, demonstrated from the other side: verify against the
        // raw bytes as received, and never re-serialise before signing or before checking. A
        // receiver that deserialised first and re-serialised to verify would reject every valid
        // delivery it was ever sent.
        h.Receiver.Received.Should().BeEmpty();
        h.Receiver.Rejections.Should().Contain(r => r.Contains("signature does not match"));
    }

    // ---- at-least-once delivery, effective-once effects ----

    [Fact]
    public async Task A_transport_retry_reuses_the_event_id_and_the_inbox_suppresses_it()
    {
        var h = Build();
        await h.Ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");

        // First attempt fails at the consumer with a 503.
        h.Receiver.NextResponseOverride = HttpStatusCode.ServiceUnavailable;
        await h.RunOutbox();

        h.Receiver.Received.Should().BeEmpty();

        await using (var db = _pg.CreateContext())
        {
            var message = await db.OutboxMessages.SingleAsync();

            // Scheduled for retry with exponential backoff, not abandoned.
            message.Status.Should().Be(OutboxMessageStatus.Retry);
            message.RetryCount.Should().Be(1);
            message.NextRetryAt.Should().NotBeNull();

            // Brought forward, because this test is about the retry and not about the backoff.
            message.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        await h.RunOutboxRetries();

        var received = h.Receiver.Received.Should().ContainSingle().Subject;

        await using var verify = _pg.CreateContext();
        var published = await verify.IntegrationEvents.SingleAsync();

        // The same event id across both attempts. That is what lets the consumer's inbox dedupe
        // a retry rather than create a second order from it.
        received.EventId.Should().Be(published.EventId);
        published.DeliveryAttempts.Should().Be(2);
        published.DeliveryAcknowledgedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Delivering_the_same_event_twice_is_acknowledged_once_and_suppressed_once()
    {
        var h = Build();
        await h.Ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");
        await h.RunOutbox();

        // Re-queue the same message, as an at-least-once outbox legitimately may.
        await using (var db = _pg.CreateContext())
        {
            var message = await db.OutboxMessages.SingleAsync();
            message.Requeue();
            await db.SaveChangesAsync();

            // And clear the acknowledgement so PC actually sends it again rather than skipping.
            var published = await db.IntegrationEvents.SingleAsync();
            published.DeliveryAcknowledgedAt = null;
            await db.SaveChangesAsync();
        }

        await h.RunOutbox();

        // One order's worth of effect from two deliveries. The inbox is the consumer's
        // obligation, not the publisher's, and this is it working.
        h.Receiver.Received.Should().ContainSingle();
        h.Receiver.DuplicatesSuppressed.Should().Be(1);
    }

    // ---- a redelivery updates, never duplicates ----

    [Fact]
    public async Task A_redelivery_arrives_as_a_new_event_carrying_the_same_order_identity()
    {
        var h = Build();
        await h.Ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");
        await h.RunOutbox();

        var first = h.Receiver.Received.Single();

        SalesOrder order;
        await using (var db = _pg.CreateContext())
        {
            order = await db.SalesOrders.Include(o => o.Lines).SingleAsync();
        }

        await using (var publishDb = _pg.CreateContext())
        {
            var publisher = new SalesOrderEventPublisher(
                new IntegrationEventRepository(publishDb),
                new OutboxService(new OutboxRepository(publishDb), new NoOpProcessor(),
                    NullLogger<OutboxService>.Instance),
                Options.Create(new IntegrationDeliveryOptions
                {
                    EndpointUrl = Endpoint,
                    SigningKeys = { new SigningKeySetting
                        { KeyId = KeyId, Secret = Secret, IsPrimary = true } }
                }),
                NullLogger<SalesOrderEventPublisher>.Instance);

            await publisher.RepublishAsync(
                order, _connection, "AMAZON_SP", EventPublicationReason.ManualRedelivery, null);
        }

        await h.RunOutbox();

        h.Receiver.Received.Should().HaveCount(2);
        var second = h.Receiver.Received.Last();

        // A new event id, so the inbox treats it as new work rather than deduping it away.
        second.EventId.Should().NotBe(first.EventId);
        second.PublicationReason.Should().Be("ManualRedelivery");

        // The same order identity, so the consumer updates the order it already has. Identity
        // governs; the event id never does.
        second.OrderIdentityKey.Should().Be(first.OrderIdentityKey);
        second.EventType.Should().Be("sales_order.updated.v1");

        // And the consumer is not left guessing which order: nothing about the identity moved.
        second.PcSalesOrderId.Should().Be(first.PcSalesOrderId);
    }

    [Fact]
    public async Task A_genuine_status_change_is_a_different_transition_to_the_consumer()
    {
        var h = Build();
        var t0 = DateTime.UtcNow.AddHours(-2);

        await h.Ingestion.IngestAsync(_connection, Snapshot(t0), "poll");
        await h.RunOutbox();

        var h2 = Build(h.Receiver);
        await h2.Ingestion.IngestAsync(
            _connection, Snapshot(t0.AddMinutes(30), SalesOrderStatus.Fulfilled, "SHIPPED"), "poll");
        await h2.RunOutbox();

        h.Receiver.Received.Should().HaveCount(2);
        var keys = h.Receiver.Received.Select(e => e.DeduplicationKey).ToList();

        // Different deduplication keys, so the consumer can tell this is new work rather than a
        // retry of the first. Equal keys would have the inbox silently drop the shipment.
        keys.Should().OnlyHaveUniqueItems();
        h.Receiver.DuplicatesSuppressed.Should().Be(0);
        h.Receiver.Received.Last().Status.Should().Be("Fulfilled");
    }

    // ---- PII on the wire ----

    [Fact]
    public async Task No_personal_data_reaches_the_consumer_unless_it_was_deliberately_enabled()
    {
        var h = Build(includeBuyerData: false);
        await h.Ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");
        await h.RunOutbox();

        var body = h.Receiver.Received.Single().RawBody;

        body.Should().NotContain("J. Carter");
        body.Should().NotContain("414 W Main St");
        body.Should().NotContain("x1y2z3@marketplace.amazon.com");
        body.Should().Contain("\"buyerDataAvailable\":true");
    }

    [Fact]
    public async Task The_address_reaches_the_consumer_when_it_is_enabled()
    {
        var h = Build(includeBuyerData: true);
        await h.Ingestion.IngestAsync(_connection, Snapshot(DateTime.UtcNow.AddMinutes(-5)), "poll");
        await h.RunOutbox();

        // A merchant-fulfilled order cannot be shipped at the consumer without it.
        h.Receiver.Received.Single().RawBody.Should().Contain("414 W Main St");
    }

    // ---- the published test vector, from the receiver's side ----

    [Fact]
    public void The_reference_receiver_reproduces_the_published_test_vector()
    {
        // The receiver computes its HMAC from first principles and does not call PC's
        // IntegrationSignature. Both landing on the same value is the only check that the two
        // sides genuinely agree rather than that one agrees with itself — and it is the check the
        // real Merchant360 receiver must also pass.
        var independent = ReceiverSignatureForTestVector();

        independent.Should().Be(IntegrationSignature.TestVector.Expected);
        independent.Should().Be(
            "v1=a050819d48d6e1cf3796d4b825f39b856ceb17d551e435ad906747a354eedd0c");
    }

    private static string ReceiverSignatureForTestVector()
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(IntegrationSignature.TestVector.Secret));

        var signingString =
            IntegrationSignature.TestVector.Timestamp + "." + IntegrationSignature.TestVector.Body;

        return "v1=" + Convert.ToHexString(
            hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(signingString))).ToLowerInvariant();
    }

    // ---- stubs for the parts of the outbox processor this loop does not use ----

    private sealed class NoOpProcessor : IOutboxMessageProcessor
    {
        public Task ProcessAsync(OutboxMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Alters the request body after PC has signed it and before the receiver verifies it.
    /// </summary>
    /// <remarks>
    /// Stands in for anything between the two that rewrites bytes — a proxy, a gateway, an
    /// attacker. The headers are passed through untouched, so what the receiver sees is a valid
    /// signature over content that no longer matches.
    /// </remarks>
    private sealed class TamperingTransport : DelegatingHandler
    {
        private readonly Func<string, string> _tamper;

        public TamperingTransport(HttpMessageHandler inner, Func<string, string> tamper)
            : base(inner)
            => _tamper = tamper;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                var original = await request.Content.ReadAsStringAsync(cancellationToken);
                request.Content = new StringContent(
                    _tamper(original), System.Text.Encoding.UTF8, "application/json");
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    /// <summary>
    /// A do-nothing Merchant360 client for the SPR paths of the outbox processor.
    /// </summary>
    /// <remarks>
    /// Generated rather than hand-written: the interface carries thirty-odd members for the
    /// price, content, subscription and inventory flows, none of which this loop touches. A
    /// hand-written stub would be forty lines of noise that also has to be maintained whenever
    /// an unrelated member is added.
    /// </remarks>
    private static Contracts.Interfaces.IMerchant360Client UnusedMerchant360Client()
        => System.Reflection.DispatchProxy.Create<
            Contracts.Interfaces.IMerchant360Client, ThrowingProxy>();

    public class ThrowingProxy : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            => throw new NotSupportedException(
                $"{targetMethod?.Name} is not part of the Merchant360 event-exchange loop.");
    }
}
