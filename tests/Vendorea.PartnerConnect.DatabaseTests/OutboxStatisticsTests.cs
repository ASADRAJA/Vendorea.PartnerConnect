using FluentAssertions;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// OutboxRepository.GetStatisticsAsync used EF.Functions.DateDiffMillisecond, which is SQL Server
/// only.
/// </summary>
/// <remarks>
/// This one is worth a test because the obvious fix was wrong in a way that compiled. Replacing
/// it with plain DateTime subtraction and .TotalMilliseconds builds fine, and Npgsql then refuses
/// to translate it at runtime - so the method threw on every call while looking perfectly
/// healthy in review. PostgreSQL expresses the interval as EXTRACT(EPOCH FROM ...), which has no
/// LINQ form, so it goes through a raw scalar query.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class OutboxStatisticsTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;

    public OutboxStatisticsTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync() => _pg.TruncateAsync("OutboxMessages");

    public Task DisposeAsync() => Task.CompletedTask;

    private static OutboxMessage Message(OutboxMessageStatus status, TimeSpan? deliveryTime = null)
    {
        var created = DateTime.UtcNow.AddHours(-1);
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "test",
            Payload = "{}",
            Status = status,
            CreatedAt = created,
            DeliveredAt = deliveryTime is null ? null : created.Add(deliveryTime.Value)
        };
    }

    [Fact]
    public async Task Average_delivery_time_is_computed_from_the_interval()
    {
        await using (var seed = _pg.CreateContext())
        {
            seed.Set<OutboxMessage>().AddRange(
                Message(OutboxMessageStatus.Delivered, TimeSpan.FromMinutes(4)),
                Message(OutboxMessageStatus.Delivered, TimeSpan.FromMinutes(6)),
                Message(OutboxMessageStatus.Pending),
                Message(OutboxMessageStatus.Failed));
            await seed.SaveChangesAsync();
        }

        await using var db = _pg.CreateContext();
        var stats = await new OutboxRepository(db).GetStatisticsAsync();

        stats.AverageDeliveryTimeMs.Should().BeApproximately(300_000, 1_000, "4 and 6 minutes average to 5");
        stats.DeliveredLast24Hours.Should().Be(2);
        stats.PendingCount.Should().Be(1);
        stats.FailedCount.Should().Be(1);
    }

    [Fact]
    public async Task Average_delivery_time_is_zero_when_nothing_has_been_delivered()
    {
        await using (var seed = _pg.CreateContext())
        {
            seed.Set<OutboxMessage>().Add(Message(OutboxMessageStatus.Pending));
            await seed.SaveChangesAsync();
        }

        await using var db = _pg.CreateContext();
        var stats = await new OutboxRepository(db).GetStatisticsAsync();

        // The original relied on DefaultIfEmpty(0); the raw query uses COALESCE to the same end.
        stats.AverageDeliveryTimeMs.Should().Be(0);
    }
}
