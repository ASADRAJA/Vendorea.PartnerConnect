using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// Asserts the shape the migrations actually produce, rather than the shape the model intends.
/// The two drifted apart on SQL Server - four migrations were invisible to EF, and a column
/// default that lived only in a migration vanished when the chain was rebaselined - so the
/// schema is worth checking directly.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SchemaTests
{
    private readonly PostgresFixture _pg;

    public SchemaTests(PostgresFixture pg) => _pg = pg;

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var db = _pg.CreateContext();
        return await db.Database.SqlQueryRaw<T>(sql).FirstAsync();
    }

    [Fact]
    public async Task Migrations_apply_and_create_the_expected_schema()
    {
        var tables = await ScalarAsync<long>(
            @"SELECT count(*)::bigint AS ""Value"" FROM information_schema.tables
              WHERE table_schema NOT IN ('pg_catalog', 'information_schema')");

        // Sanity floor rather than an exact count, so adding a table does not fail the build.
        tables.Should().BeGreaterThan(90, "the full PartnerConnect schema should have been created");
    }

    [Fact]
    public async Task Raw_spr_schema_exists_for_the_csv_import_target()
    {
        var sprTables = await ScalarAsync<long>(
            @"SELECT count(*)::bigint AS ""Value"" FROM information_schema.tables WHERE table_schema = 'spr'");

        sprTables.Should().BeGreaterThan(20, "SprCsvBulkImportService COPYs directly into spr.*");
    }

    [Fact]
    public async Task Filtered_indexes_survive_as_partial_indexes()
    {
        // SQL Server HasFilter maps 1:1 to a PostgreSQL partial index. If the bracket-quoted
        // predicates had not been converted these would silently be full indexes instead,
        // losing the uniqueness guarantees they exist to provide.
        var partial = await ScalarAsync<long>(
            @"SELECT count(*)::bigint AS ""Value"" FROM pg_indexes
              WHERE schemaname = 'public' AND indexdef LIKE '%WHERE%'");

        partial.Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task Push_progress_columns_keep_their_defaults()
    {
        // These carried DEFAULT ((0)) on SQL Server only because a migration passed
        // defaultValue: 0 to AddColumn - a backfill value, not part of the model. Rebaselining
        // dropped them and SprRawToCanonicalTransformService started failing on a NOT NULL
        // violation. Declared in the model now; this is the regression guard.
        foreach (var column in new[]
                 {
                     "M360PushTotalProducts", "M360PushProductsPushed",
                     "M360PushCurrentBatch", "M360PushTotalBatches"
                 })
        {
            var hasDefault = await ScalarAsync<long>(
                $@"SELECT count(*)::bigint AS ""Value"" FROM information_schema.columns
                   WHERE table_name = 'SprContentUploads' AND column_name = '{column}'
                     AND column_default IS NOT NULL");

            hasDefault.Should().Be(1, "{0} must keep its database default", column);
        }
    }
}
