using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationIdToMetering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "UsageSummaries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "UsageRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageSummaries_OrganizationId",
                table: "UsageSummaries",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageSummaries_OrganizationId_Period",
                table: "UsageSummaries",
                columns: new[] { "OrganizationId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_OrganizationId",
                table: "UsageRecords",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_OrganizationId_Timestamp",
                table: "UsageRecords",
                columns: new[] { "OrganizationId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UsageSummaries_OrganizationId",
                table: "UsageSummaries");

            migrationBuilder.DropIndex(
                name: "IX_UsageSummaries_OrganizationId_Period",
                table: "UsageSummaries");

            migrationBuilder.DropIndex(
                name: "IX_UsageRecords_OrganizationId",
                table: "UsageRecords");

            migrationBuilder.DropIndex(
                name: "IX_UsageRecords_OrganizationId_Timestamp",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "UsageSummaries");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "UsageRecords");
        }
    }
}
