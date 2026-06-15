using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgPortalApiKeyHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierInventorySnapshots_SupplierInventorySnapshots_PreviousSnapshotId",
                table: "SupplierInventorySnapshots");

            migrationBuilder.AddColumn<string>(
                name: "PortalApiKeyHash",
                table: "Organizations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_PortalApiKeyHash",
                table: "Organizations",
                column: "PortalApiKeyHash",
                unique: true,
                filter: "[PortalApiKeyHash] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierInventorySnapshots_SupplierInventorySnapshots_PreviousSnapshotId",
                table: "SupplierInventorySnapshots",
                column: "PreviousSnapshotId",
                principalTable: "SupplierInventorySnapshots",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierInventorySnapshots_SupplierInventorySnapshots_PreviousSnapshotId",
                table: "SupplierInventorySnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_PortalApiKeyHash",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PortalApiKeyHash",
                table: "Organizations");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierInventorySnapshots_SupplierInventorySnapshots_PreviousSnapshotId",
                table: "SupplierInventorySnapshots",
                column: "PreviousSnapshotId",
                principalTable: "SupplierInventorySnapshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
