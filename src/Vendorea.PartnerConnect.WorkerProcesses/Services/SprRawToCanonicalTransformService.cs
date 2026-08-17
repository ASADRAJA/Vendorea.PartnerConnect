using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Persistence;
using Vendorea.PartnerConnect.WorkerProcesses.Configuration;

namespace Vendorea.PartnerConnect.WorkerProcesses.Services;

/// <summary>
/// Service for transforming SPR raw schema data to canonical schema.
/// Uses raw SQL for efficient bulk operations.
/// </summary>
public class SprRawToCanonicalTransformService : ISprRawToCanonicalTransformService
{
    private readonly ILogger<SprRawToCanonicalTransformService> _logger;
    private readonly PartnerConnectDbContext _dbContext;
    private readonly SprContentIngestionOptions _options;

    // Default locale ID for EN_US (string for raw table comparisons)
    private const string DefaultLocaleId = "1";

    public SprRawToCanonicalTransformService(
        ILogger<SprRawToCanonicalTransformService> logger,
        PartnerConnectDbContext dbContext,
        IOptions<SprContentIngestionOptions> options)
    {
        _logger = logger;
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<TransformResult> TransformAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new TransformResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting raw-to-canonical transformation...");

            // Transform in order of dependencies
            result.CategoriesTransformed = await TransformCategoriesAsync(cancellationToken);
            result.ProductsTransformed = await TransformProductsAsync(cancellationToken);
            result.FeaturesTransformed = await TransformFeaturesAsync(cancellationToken);
            result.RelationshipsTransformed = await TransformRelationshipsAsync(cancellationToken);

            // Specifications transformation is optional (slow operation)
            if (_options.TransformSpecifications)
            {
                result.SpecificationsTransformed = await TransformSpecificationsAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Skipping specifications transformation (disabled in config)");
                result.SpecificationsTransformed = 0;
            }

            result.Success = true;
            _logger.LogInformation(
                "Transformation completed: {Products} products, {Categories} categories, " +
                "{Features} features, {Relationships} relationships, {Specs} specifications",
                result.ProductsTransformed, result.CategoriesTransformed,
                result.FeaturesTransformed, result.RelationshipsTransformed,
                result.SpecificationsTransformed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transformation failed");
            result.Success = false;
            result.Errors.Add(ex.Message);
        }

        result.CompletedAt = DateTime.UtcNow;
        return result;
    }

    public async Task<int> TransformCategoriesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transforming categories...");

        // Set longer timeout for bulk cleanup operations (5 minutes)
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        // Clear tables in FK order: first clear product category references, then categories
        // Set SprCategoryId to NULL on products (if any exist)
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE \"SprProductContent\" SET \"SprCategoryId\" = NULL WHERE \"SprCategoryId\" IS NOT NULL", cancellationToken);

        // Clear self-referential FK on categories
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE \"SprCategories\" SET \"ParentCategoryId\" = NULL WHERE \"ParentCategoryId\" IS NOT NULL", cancellationToken);

        // Now we can delete all categories
        await _dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"SprCategories\"", cancellationToken);

        // First pass: insert without parent references
        var insertSql = @"
            INSERT INTO ""SprCategories"" (
                ""CategoryCode"", ""CategoryName"", ""Level"", ""UnspscCode"",
                ""FullPath"", ""IsActive"", ""CreatedAt"", ""UpdatedAt""
            )
            SELECT
                CAST(mct.categoryid AS VARCHAR(50)) AS ""CategoryCode"",
                COALESCE(mcn.name, 'Category ' || CAST(mct.categoryid AS VARCHAR(20))) AS ""CategoryName"",
                0 AS ""Level"",
                NULL AS ""UnspscCode"",
                CAST(mct.categoryid AS VARCHAR(500)) AS ""FullPath"",
                true AS ""IsActive"",
                now() AS ""CreatedAt"",
                now() AS ""UpdatedAt""
            FROM spr.mapped_category_taxonomy mct
            LEFT JOIN spr.mapped_category_names mcn
                ON mcn.categoryid = mct.categoryid AND mcn.localeid = @p0";

        var count = await _dbContext.Database.ExecuteSqlRawAsync(
            insertSql, new object[] { DefaultLocaleId }, cancellationToken);

        // Second pass: update parent references
        var updateParentsSql = @"
            -- PostgreSQL UPDATE..FROM names the target in UPDATE (not FROM), does not
            -- alias-qualify SET columns, and joins the target via WHERE.
            UPDATE ""SprCategories"" c
            SET ""ParentCategoryId"" = p.""Id""
            FROM spr.mapped_category_taxonomy mct
            INNER JOIN ""SprCategories"" p
                ON CAST(mct.parentcategoryid AS VARCHAR(50)) = p.""CategoryCode""
            WHERE CAST(mct.categoryid AS VARCHAR(50)) = c.""CategoryCode""
              AND mct.parentcategoryid IS NOT NULL";

        await _dbContext.Database.ExecuteSqlRawAsync(updateParentsSql, cancellationToken);

        // Third pass: compute Level and FullPath using recursive CTE
        var computeHierarchySql = @"
            -- PostgreSQL requires RECURSIVE to be declared explicitly for self-referencing CTEs.
            WITH RECURSIVE ""CategoryHierarchy"" AS (
                -- Root categories (no parent)
                SELECT
                    ""Id"",
                    ""CategoryCode"",
                    0 AS ""ComputedLevel"",
                    CAST(""CategoryCode"" AS VARCHAR(500)) AS ""ComputedPath""
                FROM ""SprCategories""
                WHERE ""ParentCategoryId"" IS NULL

                UNION ALL

                -- Child categories
                SELECT
                    c.""Id"",
                    c.""CategoryCode"",
                    ch.""ComputedLevel"" + 1,
                    CAST(ch.""ComputedPath"" || '/' || c.""CategoryCode"" AS VARCHAR(500))
                FROM ""SprCategories"" c
                INNER JOIN ""CategoryHierarchy"" ch ON c.""ParentCategoryId"" = ch.""Id""
            )
            UPDATE ""SprCategories"" c
            SET ""Level"" = ch.""ComputedLevel"",
                ""FullPath"" = ch.""ComputedPath""
            FROM ""CategoryHierarchy"" ch
            WHERE c.""Id"" = ch.""Id""";

        await _dbContext.Database.ExecuteSqlRawAsync(computeHierarchySql, cancellationToken);

        _logger.LogInformation("Transformed {Count} categories with hierarchy levels computed", count);
        return count;
    }

    public async Task<int> TransformProductsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transforming products...");

        // Truncate canonical products table (child tables first due to FK)
        await _dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"SprProductSpecifications\"", cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"SprProductFeatures\"", cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"SprProductRelationships\"", cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"SprProductContent\"", cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"SprContentUploads\" WHERE \"CorrelationId\" LIKE 'FTP-IMPORT-%'", cancellationToken);

        // Create a content upload record for this FTP import
        var correlationId = $"FTP-IMPORT-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var productCount = await _dbContext.Database.ExecuteSqlRawAsync(@"
            SELECT COUNT(*) FROM spr.product WHERE isaccessory = '0' AND isactive = '1'", cancellationToken);

        // Get or create SPR trading partner (ID = 1 assumed for SPR)
        var createUploadSql = @"
            INSERT INTO ""SprContentUploads"" (
                ""TradingPartnerId"", ""ContentVersion"", ""LocaleId"", ""ZipFileName"", ""ZipFileSizeBytes"",
                ""Status"", ""TotalProducts"", ""ProcessedProducts"", ""NewProducts"", ""UpdatedProducts"",
                ""SkippedProducts"", ""ErrorProducts"", ""CorrelationId"", ""UploadedAt"", ""ProcessingStartedAt""
            )
            VALUES (
                (SELECT ""Id"" FROM ""TradingPartners"" WHERE ""Code"" = 'SPR' LIMIT 1),
                to_char(now(), 'YYYY.MM.DD'),
                @p0,
                'FTP-BULK-IMPORT',
                0,
                'Processing',
                0, 0, 0, 0, 0, 0,
                @p1,
                now(),
                now()
            )
            -- OUTPUT INSERTED.Id -> RETURNING. Aliased to ""Value"" because EF's SqlQueryRaw<int>
            -- binds scalar results from a column of that name.
            RETURNING ""Id"" AS ""Value""";

        // Get the upload ID
        var uploadIdResult = await _dbContext.Database.SqlQueryRaw<int>(
            createUploadSql, _options.Locale, correlationId).ToListAsync(cancellationToken);
        var uploadId = uploadIdResult.FirstOrDefault();

        if (uploadId == 0)
        {
            _logger.LogError("Failed to create content upload record. Ensure SPR trading partner exists.");
            return 0;
        }

        _logger.LogInformation("Created content upload record with ID {UploadId}", uploadId);

        // Transform products with descriptions and manufacturer info
        var sql = $@"
            INSERT INTO ""SprProductContent"" (
                ""ContentUploadId"", ""ProductId"", ""LocaleId"", ""Sku"", ""StockNumberStripped"", ""Upc"", ""BrandName"",
                ""ProductType"", ""ProductLine"", ""ProductSeries"",
                ""Description1"", ""Description2"", ""Description3"", ""MarketingText"",
                ""ManufacturerId"", ""ManufacturerName"", ""CountryOfOrigin"", ""UnspscCode"",
                ""RecycledPercent"", ""SubClassName"", ""SubClassNumber"", ""ClassName"", ""ClassNumber"",
                ""DepartmentName"", ""DepartmentNumber"", ""MasterDepartmentName"", ""MasterDepartmentNumber"",
                ""SprCategoryId"", ""ImageUrl225"", ""ImageUrl75"", ""Keywords"",
                ""ContentVersionDate"", ""CreatedAt"", ""UpdatedAt""
            )
            SELECT
                {uploadId} AS ""ContentUploadId"",
                CAST(p.productid AS VARCHAR(50)) AS ""ProductId"",
                @p0 AS ""LocaleId"",
                COALESCE(
                    (SELECT sku FROM spr.productskus
                     WHERE productid = p.productid AND name = 'SP Richards' LIMIT 1),
                    p.mfgpartno
                ) AS ""Sku"",
                -- Deliberately NOT coalesced: this stays null when SPR publishes no stock number for
                -- the product, which is what lets the M360 join exclude the mfgpartno fallback rows
                -- structurally rather than by inference. No stripping needed - genuine SPR stock
                -- numbers carry no punctuation.
                (SELECT sku FROM spr.productskus
                 WHERE productid = p.productid AND name = 'SP Richards' LIMIT 1) AS ""StockNumberStripped"",
                (SELECT sku FROM spr.productskus
                 WHERE productid = p.productid AND name = 'UPC' LIMIT 1) AS ""Upc"",
                COALESCE(m.name, 'Unknown') AS ""BrandName"",
                NULL AS ""ProductType"",
                NULL AS ""ProductLine"",
                NULL AS ""ProductSeries"",
                -- Type 0 = Manual (SPR), Type 2 = Main Title, Type 1 = Full
                COALESCE(
                    (SELECT description FROM spr.productdescriptions
                     WHERE productid = p.productid AND type = '0' AND localeid = @p1 LIMIT 1),
                    (SELECT description FROM spr.productdescriptions
                     WHERE productid = p.productid AND type = '2' AND localeid = @p1 LIMIT 1),
                    (SELECT description FROM spr.productdescriptions
                     WHERE productid = p.productid AND type = '1' AND localeid = @p1 LIMIT 1),
                    'Product ' || CAST(p.productid AS VARCHAR(20))
                ) AS ""Description1"",
                (SELECT description FROM spr.productdescriptions
                 WHERE productid = p.productid AND type = '3' AND localeid = @p1 LIMIT 1) AS ""Description2"",
                NULL AS ""Description3"",
                -- TRY_CAST guarded with a regex; a non-numeric type must not raise here.
                (SELECT description FROM spr.productdescriptions
                 WHERE productid = p.productid
                   AND type ~ '^\s*-?\d+\s*$' AND CAST(type AS INT) BETWEEN 20 AND 29
                   AND localeid = @p1 LIMIT 1) AS ""MarketingText"",
                CAST(p.manufacturerid AS VARCHAR(50)) AS ""ManufacturerId"",
                m.name AS ""ManufacturerName"",
                m.country AS ""CountryOfOrigin"",
                (SELECT sku FROM spr.productskus
                 WHERE productid = p.productid AND name = 'UNSPSC' LIMIT 1) AS ""UnspscCode"",
                NULL AS ""RecycledPercent"",
                cn.name AS ""SubClassName"",
                CAST(p.categoryid AS VARCHAR(50)) AS ""SubClassNumber"",
                NULL AS ""ClassName"",
                NULL AS ""ClassNumber"",
                NULL AS ""DepartmentName"",
                NULL AS ""DepartmentNumber"",
                NULL AS ""MasterDepartmentName"",
                NULL AS ""MasterDepartmentNumber"",
                sc.""Id"" AS ""SprCategoryId"",
                CASE WHEN EXISTS (SELECT 1 FROM spr.productimages WHERE productid = p.productid AND type = '225')
                     THEN 'https://content.etilize.com/225/' || CAST(p.productid AS VARCHAR(20)) || '.jpg'
                     ELSE NULL END AS ""ImageUrl225"",
                CASE WHEN EXISTS (SELECT 1 FROM spr.productimages WHERE productid = p.productid AND type = '75')
                     THEN 'https://content.etilize.com/75/' || CAST(p.productid AS VARCHAR(20)) || '.jpg'
                     ELSE NULL END AS ""ImageUrl75"",
                pk.keywords AS ""Keywords"",
                now() AS ""ContentVersionDate"",
                now() AS ""CreatedAt"",
                now() AS ""UpdatedAt""
            FROM spr.product p
            LEFT JOIN spr.manufacturer m ON m.manufacturerid = p.manufacturerid
            LEFT JOIN spr.categorynames cn ON cn.categoryid = p.categoryid AND cn.localeid = @p1
            LEFT JOIN spr.mapped_category mc ON mc.productid = p.productid
            LEFT JOIN ""SprCategories"" sc ON sc.""CategoryCode"" = CAST(mc.categoryid AS VARCHAR(50))
            LEFT JOIN spr.productkeywords pk ON pk.productid = p.productid AND pk.localeid = @p1
            WHERE p.isaccessory = '0' AND p.isactive = '1'";

        var count = await _dbContext.Database.ExecuteSqlRawAsync(
            sql, new object[] { _options.Locale, DefaultLocaleId }, cancellationToken);

        // Update the upload record with final counts (using parameterized query to avoid SQL injection)
        await _dbContext.Database.ExecuteSqlAsync($@"
            UPDATE ""SprContentUploads""
            SET ""TotalProducts"" = {count},
                ""ProcessedProducts"" = {count},
                ""NewProducts"" = {count},
                ""Status"" = 'Completed',
                ""ProcessingCompletedAt"" = now()
            WHERE ""Id"" = {uploadId}", cancellationToken);

        _logger.LogInformation("Transformed {Count} products", count);
        return count;
    }

    public async Task<int> TransformFeaturesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transforming features...");

        var sql = @"
            INSERT INTO ""SprProductFeatures"" (
                ""SprProductContentId"", ""SortOrder"", ""FeatureGroup"", ""BulletText"", ""CreatedAt""
            )
            SELECT
                pc.""Id"" AS ""SprProductContentId"",
                -- TRY_CAST has no PostgreSQL equivalent; guard the cast with a regex so a
                -- non-numeric sequenceno yields NULL rather than raising, as TRY_CAST did.
                COALESCE(
                    CASE WHEN pf.sequenceno ~ '^\s*-?\d+\s*$' THEN CAST(pf.sequenceno AS INT) END,
                    CAST(ROW_NUMBER() OVER (PARTITION BY pf.productid ORDER BY pf.id) AS INT)
                ) AS ""SortOrder"",
                NULL AS ""FeatureGroup"",
                pf.bullettext AS ""BulletText"",
                now() AS ""CreatedAt""
            FROM spr.productfeatures pf
            INNER JOIN ""SprProductContent"" pc ON pc.""ProductId"" = pf.productid
            WHERE pf.localeid = @p0 AND pf.bullettext IS NOT NULL";

        var count = await _dbContext.Database.ExecuteSqlRawAsync(
            sql, new object[] { DefaultLocaleId }, cancellationToken);

        _logger.LogInformation("Transformed {Count} features", count);
        return count;
    }

    public async Task<int> TransformRelationshipsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transforming relationships...");

        int totalCount = 0;

        // Accessories and Options
        var accessoriesSql = @"
            INSERT INTO ""SprProductRelationships"" (
                ""SprProductContentId"", ""RelatedProductId"", ""RelatedSku"", ""RelationshipType"",
                ""SortOrder"", ""IsBidirectional"", ""CreatedAt""
            )
            SELECT
                pc.""Id"" AS ""SprProductContentId"",
                CAST(pa.accessoryproductid AS VARCHAR(50)) AS ""RelatedProductId"",
                rpc.""Sku"" AS ""RelatedSku"",
                CASE WHEN pa.isoption = '1' THEN 'CrossSell'
                     WHEN pa.note LIKE '%Also Bought%' THEN 'AlsoBought'
                     ELSE 'Accessory' END AS ""RelationshipType"",
                0 AS ""SortOrder"",
                false AS ""IsBidirectional"",
                now() AS ""CreatedAt""
            FROM spr.productaccessories pa
            INNER JOIN ""SprProductContent"" pc ON pc.""ProductId"" = CAST(pa.productid AS VARCHAR(50))
            -- Resolve the related product's SKU (its StockNumber on M360) from its product id, so the
            -- content push emits RelatedStockNumber as a real SKU that matches a product. Left join so
            -- relationships to products outside the catalog still insert (RelatedSku stays null).
            LEFT JOIN ""SprProductContent"" rpc ON rpc.""ProductId"" = CAST(pa.accessoryproductid AS VARCHAR(50))
            WHERE pa.isactive = '1'";

        totalCount += await _dbContext.Database.ExecuteSqlRawAsync(accessoriesSql, cancellationToken);

        // Similar products
        var similarSql = @"
            INSERT INTO ""SprProductRelationships"" (
                ""SprProductContentId"", ""RelatedProductId"", ""RelatedSku"", ""RelationshipType"",
                ""SortOrder"", ""IsBidirectional"", ""CreatedAt""
            )
            SELECT
                pc.""Id"" AS ""SprProductContentId"",
                CAST(ps.similarproductid AS VARCHAR(50)) AS ""RelatedProductId"",
                rpc.""Sku"" AS ""RelatedSku"",
                'Similar' AS ""RelationshipType"",
                0 AS ""SortOrder"",
                true AS ""IsBidirectional"",
                now() AS ""CreatedAt""
            FROM spr.productsimilar ps
            INNER JOIN ""SprProductContent"" pc ON pc.""ProductId"" = CAST(ps.productid AS VARCHAR(50))
            LEFT JOIN ""SprProductContent"" rpc ON rpc.""ProductId"" = CAST(ps.similarproductid AS VARCHAR(50))
            WHERE ps.localeid = @p0";

        totalCount += await _dbContext.Database.ExecuteSqlRawAsync(
            similarSql, new object[] { DefaultLocaleId }, cancellationToken);

        // Upsell products
        var upsellSql = @"
            INSERT INTO ""SprProductRelationships"" (
                ""SprProductContentId"", ""RelatedProductId"", ""RelatedSku"", ""RelationshipType"",
                ""SortOrder"", ""IsBidirectional"", ""CreatedAt""
            )
            SELECT
                pc.""Id"" AS ""SprProductContentId"",
                CAST(pu.upsellproductid AS VARCHAR(50)) AS ""RelatedProductId"",
                rpc.""Sku"" AS ""RelatedSku"",
                'Upsell' AS ""RelationshipType"",
                0 AS ""SortOrder"",
                false AS ""IsBidirectional"",
                now() AS ""CreatedAt""
            FROM spr.productupsell pu
            INNER JOIN ""SprProductContent"" pc ON pc.""ProductId"" = CAST(pu.productid AS VARCHAR(50))
            LEFT JOIN ""SprProductContent"" rpc ON rpc.""ProductId"" = CAST(pu.upsellproductid AS VARCHAR(50))
            WHERE pu.localeid = @p0";

        totalCount += await _dbContext.Database.ExecuteSqlRawAsync(
            upsellSql, new object[] { DefaultLocaleId }, cancellationToken);

        _logger.LogInformation("Transformed {Count} relationships", totalCount);
        return totalCount;
    }

    public async Task<int> TransformSpecificationsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transforming specifications...");

        // Ensure indexes exist for the heavy join query
        _logger.LogInformation("Creating indexes on raw tables for specifications query...");

        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync(@"
                -- PostgreSQL has CREATE INDEX IF NOT EXISTS natively, so the sys.indexes
                -- existence check is dropped rather than translated (T-SQL IF is not valid
                -- outside a DO block anyway). NONCLUSTERED has no meaning here - every
                -- PostgreSQL index is non-clustered. INCLUDE is supported (PG 11+).
                CREATE INDEX IF NOT EXISTS ix_productattribute_productid_localeid
                    ON spr.productattribute (productid, localeid, isactive)
                    INCLUDE (attributeid, categoryid, displayvalue)", cancellationToken);

            await _dbContext.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_attributenames_attributeid_localeid
                    ON spr.attributenames (attributeid, localeid)
                    INCLUDE (name)", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create indexes (may already exist or lack permissions)");
        }

        // Build HTML specifications from product attributes grouped by headers
        // Using JOIN + GROUP BY instead of correlated subquery for much better performance
        var sql = @"
            WITH ""ProductSpecs"" AS (
                SELECT
                    pc.""Id"" AS ""SprProductContentId"",
                    '<table class=""specs"">' ||
                    -- T-SQL puts ordering in WITHIN GROUP; PostgreSQL puts it inside the
                    -- aggregate call. TRY_CAST is regex-guarded so a non-numeric display
                    -- order sorts last (9999) instead of raising.
                    string_agg(
                        '<tr><th>' || COALESCE(an.name, 'Attribute') || '</th><td>' ||
                        REPLACE(REPLACE(COALESCE(pa.displayvalue, ''), '<', '&lt;'), '>', '&gt;') ||
                        '</td></tr>',
                        ''
                        ORDER BY COALESCE(
                            CASE WHEN cda.defaultdisplayorder ~ '^\s*-?\d+\s*$'
                                 THEN CAST(cda.defaultdisplayorder AS INT) END, 9999),
                            an.name
                    ) ||
                    '</table>' AS ""SpecificationsHtml""
                FROM ""SprProductContent"" pc
                INNER JOIN spr.productattribute pa
                    ON pa.productid = pc.""ProductId""
                    AND pa.localeid = @p0
                    AND pa.isactive = '1'
                    AND pa.displayvalue IS NOT NULL
                    AND length(pa.displayvalue) > 0
                INNER JOIN spr.attributenames an
                    ON an.attributeid = pa.attributeid AND an.localeid = pa.localeid
                LEFT JOIN spr.categorydisplayattributes cda
                    ON cda.attributeid = pa.attributeid
                    AND cda.categoryid = pa.categoryid
                    AND cda.templatetype = '0'
                GROUP BY pc.""Id""
            )
            INSERT INTO ""SprProductSpecifications"" (
                ""SprProductContentId"", ""SpecificationsHtml"", ""CreatedAt""
            )
            SELECT
                ""SprProductContentId"",
                ""SpecificationsHtml"",
                now()
            FROM ""ProductSpecs""";

        var count = await _dbContext.Database.ExecuteSqlRawAsync(
            sql, new object[] { DefaultLocaleId }, cancellationToken);

        _logger.LogInformation("Transformed {Count} specifications", count);
        return count;
    }
}
