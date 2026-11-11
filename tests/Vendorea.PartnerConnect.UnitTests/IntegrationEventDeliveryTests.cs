using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities.Integration;
using Vendorea.PartnerConnect.Infrastructure.Security;
using Vendorea.PartnerConnect.Infrastructure.Services;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests;

/// <summary>
/// Signed delivery and key rotation — test matrix 4.2 (correction C1).
/// </summary>
public class IntegrationSigningKeyProviderTests
{
    private static IntegrationSigningKeyProvider Build(params SigningKeySetting[] keys)
        => new(
            Options.Create(new IntegrationDeliveryOptions { SigningKeys = keys.ToList() }),
            NullLogger<IntegrationSigningKeyProvider>.Instance);

    [Fact]
    public void The_primary_key_signs_and_every_configured_key_still_verifies()
    {
        var provider = Build(
            new SigningKeySetting { KeyId = "2026-11-new", Secret = "new-secret", IsPrimary = true },
            new SigningKeySetting { KeyId = "2026-08-old", Secret = "old-secret" });

        // Rotation needs an overlap: the new key signs while the old one still verifies, then the
        // old id is retired. One secret forces a simultaneous change on both sides, which in
        // practice means a delivery gap.
        provider.Primary!.Value.KeyId.Should().Be("2026-11-new");
        provider.Resolve("2026-08-old").Should().Be("old-secret");
        provider.Resolve("2026-11-new").Should().Be("new-secret");
        provider.AcceptedKeys.Should().HaveCount(2);
    }

    [Fact]
    public void An_unknown_key_id_resolves_to_nothing()
    {
        Build(new SigningKeySetting { KeyId = "a", Secret = "s", IsPrimary = true })
            .Resolve("retired-last-year").Should().BeNull();
    }

    [Fact]
    public void A_null_key_id_resolves_to_nothing()
    {
        Build(new SigningKeySetting { KeyId = "a", Secret = "s", IsPrimary = true })
            .Resolve(null).Should().BeNull();
    }

    [Fact]
    public void Two_primaries_leave_no_primary_at_all()
    {
        var provider = Build(
            new SigningKeySetting { KeyId = "a", Secret = "s1", IsPrimary = true },
            new SigningKeySetting { KeyId = "b", Secret = "s2", IsPrimary = true });

        // Ambiguous rather than harmless: whichever was listed first would sign, and a consumer
        // debugging a signature failure would be told the wrong key id.
        provider.Primary.Should().BeNull();
    }

    [Fact]
    public void Keys_with_no_primary_leave_no_primary()
    {
        Build(new SigningKeySetting { KeyId = "a", Secret = "s" }).Primary.Should().BeNull();
    }

    [Fact]
    public void Incomplete_key_entries_are_ignored()
    {
        var provider = Build(
            new SigningKeySetting { KeyId = "", Secret = "orphan" },
            new SigningKeySetting { KeyId = "no-secret", Secret = "" },
            new SigningKeySetting { KeyId = "good", Secret = "s", IsPrimary = true });

        provider.AcceptedKeys.Should().ContainSingle();
        provider.Primary!.Value.KeyId.Should().Be("good");
    }
}

/// <summary>
/// The delivery itself — headers, the signature over the stored bytes, and what an
/// acknowledgement does and does not mean.
/// </summary>
public class IntegrationEventDeliveryServiceTests
{
    private const string Envelope = """{"eventId":"7f1c0b2a-9d3e-4a51-8c77-2b9e4a1f6d30"}""";

    private static (IntegrationEventDeliveryService Service, StubHandler Handler, StubEvents Events)
        Build(HttpStatusCode status = HttpStatusCode.Accepted,
              IntegrationEvent? integrationEvent = null,
              bool configureKey = true,
              string? endpoint = "https://m360.test/events")
    {
        var handler = new StubHandler(status);
        var events = new StubEvents(integrationEvent ?? NewEvent());

        var keys = configureKey
            ? new List<SigningKeySetting>
            {
                new() { KeyId = "2026-11", Secret = "pc-test-secret", IsPrimary = true }
            }
            : new List<SigningKeySetting>();

        var options = Options.Create(new IntegrationDeliveryOptions
        {
            EndpointUrl = endpoint,
            SigningKeys = keys
        });

        var service = new IntegrationEventDeliveryService(
            new HttpClient(handler),
            events,
            new IntegrationSigningKeyProvider(options, NullLogger<IntegrationSigningKeyProvider>.Instance),
            options,
            NullLogger<IntegrationEventDeliveryService>.Instance);

        return (service, handler, events);
    }

    private static IntegrationEvent NewEvent() => new()
    {
        EventId = Guid.Parse("7f1c0b2a-9d3e-4a51-8c77-2b9e4a1f6d30"),
        EventType = IntegrationEventType.SalesOrderCreated,
        TenantId = 7,
        DeduplicationKey = "abc",
        EnvelopeJson = Envelope
    };

    [Fact]
    public async Task All_three_contract_headers_are_sent()
    {
        var (service, handler, _) = Build();

        (await service.DeliverAsync(NewEvent().EventId)).Should().BeTrue();

        // All three are required by the contract, and the consumer rejects with 401 if any is
        // missing.
        handler.Headers.Should().ContainKey(IntegrationEventDeliveryService.TimestampHeader);
        handler.Headers.Should().ContainKey(IntegrationEventDeliveryService.KeyIdHeader);
        handler.Headers.Should().ContainKey(IntegrationEventDeliveryService.SignatureHeader);

        handler.Headers[IntegrationEventDeliveryService.KeyIdHeader].Should().Be("2026-11");
        handler.Headers[IntegrationEventDeliveryService.SignatureHeader].Should().StartWith("v1=");
    }

    [Fact]
    public async Task The_signature_covers_the_timestamp_and_the_exact_stored_bytes()
    {
        var (service, handler, _) = Build();

        await service.DeliverAsync(NewEvent().EventId);

        var timestamp = handler.Headers[IntegrationEventDeliveryService.TimestampHeader];
        var expected = IntegrationSignature.Compute("pc-test-secret", timestamp, Envelope);

        handler.Headers[IntegrationEventDeliveryService.SignatureHeader].Should().Be(expected);

        // The body sent is byte-identical to what was stored. Re-serialising the envelope would
        // change whitespace or key order, and the consumer's recomputation would not match.
        handler.Body.Should().Be(Envelope);
    }

    [Fact]
    public async Task The_timestamp_is_RFC3339_UTC_to_the_second()
    {
        var (service, handler, _) = Build();

        await service.DeliverAsync(NewEvent().EventId);

        var timestamp = handler.Headers[IntegrationEventDeliveryService.TimestampHeader];

        // The same string is signed and sent. Formatting it twice would risk the two differing
        // by a tick and failing verification for no visible reason.
        timestamp.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$");
    }

    [Fact]
    public async Task A_success_records_the_acknowledgement_and_nothing_more()
    {
        var (service, _, events) = Build(HttpStatusCode.Accepted);

        await service.DeliverAsync(NewEvent().EventId);

        events.Event.DeliveryAcknowledgedAt.Should().NotBeNull();
        events.Event.SigningKeyId.Should().Be("2026-11");
        events.Event.DeliveryAttempts.Should().Be(1);

        // Transport only. The contract is explicit that a 202 must never be read as successful
        // business processing — that arrives separately on the processing-result endpoint.
        events.Event.ProcessingResult.Should().BeNull();
        events.Event.State.Should().Be(IntegrationEventState.DeliveryAcknowledged);
    }

    [Fact]
    public async Task An_already_acknowledged_event_is_not_delivered_again()
    {
        var acknowledged = NewEvent();
        acknowledged.DeliveryAcknowledgedAt = DateTime.UtcNow.AddMinutes(-1);

        var (service, handler, _) = Build(integrationEvent: acknowledged);

        (await service.DeliverAsync(acknowledged.EventId)).Should().BeTrue();

        // The outbox is at-least-once, so seeing a message twice is normal. Delivering twice
        // would make the consumer dedupe work it need not have been given.
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Without_a_primary_key_nothing_is_sent_unsigned()
    {
        var (service, handler, events) = Build(configureKey: false);

        (await service.DeliverAsync(NewEvent().EventId)).Should().BeFalse();

        // An unsigned delivery is one the consumer must reject, so sending it would turn a
        // configuration error into a stream of 401s.
        handler.Calls.Should().Be(0);
        events.Event.LastDeliveryError.Should().Contain("signing key");
    }

    [Fact]
    public async Task Without_an_endpoint_nothing_is_sent()
    {
        var (service, handler, events) = Build(endpoint: null);

        (await service.DeliverAsync(NewEvent().EventId)).Should().BeFalse();

        handler.Calls.Should().Be(0);
        events.Event.LastDeliveryError.Should().Contain("endpoint");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task A_rejection_is_recorded_and_reported_for_retry(HttpStatusCode status)
    {
        var (service, _, events) = Build(status);

        (await service.DeliverAsync(NewEvent().EventId)).Should().BeFalse();

        // The outbox owns the retry budget and the dead-letter, so the delivery reports honestly
        // and lets it decide rather than deciding on its behalf.
        events.Event.DeliveryAcknowledgedAt.Should().BeNull();
        events.Event.LastDeliveryError.Should().Contain(((int)status).ToString());
        events.Event.DeliveryAttempts.Should().Be(1);
    }

    [Fact]
    public async Task An_unreachable_consumer_is_recorded_without_leaking_the_envelope()
    {
        var events = new StubEvents(NewEvent());
        var options = Options.Create(new IntegrationDeliveryOptions
        {
            EndpointUrl = "https://m360.test/events",
            SigningKeys = { new SigningKeySetting { KeyId = "k", Secret = "s", IsPrimary = true } }
        });

        var service = new IntegrationEventDeliveryService(
            new HttpClient(new ThrowingHandler()),
            events,
            new IntegrationSigningKeyProvider(options, NullLogger<IntegrationSigningKeyProvider>.Instance),
            options,
            NullLogger<IntegrationEventDeliveryService>.Instance);

        (await service.DeliverAsync(NewEvent().EventId)).Should().BeFalse();

        events.Event.LastDeliveryError.Should().NotBeNullOrEmpty();
        events.Event.LastDeliveryError.Should().NotContain("eventId");
    }

    [Fact]
    public async Task A_missing_event_retires_the_message_rather_than_retrying_for_ever()
    {
        var (service, _, _) = Build();

        // Nothing to deliver and nothing to wait for.
        (await service.DeliverAsync(Guid.NewGuid())).Should().BeTrue();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public StubHandler(HttpStatusCode status) => _status = status;

        public int Calls { get; private set; }
        public string? Body { get; private set; }
        public Dictionary<string, string> Headers { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            Headers.Clear();
            foreach (var header in request.Headers)
            {
                Headers[header.Key] = string.Join(',', header.Value);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent("", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Connection refused");
    }

    private sealed class StubEvents : IIntegrationEventRepository
    {
        public StubEvents(IntegrationEvent integrationEvent) => Event = integrationEvent;

        public IntegrationEvent Event { get; }

        public void Stage(IntegrationEvent integrationEvent) { }

        public Task<IntegrationEvent?> GetAsync(Guid eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(eventId == Event.EventId ? Event : null);

        public Task<IReadOnlyList<IntegrationEvent>> GetForOrderAsync(
            Guid salesOrderId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
