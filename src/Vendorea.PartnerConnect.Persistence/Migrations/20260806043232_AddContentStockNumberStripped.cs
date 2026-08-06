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
            migrationBuilder.Sql(@"
                IF OBJECT_ID('spr.productskus', 'U') IS NOT NULL
                BEGIN
                    UPDATE c
                    SET c.StockNumberStripped = s.sku
                    FROM SprProductContent c
                    CROSS APPLY (
                        SELECT TOP 1 sku FROM spr.productskus
                        WHERE CAST(productid AS NVARCHAR(50)) = c.ProductId AND name = 'SP Richards'
                    ) s;
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
