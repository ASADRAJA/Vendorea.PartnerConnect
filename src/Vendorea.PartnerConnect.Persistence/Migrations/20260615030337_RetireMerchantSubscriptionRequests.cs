using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireMerchantSubscriptionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill: migrate APPROVED legacy subscriptions onto the connections model
            // (TenantPartnerAccount). Skip any whose tenant already has a connection to that
            // partner, so we never create duplicate/near-duplicate connections. The legacy
            // TenantId is the internal PC Tenant.Id. Runs before the table is dropped.
            migrationBuilder.Sql(@"
INSERT INTO TenantPartnerAccounts
    (TenantId, OrganizationId, ExternalTenantId, TradingPartnerId, AccountNumber,
     ApprovalStatus, IsActive, CreatedAt, VerifiedAt, DecidedAt)
SELECT m.TenantId, t.OrganizationId, ISNULL(t.ExternalId, ''), m.TradingPartnerId, m.AccountNumber,
       'Approved', 1, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME()
FROM MerchantSubscriptionRequests m
INNER JOIN Tenants t ON t.Id = m.TenantId
WHERE m.Status = 'Approved'
  AND NOT EXISTS (
      SELECT 1 FROM TenantPartnerAccounts a
      WHERE a.TenantId = m.TenantId AND a.TradingPartnerId = m.TradingPartnerId
  );");

            migrationBuilder.DropTable(
                name: "MerchantSubscriptionRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MerchantSubscriptionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DenialReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DeniedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeniedByUserId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SuspendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedByUserId = table.Column<int>(type: "int", nullable: true),
                    TenantCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    TenantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
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
    }
}
