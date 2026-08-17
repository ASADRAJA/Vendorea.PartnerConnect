using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Persistence.Repositories;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// SprPriceRecordRepository.BulkInsertAsync used SqlBulkCopy, which is SQL Server only. It is now
/// Npgsql binary COPY.
/// </summary>
/// <remarks>
/// This sits on the SPR price ingest path, which is the heaviest thing PartnerConnect does. The
/// original comment explains why EF AddRange was rejected: change detection is O(n^2) over a
/// growing set and blew past App Service's 230s request limit. The replacement has to stay fast
/// as well as correct, so throughput is asserted loosely here to catch a regression to
/// row-by-row inserts.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class BulkCopyTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private int _uploadId;

    public BulkCopyTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync("SprPriceRecords", "PriceFeedUploads", "TradingPartners");

        await using var db = _pg.CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""TradingPartners"" (""Id"", ""Code"", ""Name"", ""PartnerType"", ""Status"", ""CreatedAt"")
              VALUES (1, 'SPR', 'S.P. Richards', 'Distributor', 'Active', now())");

        var upload = new PriceFeedUpload
        {
            DealerId = 1,
            TradingPartnerId = 1,
            FileName = "bulk-copy-test.csv",
            FileHash = "hash",
            FileSizeBytes = 1,
            RecordCount = 0,
            ErrorCount = 0,
            UploadedAt = DateTime.UtcNow
        };
        db.Set<PriceFeedUpload>().Add(upload);
        await db.SaveChangesAsync();
        _uploadId = upload.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private List<SprPriceRecord> Records(int count) =>
        Enumerable.Range(0, count).Select(i => new SprPriceRecord
        {
            PriceFeedUploadId = _uploadId,
            DealerId = 1,
            StockNumber = $"BULK{i:D7}",
            StockNumberStripped = $"BULK{i:D7}",
            ProductDescription = $"Bulk copy row {i}",
            ProductStatus = "A",
            SellingUnitOfMeasure = "EA",
            PackingQuantity1 = 1,
            WeightLbs = 1.5m,
            HeightInches = 2m,
            LengthInches = 3m,
            WidthInches = 4m
        }).ToList();

    [Fact]
    public async Task Copy_inserts_every_row_with_values_intact()
    {
        await using var db = _pg.CreateContext();
        var repo = new SprPriceRecordRepository(db);

        await repo.BulkInsertAsync(Records(1_000));

        var count = await db.Set<SprPriceRecord>().CountAsync(r => r.PriceFeedUploadId == _uploadId);
        count.Should().Be(1_000);

        // Spot-check that columns landed in the right order - a COPY column list out of step with
        // the property order would still insert 1,000 rows, just with scrambled values.
        var sample = await db.Set<SprPriceRecord>()
            .SingleAsync(r => r.StockNumber == "BULK0000500");
        sample.ProductDescription.Should().Be("Bulk copy row 500");
        sample.SellingUnitOfMeasure.Should().Be("EA");
        sample.WeightLbs.Should().Be(1.5m);
    }

    [Fact]
    public async Task Copy_handles_a_full_price_file_quickly()
    {
        await using var db = _pg.CreateContext();
        var repo = new SprPriceRecordRepository(db);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await repo.BulkInsertAsync(Records(95_000));   // a full SPR price file
        sw.Stop();

        var count = await db.Set<SprPriceRecord>().CountAsync(r => r.PriceFeedUploadId == _uploadId);
        count.Should().Be(95_000);

        // Deliberately generous - this guards against falling back to row-by-row inserts, not
        // against a few hundred milliseconds of drift. Locally this runs in ~1.2s.
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "a full price file must not approach the 230s App Service request limit");
    }

    [Fact]
    public async Task Copy_of_an_empty_set_is_a_no_op()
    {
        await using var db = _pg.CreateContext();
        var repo = new SprPriceRecordRepository(db);

        var act = async () => await repo.BulkInsertAsync(new List<SprPriceRecord>());

        await act.Should().NotThrowAsync();
    }
}
