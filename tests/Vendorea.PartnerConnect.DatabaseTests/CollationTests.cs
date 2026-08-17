using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// SQL Server ran under SQL_Latin1_General_CP1_CI_AS, so string comparison was case-insensitive
/// everywhere. PostgreSQL is case-sensitive by default and refuses a non-deterministic collation
/// as the database default, so the behaviour has to be restored per column.
/// </summary>
/// <remarks>
/// Without this, two things break silently: a login lookup stops matching on different casing,
/// and - worse - a unique index stops rejecting case-variant duplicates, so you get two admin
/// accounts differing only by case. Neither throws, so only a test catches it.
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
    public async Task Collation_is_non_deterministic_on_the_username_column()
    {
        await using var db = _pg.CreateContext();

        var collation = await db.Database.SqlQueryRaw<string>(
            @"SELECT collation_name AS ""Value"" FROM information_schema.columns
              WHERE table_name = 'AdminPortalUsers' AND column_name = 'Username'").FirstAsync();

        collation.Should().Be("ci");

        var deterministic = await db.Database.SqlQueryRaw<bool>(
            @"SELECT collisdeterministic AS ""Value"" FROM pg_collation WHERE collname = 'ci'").FirstAsync();

        deterministic.Should().BeFalse("a deterministic collation would still compare case-sensitively");
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
        // PostgreSQL 18 is the first release to support LIKE against a non-deterministic
        // collation; on 16 or 17 this throws and the design has to switch to citext.
        await using var db = _pg.CreateContext();

        var found = await db.AdminPortalUsers.CountAsync(u => u.Username.Contains("ADMIN"));

        found.Should().Be(1);
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
