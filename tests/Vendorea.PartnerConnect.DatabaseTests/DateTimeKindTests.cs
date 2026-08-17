using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// Characterises how Npgsql handles DateTime.Kind, so the behaviour is written down rather than
/// rediscovered.
/// </summary>
/// <remarks>
/// Npgsql maps DateTime to timestamptz and rejects anything whose Kind is not Utc. SQL Server did
/// not care. PartnerConnect is in good shape here - 508 DateTime.UtcNow and no DateTime.Today or
/// DateTime.Now anywhere in src - so the exposure is limited to a handful of new DateTime(...)
/// literals.
///
/// These tests deliberately assert the *current* behaviour, including the failures. If someone
/// enables AppContext switch "Npgsql.EnableLegacyTimestampBehavior" the two throwing tests will
/// start failing, which is the point: that switch is a decision, not something to slip in.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class DateTimeKindTests
{
    private readonly PostgresFixture _pg;

    public DateTimeKindTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Utc_datetimes_work_in_predicates()
    {
        await using var db = _pg.CreateContext();

        var act = async () => await db.Set<SprPriceRecord>()
            .CountAsync(r => r.CreatedAt >= DateTime.UtcNow.AddDays(-30));

        await act.Should().NotThrowAsync("DateTime.UtcNow carries Kind=Utc, which is all Npgsql asks for");
    }

    [Fact]
    public async Task Unspecified_kind_literals_are_rejected()
    {
        await using var db = _pg.CreateContext();

        var act = async () => await db.Set<SprPriceRecord>()
            .CountAsync(r => r.CreatedAt >= new DateTime(2026, 1, 1));

        // new DateTime(...) produces Kind=Unspecified. Npgsql refuses rather than guessing a
        // timezone - the alternative would be silently shifting every comparison.
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unspecified*", "use DateTime.SpecifyKind(..., DateTimeKind.Utc) or a UTC literal");
    }

    [Fact]
    public async Task Local_kind_values_are_rejected()
    {
        await using var db = _pg.CreateContext();

        var act = async () => await db.Set<SprPriceRecord>()
            .CountAsync(r => r.CreatedAt >= DateTime.Today);

        // DateTime.Today is Kind=Local. There are none in src today; this guards against one
        // being introduced.
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Utc_values_round_trip_unchanged()
    {
        var written = DateTime.UtcNow;

        await using var db = _pg.CreateContext();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "kind-round-trip",
            Payload = "{}",
            Status = OutboxMessageStatus.Pending,
            CreatedAt = written
        };
        db.Set<OutboxMessage>().Add(message);
        await db.SaveChangesAsync();

        await using var read = _pg.CreateContext();
        var loaded = await read.Set<OutboxMessage>().SingleAsync(m => m.Id == message.Id);

        loaded.CreatedAt.Kind.Should().Be(DateTimeKind.Utc, "values must come back as UTC, not Unspecified");
        loaded.CreatedAt.Should().BeCloseTo(written, TimeSpan.FromMilliseconds(1));
    }
}
