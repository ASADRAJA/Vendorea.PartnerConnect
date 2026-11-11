using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Services;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// That the retention handlers are actually reachable.
/// </summary>
/// <remarks>
/// A handler is bound to its schedule by <c>JobKey</c>, and a handler whose row does not exist
/// never runs — no error, no log, just a retention policy that silently is not applied. That is
/// how <c>RawDocumentArchive</c> became a table nothing ever wrote to, so the wiring is asserted
/// rather than assumed.
///
/// Deliberately reads the migrated schema rather than seeding its own rows: the thing under test
/// is the migration.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class RetentionJobWiringTests
{
    private readonly PostgresFixture _pg;

    public RetentionJobWiringTests(PostgresFixture pg) => _pg = pg;

    [Theory]
    [InlineData(IntegrationOAuthStatePurgeJobHandler.Key)]
    [InlineData(IntegrationPayloadArchivePurgeJobHandler.Key)]
    [InlineData(BuyerDataRetentionJobHandler.Key)]
    public async Task Every_retention_handler_has_a_schedule_bound_to_it(string jobKey)
    {
        await using var db = _pg.CreateContext();

        var job = await db.ScheduledJobs.AsNoTracking().SingleOrDefaultAsync(j => j.JobKey == jobKey);

        job.Should().NotBeNull($"handler '{jobKey}' is registered but would never run without a row");
        job!.IsEnabled.Should().BeTrue();
        job.CronExpression.Should().NotBeNullOrWhiteSpace();
        job.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_retention_schedules_do_not_all_fire_at_once()
    {
        await using var db = _pg.CreateContext();

        var crons = await db.ScheduledJobs.AsNoTracking()
            .Where(j => j.JobKey == IntegrationOAuthStatePurgeJobHandler.Key
                        || j.JobKey == IntegrationPayloadArchivePurgeJobHandler.Key
                        || j.JobKey == BuyerDataRetentionJobHandler.Key)
            .Select(j => j.CronExpression)
            .ToListAsync();

        // Staggered on purpose. Three sweeps starting together on a database shared with
        // Merchant360 is a self-inflicted load spike.
        crons.Should().HaveCount(3);
        crons.Distinct().Should().HaveCount(3);
    }
}
