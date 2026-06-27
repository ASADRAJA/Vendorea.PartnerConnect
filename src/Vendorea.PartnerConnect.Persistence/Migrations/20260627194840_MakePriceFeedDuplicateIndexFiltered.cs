using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakePriceFeedDuplicateIndexFiltered : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data fix: older imports that parsed zero records were incorrectly marked 'Completed',
            // which leaves them occupying the (soon-to-be-filtered) unique slot and permanently
            // blocks re-importing the same file. Demote them to 'Failed' so they no longer block.
            // Runs before the filtered unique index is created so the index build sees clean data.
            migrationBuilder.Sql(
                "UPDATE PriceFeedUploads " +
                "SET Status = 'Failed', " +
                "    ErrorMessage = COALESCE(ErrorMessage, 'No valid records found in the file.') " +
                "WHERE Status = 'Completed' AND RecordCount = 0;");

            migrationBuilder.DropIndex(
                name: "IX_PriceFeedUploads_DealerId_TradingPartnerId_FileHash",
                table: "PriceFeedUploads");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_DealerId_TradingPartnerId_FileHash",
                table: "PriceFeedUploads",
                columns: new[] { "DealerId", "TradingPartnerId", "FileHash" },
                unique: true,
                filter: "[Status] IN ('Completed', 'PushedToMerchant360')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PriceFeedUploads_DealerId_TradingPartnerId_FileHash",
                table: "PriceFeedUploads");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_DealerId_TradingPartnerId_FileHash",
                table: "PriceFeedUploads",
                columns: new[] { "DealerId", "TradingPartnerId", "FileHash" },
                unique: true);
        }
    }
}
