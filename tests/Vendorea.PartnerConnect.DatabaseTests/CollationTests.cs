using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// SQL Server ran under SQL_Latin1_General_CP1_CI_AS, so every string comparison was
/// case-insensitive. PostgreSQL is case-sensitive by default and will not take a
/// non-deterministic collation as the database default, so the behaviour is restored per column
/// with citext.
/// </summary>
/// <remarks>
/// citext rather than an ICU non-deterministic collation: collations only gained LIKE support in
/// PostgreSQL 18, and citext works on every release, so the design does not depend on a version
/// we might not control at every stage.
///
/// Two things break without this, and neither throws. A login lookup stops matching on different
/// casing, and a unique index stops rejecting case-variant duplicates - so you get two
/// administrator accounts differing only by case. Only a test catches either.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class CollationTests : IAsyncLifetime
{
    private const string Username = "pcadmin";

    private readonly PostgresFixture _pg;

    public CollationTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync("AdminPortalUsers");

        await using var db = _pg.CreateContext();
        db.AdminPortalUsers.Add(new AdminPortalUser
        {
            Username = Username,
            DisplayName = "Portal Administrator",
            Role = AdminPortalRole.Admin,
            IsActive = true,
            PasswordHash = "not-a-real-hash"
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Citext_extension_is_installed()
    {
        await using var db = _pg.CreateContext();

        var installed = await db.Database.SqlQueryRaw<long>(
            @"SELECT count(*)::bigint AS ""Value"" FROM pg_extension WHERE extname = 'citext'").FirstAsync();

        installed.Should().Be(1, "the migration must create the extension before any citext column");
    }

    [Theory]
    // Identifiers a person types, or that key a unique index, and so relied on the old
    // case-insensitive collation.
    [InlineData("AdminPortalUsers", "Username")]
    [InlineData("OrgPortalUsers", "Email")]
    [InlineData("ExternalDealers", "Email")]
    [InlineData("Organizations", "Code")]
    [InlineData("TradingPartners", "Code")]
    [InlineData("Tenants", "Code")]
    [InlineData("Tenants", "ExternalId")]
    [InlineData("TenantPartnerAccounts", "AccountNumber")]
    [InlineData("Roles", "Code")]
    [InlineData("SprPriceRecords", "StockNumber")]
    [InlineData("SprPriceRecords", "StockNumberStripped")]
    public async Task Identity_columns_are_citext(string table, string column)
    {
        await using var db = _pg.CreateContext();

        var type = await db.Database.SqlQueryRaw<string>(
            $@"SELECT udt_name AS ""Value"" FROM information_schema.columns
               WHERE table_name = '{table}' AND column_name = '{column}'").FirstAsync();

        type.Should().Be("citext");
    }

    [Fact]
    public async Task Key_hash_is_deliberately_left_case_sensitive()
    {
        // ApiKey.KeyHash was case-insensitive on SQL Server only as a side effect of the database
        // collation. It is a hash compared for exact equality; making it case-insensitive would be
        // propagating an accident, not preserving intent.
        await using var db = _pg.CreateContext();

        var type = await db.Database.SqlQueryRaw<string>(
            @"SELECT udt_name AS ""Value"" FROM information_schema.columns
              WHERE table_name = 'ApiKeys' AND column_name = 'KeyHash'").FirstAsync();

        type.Should().NotBe("citext");
    }

    [Theory]
    [InlineData("pcadmin")]
    [InlineData("PCAdmin")]
    [InlineData("PCADMIN")]
    [InlineData("pCaDmIn")]
    public async Task Lookup_matches_regardless_of_case(string attempted)
    {
        await using var db = _pg.CreateContext();

        var found = await db.AdminPortalUsers.CountAsync(u => u.Username == attempted);

        found.Should().Be(1, "SQL Server matched '{0}' and the port must not change that", attempted);
    }

    [Fact]
    public async Task Like_matches_regardless_of_case()
    {
        await using var db = _pg.CreateContext();

        var found = await db.AdminPortalUsers.CountAsync(u => u.Username.Contains("ADMIN"));

        found.Should().Be(1, "citext supports LIKE, which is why it was chosen over a collation");
    }

    [Fact]
    public async Task Unique_index_rejects_a_case_variant_duplicate()
    {
        await using var db = _pg.CreateContext();
        db.AdminPortalUsers.Add(new AdminPortalUser
        {
            Username = "PCADMIN",
            DisplayName = "case-variant duplicate",
            Role = AdminPortalRole.Admin,
            IsActive = true,
            PasswordHash = "not-a-real-hash"
        });

        var insert = async () => await db.SaveChangesAsync();

        await insert.Should().ThrowAsync<DbUpdateException>(
            "'PCADMIN' and 'pcadmin' are the same account; allowing both would create a second "
            + "administrator that the unique index exists to prevent");
    }
}
