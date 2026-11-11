using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Domain.Entities.Sales;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// The processing-result validation ladder, steps 4 to 6 — test matrix 4.3 (correction C4).
/// </summary>
/// <remarks>
/// The contract's central warning is that a successful HTTP acknowledgement must never be read as
/// successful business processing. This is the path that carries the real answer, and it is the
/// path an attacker or a confused consumer would use to write a result against the wrong order,
/// so each relationship is verified rather than assumed.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class ProcessingResultLadderTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private IntegrationConnection _connection = null!;
    private int _tenantId;
    private int _otherTenantId;
    private int _orgId;

    public ProcessingResultLadderTests(PostgresFixture pg) => _pg = pg;

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

        var connection = new IntegrationConnection
        {
            OrganizationId = org.Id, TenantId = t1.Id, IntegrationProviderId = provider.Id,
            ExternalAccountId = "SELLER-A", Status = IntegrationConnectionStatus.Connected
        };
        db.IntegrationConnections.Add(connection);
        await db.SaveChangesAsync();
        _connection = connection;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ProcessingResultRecorder Recorder(Persistence.PartnerConnectDbContext db)
        => new(new IntegrationEventRepository(db), new SalesOrderRepository(db),
            NullLogger<ProcessingResultRecorder>.Instance);

    private async Task<(Guid OrderId, Guid EventId)> SeedAsync(
        int? tenantId = null, string reference = "111-0000001-0000001")
    {
        await using var db = _pg.CreateContext();

        var order = new SalesOrder
        {
            IntegrationConnectionId = _connection.Id,
            TenantId = tenantId ?? _tenantId,
            ExternalOrderId = reference,
            Status = SalesOrderStatus.ReadyToFulfill,
            FulfillmentChannel = FulfillmentChannel.MerchantFulfilled,
            MappingStatus = MappingStatus.Mapped,
            PurchaseDate = DateTime.UtcNow.AddHours(-2),
            SourceLastUpdatedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync();

        var published = new IntegrationEvent
        {
            EventType = IntegrationEventType.SalesOrderCreated,
            OrganizationId = _orgId,
            TenantId = order.TenantId,
            IntegrationConnectionId = _connection.Id,
            SalesOrderId = order.Id,
            DeduplicationKey = Guid.NewGuid().ToString("n"),
            EnvelopeJson = "{}",
            DeliveryAcknowledgedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        db.IntegrationEvents.Add(published);
        await db.SaveChangesAsync();

        return (order.Id, published.EventId);
    }

    private async Task<SalesOrder> LoadOrderAsync(
        Persistence.PartnerConnectDbContext db, Guid orderId, int? tenantId = null)
        => await db.SalesOrders.SingleAsync(o => o.Id == orderId && o.TenantId == (tenantId ?? _tenantId));

    // ---- step 4 ----

    [Fact]
    public async Task A_result_for_an_event_that_was_never_published_is_refused()
    {
        var (orderId, _) = await SeedAsync();

        await using var db = _pg.CreateContext();
        var result = await Recorder(db).RecordAsync(
            _tenantId, await LoadOrderAsync(db, orderId), Guid.NewGuid(),
            IntegrationEventProcessingResult.Processed, "M360-1", null, null);

        // A result for an event we never sent is not something to record.
        result.Decision.Should().Be(ProcessingResultDecision.UnknownSourceEvent);
    }

    [Fact]
    public async Task A_result_naming_another_orders_event_is_refused()
    {
        var (firstOrderId, firstEventId) = await SeedAsync(reference: "111-0000001-0000001");
        var (secondOrderId, _) = await SeedAsync(reference: "111-0000002-0000002");

        await using var db = _pg.CreateContext();

        // The event belongs to the first order; the callback claims it is about the second.
        var result = await Recorder(db).RecordAsync(
            _tenantId, await LoadOrderAsync(db, secondOrderId), firstEventId,
            IntegrationEventProcessingResult.Processed, "M360-1", null, null);

        result.Decision.Should().Be(ProcessingResultDecision.UnknownSourceEvent);

        await using var verify = _pg.CreateContext();

        // And nothing was written to either order — the relationship is checked, not assumed.
        (await verify.IntegrationEvents.SingleAsync(e => e.EventId == firstEventId))
            .ProcessingResult.Should().BeNull();
        (await verify.SalesOrders.SingleAsync(o => o.Id == firstOrderId))
            .LastProcessingResult.Should().BeNull();
    }

    [Fact]
    public async Task A_result_from_the_wrong_tenant_is_refused()
    {
        var (orderId, eventId) = await SeedAsync();

        await using var db = _pg.CreateContext();
        var order = await LoadOrderAsync(db, orderId);

        // The order and event are the real ones; only the claimed tenant is wrong.
        var result = await Recorder(db).RecordAsync(
            _otherTenantId, order, eventId,
            IntegrationEventProcessingResult.Processed, "M360-1", null, null);

        result.Decision.Should().Be(ProcessingResultDecision.UnknownSourceEvent);
    }

    // ---- recording ----

    [Fact]
    public async Task A_first_result_is_recorded_on_the_event_and_mirrored_onto_the_order()
    {
        var (orderId, eventId) = await SeedAsync();

        await using var db = _pg.CreateContext();
        var result = await Recorder(db).RecordAsync(
            _tenantId, await LoadOrderAsync(db, orderId), eventId,
            IntegrationEventProcessingResult.Processed, "M360-44821", null, null);

        result.Decision.Should().Be(ProcessingResultDecision.Recorded);

        await using var verify = _pg.CreateContext();

        var published = await verify.IntegrationEvents.SingleAsync(e => e.EventId == eventId);
        published.ProcessingResult.Should().Be(IntegrationEventProcessingResult.Processed);
        published.ConsumerOrderId.Should().Be("M360-44821");
        published.ProcessingResultAt.Should().NotBeNull();

        // Mirrored so the ordinary order read shows the latest outcome without a join.
        var order = await verify.SalesOrders.SingleAsync(o => o.Id == orderId);
        order.LastEventId.Should().Be(eventId);
        order.LastProcessingResult.Should().Be(SalesOrderProcessingResult.Processed);
    }

    [Fact]
    public async Task A_failure_is_recorded_with_its_error()
    {
        var (orderId, eventId) = await SeedAsync();

        await using var db = _pg.CreateContext();
        await Recorder(db).RecordAsync(
            _tenantId, await LoadOrderAsync(db, orderId), eventId,
            IntegrationEventProcessingResult.FailedPermanent, null, "SKU not mapped", null);

        await using var verify = _pg.CreateContext();
        var published = await verify.IntegrationEvents.SingleAsync(e => e.EventId == eventId);

        published.ProcessingResult.Should().Be(IntegrationEventProcessingResult.FailedPermanent);
        published.ProcessingError.Should().Be("SKU not mapped");

        // A real Amazon order with no Merchant360 order behind it, visible on the order itself.
        (await verify.SalesOrders.SingleAsync(o => o.Id == orderId))
            .LastProcessingResult.Should().Be(SalesOrderProcessingResult.FailedPermanent);
    }

    [Fact]
    public async Task The_consumers_own_attempt_time_is_kept_when_it_supplies_one()
    {
        var (orderId, eventId) = await SeedAsync();
        var attemptedAt = new DateTime(2026, 11, 10, 9, 30, 0, DateTimeKind.Utc);

        await using var db = _pg.CreateContext();
        await Recorder(db).RecordAsync(
            _tenantId, await LoadOrderAsync(db, orderId), eventId,
            IntegrationEventProcessingResult.Processed, "M360-1", null, attemptedAt);

        await using var verify = _pg.CreateContext();

        // When the handler ran is the consumer's fact, not ours. Overwriting it with our clock
        // would make a delayed callback look instantaneous.
        (await verify.IntegrationEvents.SingleAsync(e => e.EventId == eventId))
            .ProcessingResultAt.Should().Be(attemptedAt);
    }

    // ---- step 5: idempotency ----

    [Fact]
    public async Task An_identical_repeated_result_is_idempotent_and_keeps_the_original()
    {
        var (orderId, eventId) = await SeedAsync();

        DateTime firstRecordedAt;
        await using (var db = _pg.CreateContext())
        {
            await Recorder(db).RecordAsync(
                _tenantId, await LoadOrderAsync(db, orderId), eventId,
                IntegrationEventProcessingResult.Processed, "M360-1", null, null);
        }

        await using (var read = _pg.CreateContext())
        {
            firstRecordedAt = (await read.IntegrationEvents.SingleAsync(e => e.EventId == eventId))
                .ProcessingResultAt!.Value;
        }

        await Task.Delay(30);

        await using var db2 = _pg.CreateContext();
        var repeat = await Recorder(db2).RecordAsync(
            _tenantId, await LoadOrderAsync(db2, orderId), eventId,
            IntegrationEventProcessingResult.Processed, "M360-1", null, null);

        // A retried callback is normal and must change nothing — including the timestamp, which
        // records when the outcome actually happened.
        repeat.Decision.Should().Be(ProcessingResultDecision.AlreadyRecorded);

        await using var verify = _pg.CreateContext();
        (await verify.IntegrationEvents.SingleAsync(e => e.EventId == eventId))
            .ProcessingResultAt.Should().Be(firstRecordedAt);
    }

    // ---- step 6: first result wins ----

    [Fact]
    public async Task A_conflicting_outcome_is_refused_and_the_first_result_stands()
    {
        var (orderId, eventId) = await SeedAsync();

        await using (var db = _pg.CreateContext())
        {
            await Recorder(db).RecordAsync(
                _tenantId, await LoadOrderAsync(db, orderId), eventId,
                IntegrationEventProcessingResult.Processed, "M360-1", null, null);
        }

        await using var db2 = _pg.CreateContext();
        var conflicting = await Recorder(db2).RecordAsync(
            _tenantId, await LoadOrderAsync(db2, orderId), eventId,
            IntegrationEventProcessingResult.FailedPermanent, null, "changed my mind", null);

        conflicting.Decision.Should().Be(ProcessingResultDecision.Conflicting);
        conflicting.Recorded.Should().Be(IntegrationEventProcessingResult.Processed);

        await using var verify = _pg.CreateContext();
        var published = await verify.IntegrationEvents.SingleAsync(e => e.EventId == eventId);

        // Two different answers for one event means the handler ran twice with different
        // effects. Overwriting would hide that rather than surface it.
        published.ProcessingResult.Should().Be(IntegrationEventProcessingResult.Processed);
        published.ProcessingError.Should().BeNull();
    }

    [Fact]
    public async Task A_different_consumer_order_id_for_the_same_outcome_is_also_a_conflict()
    {
        var (orderId, eventId) = await SeedAsync();

        await using (var db = _pg.CreateContext())
        {
            await Recorder(db).RecordAsync(
                _tenantId, await LoadOrderAsync(db, orderId), eventId,
                IntegrationEventProcessingResult.Processed, "M360-1", null, null);
        }

        await using var db2 = _pg.CreateContext();
        var conflicting = await Recorder(db2).RecordAsync(
            _tenantId, await LoadOrderAsync(db2, orderId), eventId,
            IntegrationEventProcessingResult.Processed, "M360-2", null, null);

        // Same outcome, different order id: the consumer created two orders for one event, which
        // is exactly the duplicate the whole exchange exists to prevent.
        conflicting.Decision.Should().Be(ProcessingResultDecision.Conflicting);

        await using var verify = _pg.CreateContext();
        (await verify.IntegrationEvents.SingleAsync(e => e.EventId == eventId))
            .ConsumerOrderId.Should().Be("M360-1");
    }

    [Fact]
    public async Task Concurrent_identical_callbacks_leave_exactly_one_recorded_result()
    {
        var (orderId, eventId) = await SeedAsync();

        var tasks = Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var db = _pg.CreateContext();
            return await Recorder(db).RecordAsync(
                _tenantId, await LoadOrderAsync(db, orderId), eventId,
                IntegrationEventProcessingResult.Processed, "M360-1", null, null);
        });

        var results = await Task.WhenAll(tasks);

        // Whichever order they land in, the event ends up with one outcome and the callers are
        // told either "recorded" or "already recorded" — never a conflict for identical input.
        results.Should().NotContain(r => r.Decision == ProcessingResultDecision.Conflicting);
        results.Should().Contain(r => r.Decision == ProcessingResultDecision.Recorded);

        await using var verify = _pg.CreateContext();
        (await verify.IntegrationEvents.SingleAsync(e => e.EventId == eventId))
            .ProcessingResult.Should().Be(IntegrationEventProcessingResult.Processed);
    }

    // ---- the five observable states ----

    [Fact]
    public async Task The_event_state_reflects_how_far_the_handoff_got()
    {
        var (orderId, eventId) = await SeedAsync();

        await using (var db = _pg.CreateContext())
        {
            var published = await db.IntegrationEvents.SingleAsync(e => e.EventId == eventId);

            // Acknowledged but not yet reported on — the gap the contract warns about, where a
            // 202 is mistaken for a business outcome.
            published.State.Should().Be(IntegrationEventState.DeliveryAcknowledged);

            published.DeliveryAcknowledgedAt = null;
            published.State.Should().Be(IntegrationEventState.OutboxCreated);
        }

        await using (var db = _pg.CreateContext())
        {
            await Recorder(db).RecordAsync(
                _tenantId, await LoadOrderAsync(db, orderId), eventId,
                IntegrationEventProcessingResult.Processed, "M360-1", null, null);
        }

        await using var verify = _pg.CreateContext();
        (await verify.IntegrationEvents.SingleAsync(e => e.EventId == eventId))
            .State.Should().Be(IntegrationEventState.ProcessingComplete);
    }

    [Fact]
    public async Task A_retryable_failure_and_a_permanent_one_both_report_processing_failed()
    {
        var (orderId, eventId) = await SeedAsync();

        await using var db = _pg.CreateContext();
        await Recorder(db).RecordAsync(
            _tenantId, await LoadOrderAsync(db, orderId), eventId,
            IntegrationEventProcessingResult.FailedRetryable, null, "database busy", null);

        await using var verify = _pg.CreateContext();

        // Six enum values, five states: processing has two outcomes.
        (await verify.IntegrationEvents.SingleAsync(e => e.EventId == eventId))
            .State.Should().Be(IntegrationEventState.ProcessingFailed);
    }
}
