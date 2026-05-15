using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantSubscriptionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MerchantSubscriptionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    DeniedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeniedByUserId = table.Column<int>(type: "int", nullable: true),
                    DenialReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantSubscriptionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantSubscriptionRequests_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptionRequests_Status",
                table: "MerchantSubscriptionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptionRequests_TenantId",
                table: "MerchantSubscriptionRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptionRequests_TenantId_TradingPartnerId",
                table: "MerchantSubscriptionRequests",
                columns: new[] { "TenantId", "TradingPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptionRequests_TradingPartnerId",
                table: "MerchantSubscriptionRequests",
                column: "TradingPartnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantSubscriptionRequests");
        }
    }
}
