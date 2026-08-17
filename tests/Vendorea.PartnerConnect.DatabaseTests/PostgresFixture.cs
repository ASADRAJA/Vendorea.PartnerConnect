using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Vendorea.PartnerConnect.Persistence;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// Spins up a real PostgreSQL container once per test run and applies the EF migrations to it.
/// </summary>
/// <remarks>
/// The existing suites mock every repository, so nothing exercised generated SQL. That was
/// tolerable against SQL Server, where the provider had been stable for years; it is not
/// tolerable while the data layer is being ported, because a query that fails to translate or
/// silently returns nothing looks identical to a passing test.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>
    /// Must match the major version of the target host. PostgreSQL 18 is the first release where
    /// non-deterministic (case-insensitive) collations support LIKE - the collation work in
    /// <see cref="CollationTests"/> depends on that, so testing against 18 while deploying to 16
    /// or 17 would give false confidence.
    /// </summary>
    public const string PostgresImage = "postgres:18-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage(PostgresImage)
        .WithDatabase("partnerconnect_test")
        .WithUsername("pc")
        .WithPassword("pc")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public PartnerConnectDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PartnerConnectDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new PartnerConnectDbContext(options);
    }

    /// <summary>
    /// Empties the tables a test touches. Cheaper and less brittle than recreating the schema,
    /// and TRUNCATE ... CASCADE handles the FK ordering for us.
    /// </summary>
    public async Task TruncateAsync(params string[] tables)
    {
        if (tables.Length == 0) return;

        await using var db = CreateContext();
        var list = string.Join(", ", tables.Select(t => t.Contains('.') ? t : $"\"{t}\""));
        await db.Database.ExecuteSqlRawAsync($"TRUNCATE {list} RESTART IDENTITY CASCADE");
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
