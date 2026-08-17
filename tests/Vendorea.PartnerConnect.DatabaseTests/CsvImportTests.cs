using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;
using Vendorea.PartnerConnect.WorkerProcesses.Services;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// SprCsvBulkImportService streamed SPR's CSVs into the raw spr.* tables with SqlBulkCopy. It now
/// streams into PostgreSQL text-format COPY, feeding the unchanged SprCsvDataReader.
/// </summary>
/// <remarks>
/// The parsing is deliberately untouched - the reader already hands back strings and every spr.*
/// column is text, so there is no conversion at either end. What did change is the null and
/// quoting semantics, which now come from CSV mode rather than from SqlBulkCopy's handling of
/// DBNull. Those are asserted explicitly below, because getting them subtly wrong would corrupt
/// the catalogue rather than fail loudly.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class CsvImportTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private string _csvPath = string.Empty;

    public CsvImportTests(PostgresFixture pg) => _pg = pg;

    public Task InitializeAsync() => _pg.TruncateAsync("spr.productattribute");

    public Task DisposeAsync()
    {
        if (File.Exists(_csvPath)) File.Delete(_csvPath);
        return Task.CompletedTask;
    }

    private SprCsvBulkImportService CreateService(PartnerConnectDbContextAccessor accessor) =>
        new(NullLogger<SprCsvBulkImportService>.Instance,
            accessor.Context,
            Options.Create(new SprContentIngestionOptions()),
            storage: null!);   // ImportCsvFileAsync does not touch storage

    private string WriteCsv(int rows)
    {
        _csvPath = Path.Combine(Path.GetTempPath(), $"spr_import_{Guid.NewGuid():N}.csv");
        using var w = new StreamWriter(_csvPath);
        for (var i = 0; i < rows; i++)
        {
            // productid,attributeid,categoryid,displayvalue,absolutevalue,unitid,isabsolute,isactive,localeid
            var display = i % 100 == 0 ? "\"12 in, wide\"" : $"Value {i}";      // embedded delimiter
            var absolute = i % 150 == 0 ? "\"say \"\"hi\"\"\"" : $"abs{i}";      // escaped quotes
            var unit = i % 7 == 0 ? "" : "u1";                                   // empty -> NULL
            w.WriteLine($"P{i},A{i % 50},C{i % 10},{display},{absolute},{unit},1,1,1");
        }
        return _csvPath;
    }

    [Fact]
    public async Task Import_lands_every_row_and_preserves_csv_semantics()
    {
        const int rows = 10_000;
        var csv = WriteCsv(rows);

        await using var db = _pg.CreateContext();
        var service = CreateService(new PartnerConnectDbContextAccessor(db));

        var result = await service.ImportCsvFileAsync(csv, "productattribute");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.RowsInserted.Should().Be(rows);

        async Task<long> Count(string where) => await db.Database
            .SqlQueryRaw<long>($"SELECT count(*)::bigint AS \"Value\" FROM spr.productattribute WHERE {where}")
            .FirstAsync();

        (await Count("true")).Should().Be(rows);

        // An unquoted empty field is NULL in CSV mode; a quoted one is an empty string. That
        // mirrors how SqlBulkCopy treated the reader's DBNull.
        (await Count("unitid IS NULL")).Should().Be(rows / 7 + 1, "empty fields must arrive as NULL");

        (await Count("displayvalue = '12 in, wide'")).Should().Be(rows / 100,
            "a quoted field containing the delimiter must survive intact");

        (await Count("absolutevalue = 'say \"hi\"'")).Should().Be((rows + 149) / 150,
            "doubled quotes must be unescaped to a single quote");
    }

    [Fact]
    public async Task Import_rejects_an_unknown_target_table()
    {
        var csv = WriteCsv(1);

        await using var db = _pg.CreateContext();
        var service = CreateService(new PartnerConnectDbContextAccessor(db));

        var result = await service.ImportCsvFileAsync(csv, "not_a_real_table");

        result.Success.Should().BeFalse("the table whitelist is what keeps the COPY statement injection-free");
    }
}

/// <summary>Tiny holder so the fixture's context can be handed to a service that expects one.</summary>
public sealed class PartnerConnectDbContextAccessor
{
    public PartnerConnectDbContextAccessor(Persistence.PartnerConnectDbContext context) => Context = context;
    public Persistence.PartnerConnectDbContext Context { get; }
}
