using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceFeedUploadAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantPartnerAccountId",
                table: "PriceFeedUploads",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_AccountLatest",
                table: "PriceFeedUploads",
                columns: new[] { "TenantPartnerAccountId", "UploadedAt" },
                filter: "\"TenantPartnerAccountId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PriceFeedUploads_TenantPartnerAccounts_TenantPartnerAccount~",
                table: "PriceFeedUploads",
                column: "TenantPartnerAccountId",
                principalTable: "TenantPartnerAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceFeedUploads_TenantPartnerAccounts_TenantPartnerAccount~",
                table: "PriceFeedUploads");

            migrationBuilder.DropIndex(
                name: "IX_PriceFeedUploads_AccountLatest",
                table: "PriceFeedUploads");

            migrationBuilder.DropColumn(
                name: "TenantPartnerAccountId",
                table: "PriceFeedUploads");
        }
    }
}
