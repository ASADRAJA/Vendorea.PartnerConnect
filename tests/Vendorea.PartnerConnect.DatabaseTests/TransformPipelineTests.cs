using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;
using Vendorea.PartnerConnect.WorkerProcesses.Services;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// SprRawToCanonicalTransformService is ~15 hand-written SQL statements that turn the raw spr.*
/// tables into the canonical schema. Almost every one needed rewriting for PostgreSQL.
/// </summary>
/// <remarks>
/// The mechanical parts (GETUTCDATE, ISNULL, LEN, NVARCHAR, string +) would fail loudly. These
/// tests exist for the parts that would not: TOP 1 becoming LIMIT 1 inside correlated subqueries,
/// T-SQL UPDATE..FROM..alias, WITH RECURSIVE, OUTPUT INSERTED becoming RETURNING, STRING_AGG's
/// WITHIN GROUP ordering moving inside the call, and TRY_CAST - which has no PostgreSQL
/// equivalent at all and is emulated with a regex-guarded CASE at three sites.
///
/// The seed below deliberately includes non-numeric values in columns the original TRY_CASTs
/// guarded, so a naive cast would raise instead of yielding NULL.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class TransformPipelineTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;

    public TransformPipelineTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync(
            "SprProductSpecifications", "SprProductFeatures", "SprProductRelationships",
            "SprProductContent", "SprCategories", "SprContentUploads", "TradingPartners",
            "spr.product", "spr.productskus", "spr.productdescriptions", "spr.productkeywords",
            "spr.productimages", "spr.productfeatures", "spr.productaccessories",
            "spr.productsimilar", "spr.productupsell", "spr.manufacturer", "spr.categorynames",
            "spr.attributenames", "spr.categorydisplayattributes", "spr.productattribute",
            "spr.mapped_category", "spr.mapped_category_names", "spr.mapped_category_taxonomy");

        await using var db = _pg.CreateContext();
        await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO ""TradingPartners"" (""Id"",""Code"",""Name"",""PartnerType"",""Status"",""CreatedAt"")
  VALUES (1,'SPR','S.P. Richards','Distributor','Active', now());

INSERT INTO spr.mapped_category_taxonomy (categoryid, parentcategoryid) VALUES ('10',NULL),('11','10'),('12','11');
INSERT INTO spr.mapped_category_names (categoryid, localeid, name) VALUES ('10','1','Office'),('11','1','Paper'),('12','1','Copy Paper');
INSERT INTO spr.mapped_category (productid, categoryid) VALUES ('P1','12'),('P2','11');
INSERT INTO spr.manufacturer (manufacturerid, name, country) VALUES ('M1','Acme Corp','US');
INSERT INTO spr.product (productid, manufacturerid, isactive, mfgpartno, categoryid, isaccessory) VALUES
  ('P1','M1','1','MPN-1','12','0'), ('P2','M1','1','MPN-2','11','0'), ('P3','M1','1','MPN-3','11','1');
INSERT INTO spr.productskus (productid, name, sku, localeid) VALUES
  ('P1','SP Richards','SPR1001','1'), ('P1','UPC','000111222333','1'), ('P1','UNSPSC','44121506','1');
INSERT INTO spr.productdescriptions (productid, description, type, localeid) VALUES
  ('P1','Premium copy paper','0','1'), ('P1','Extra detail','3','1'),
  ('P1','Marketing blurb','25','1'), ('P1','non-numeric type','abc','1');
INSERT INTO spr.productkeywords (productid, keywords, localeid) VALUES ('P1','paper,copy','1');
INSERT INTO spr.categorynames (categoryid, name, localeid) VALUES ('12','Copy Paper','1'),('11','Paper','1');
INSERT INTO spr.productimages (productid, type, status) VALUES ('P1','225','A'),('P1','75','A');
INSERT INTO spr.productfeatures (productid, localeid, sequenceno, bullettext) VALUES
  ('P1','1','1','Bright white'), ('P1','1','not-a-number','Acid free');
INSERT INTO spr.productaccessories (productid, accessoryproductid, isactive, isoption, note) VALUES ('P1','P2','1','1','opt');
INSERT INTO spr.productsimilar (productid, similarproductid, localeid) VALUES ('P1','P2','1');
INSERT INTO spr.productupsell (productid, upsellproductid, localeid) VALUES ('P1','P2','1');
INSERT INTO spr.attributenames (attributeid, name, localeid) VALUES ('A1','Colour','1'),('A2','Weight','1');
INSERT INTO spr.categorydisplayattributes (headerid, categoryid, attributeid, isactive, templatetype, defaultdisplayorder, displayorder, lastupdated)
  VALUES ('H1','12','A1','1','0','2','1','x'), ('H1','12','A2','1','0','oops','2','x');
INSERT INTO spr.productattribute (productid, attributeid, categoryid, displayvalue, absolutevalue, unitid, isabsolute, isactive, localeid) VALUES
  ('P1','A1','12','White <b>bright</b>','w','u','0','1','1'),
  ('P1','A2','12','20 lb','20','u','1','1','1');");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private SprRawToCanonicalTransformService CreateService(Persistence.PartnerConnectDbContext db) =>
        new(NullLogger<SprRawToCanonicalTransformService>.Instance, db,
            Options.Create(new SprContentIngestionOptions()));

    [Fact]
    public async Task Categories_build_their_hierarchy_through_the_recursive_cte()
    {
        await using var db = _pg.CreateContext();

        var count = await CreateService(db).TransformCategoriesAsync();

        count.Should().Be(3);

        // T-SQL UPDATE..FROM..alias and WITH RECURSIVE both had to be restructured; if either
        // were wrong the rows would exist but Level and FullPath would stay at their first-pass
        // values of 0 and the bare category code.
        var deepest = await db.Database.SqlQueryRaw<string>(
            @"SELECT ""FullPath"" AS ""Value"" FROM ""SprCategories"" ORDER BY ""Level"" DESC LIMIT 1").FirstAsync();
        deepest.Should().Be("10/11/12");

        var nested = await db.Database.SqlQueryRaw<long>(
            @"SELECT count(*)::bigint AS ""Value"" FROM ""SprCategories"" WHERE ""Level"" > 0").FirstAsync();
        nested.Should().Be(2);
    }

    [Fact]
    public async Task Products_resolve_skus_descriptions_and_images()
    {
        await using var db = _pg.CreateContext();
        var service = CreateService(db);
        await service.TransformCategoriesAsync();

        var count = await service.TransformProductsAsync();

        count.Should().Be(2, "P3 is an accessory and must be excluded");

        async Task<string> Field(string column, string productId) => await db.Database.SqlQueryRaw<string>(
            $@"SELECT COALESCE(""{column}"", '<null>') AS ""Value"" FROM ""SprProductContent"" WHERE ""ProductId"" = '{productId}'")
            .FirstAsync();

        // Ten correlated "SELECT TOP 1 ..." subqueries became "... LIMIT 1"; the token moves to
        // the end of each subquery rather than being replaced in place.
        (await Field("Sku", "P1")).Should().Be("SPR1001");
        (await Field("Sku", "P2")).Should().Be("MPN-2", "falls back to mfgpartno when SPR has no stock number");

        // TRY_CAST guard: type '25' is in range, and the row with type 'abc' must not raise.
        (await Field("MarketingText", "P1")).Should().Be("Marketing blurb");

        // T-SQL string concatenation with + became ||
        (await Field("ImageUrl225", "P1")).Should().Be("https://content.etilize.com/225/P1.jpg");
    }

    [Fact]
    public async Task Content_upload_row_is_returned_by_returning_clause()
    {
        await using var db = _pg.CreateContext();
        var service = CreateService(db);
        await service.TransformCategoriesAsync();
        await service.TransformProductsAsync();

        // OUTPUT INSERTED.Id became RETURNING "Id" AS "Value" - aliased because EF's
        // SqlQueryRaw<int> binds scalars from a column of that name. If it were wrong the
        // service would log "Failed to create content upload record" and return 0 products.
        var version = await db.Database.SqlQueryRaw<string>(
            @"SELECT ""ContentVersion"" AS ""Value"" FROM ""SprContentUploads"" LIMIT 1").FirstAsync();

        version.Should().MatchRegex(@"^\d{4}\.\d{2}\.\d{2}$", "FORMAT(...,'yyyy.MM.dd') became to_char(...,'YYYY.MM.DD')");
    }

    [Fact]
    public async Task Features_fall_back_to_row_number_when_sequence_is_not_numeric()
    {
        await using var db = _pg.CreateContext();
        var service = CreateService(db);
        await service.TransformCategoriesAsync();
        await service.TransformProductsAsync();

        var count = await service.TransformFeaturesAsync();

        count.Should().Be(2, "the row with sequenceno 'not-a-number' must still import via the ROW_NUMBER fallback");
    }

    [Fact]
    public async Task Relationships_map_booleans_correctly()
    {
        await using var db = _pg.CreateContext();
        var service = CreateService(db);
        await service.TransformCategoriesAsync();
        await service.TransformProductsAsync();

        var count = await service.TransformRelationshipsAsync();

        count.Should().Be(3);

        // T-SQL wrote 0/1 into a bit column; PostgreSQL needs true/false against boolean.
        var bidirectional = await db.Database.SqlQueryRaw<long>(
            @"SELECT count(*)::bigint AS ""Value"" FROM ""SprProductRelationships"" WHERE ""IsBidirectional""").FirstAsync();
        bidirectional.Should().Be(1, "only Similar is bidirectional; Accessory and Upsell are not");
    }

    [Fact]
    public async Task Specifications_html_is_ordered_and_escaped_as_before()
    {
        await using var db = _pg.CreateContext();
        var service = CreateService(db);
        await service.TransformCategoriesAsync();
        await service.TransformProductsAsync();

        var count = await service.TransformSpecificationsAsync();

        count.Should().Be(1);

        var html = await db.Database.SqlQueryRaw<string>(
            @"SELECT ""SpecificationsHtml"" AS ""Value"" FROM ""SprProductSpecifications"" LIMIT 1").FirstAsync();

        // STRING_AGG(...) WITHIN GROUP (ORDER BY ...) became string_agg(..., '' ORDER BY ...).
        // Colour has display order '2'; Weight has 'oops', which the TRY_CAST guard sends to
        // 9999 so it sorts last - exactly as SQL Server's ISNULL(TRY_CAST(...), 9999) did.
        html.IndexOf("Colour", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Weight", StringComparison.Ordinal));

        html.Should().Contain("&lt;b&gt;", "REPLACE-based HTML escaping must still apply");
        html.Should().StartWith("<table class=\"specs\">");
    }
}
