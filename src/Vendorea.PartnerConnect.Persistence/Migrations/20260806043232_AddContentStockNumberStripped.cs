using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentStockNumberStripped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StockNumberStripped",
                table: "SprProductContent",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // Backfill existing content so the price-to-content join keeps working without waiting
            // for the next full ingest. Same rule the transform applies: the SPR stock number when
            // SPR publishes one, left null when Sku fell back to the manufacturer part number.
            // Guarded because the raw Etilize staging tables only exist where an ingest has run.
            //
            // Staged through a temp table rather than a correlated lookup per content row: productid
            // needs casting to compare against ProductId, which is non-sargable, so the correlated
            // form rescans all of productskus for every content row and times out on Azure SQL.
            // MIN(sku) matches the transform's TOP 1 exactly - 'SP Richards' rows are 1:1 per product.
            migrationBuilder.Sql(@"
                IF OBJECT_ID('spr.productskus', 'U') IS NOT NULL
                BEGIN
                    SELECT CAST(productid AS NVARCHAR(50)) AS ProductId, MIN(sku) AS Sku
                    INTO #SprStockNumbers
                    FROM spr.productskus
                    WHERE name = 'SP Richards'
                    GROUP BY productid;

                    CREATE CLUSTERED INDEX IX_SprStockNumbers ON #SprStockNumbers(ProductId);

                    UPDATE c
                    SET c.StockNumberStripped = s.Sku
                    FROM SprProductContent c
                    INNER JOIN #SprStockNumbers s ON s.ProductId = c.ProductId;

                    DROP TABLE #SprStockNumbers;
                END");

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_StockNumberStripped_Locale",
                table: "SprProductContent",
                columns: new[] { "StockNumberStripped", "LocaleId" },
                filter: "[StockNumberStripped] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SprProductContent_StockNumberStripped_Locale",
                table: "SprProductContent");

            migrationBuilder.DropColumn(
                name: "StockNumberStripped",
                table: "SprProductContent");
        }
    }
}
