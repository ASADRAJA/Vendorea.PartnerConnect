using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationPayloadArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_PiiRedactedAt",
                table: "SalesOrders");

            migrationBuilder.CreateTable(
                name: "IntegrationPayloadArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PayloadKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<int>(type: "integer", nullable: false),
                    RedactedPaths = table.Column<string>(type: "jsonb", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationPayloadArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationPayloadArchives_IntegrationConnections_Integrati~",
                        column: x => x.IntegrationConnectionId,
                        principalTable: "IntegrationConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_PendingRedaction",
                table: "SalesOrders",
                columns: new[] { "PurchaseDate", "Status" },
                filter: "\"PiiRedactedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationPayloadArchives_ExpiresAt",
                table: "IntegrationPayloadArchives",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationPayloadArchives_IntegrationConnectionId_PayloadK~",
                table: "IntegrationPayloadArchives",
                columns: new[] { "IntegrationConnectionId", "PayloadKind", "ExternalReference", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationPayloadArchives_TenantId_ExternalReference_Captu~",
                table: "IntegrationPayloadArchives",
                columns: new[] { "TenantId", "ExternalReference", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationPayloadArchives");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_PendingRedaction",
                table: "SalesOrders");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_PiiRedactedAt",
                table: "SalesOrders",
                column: "PiiRedactedAt",
                filter: "\"PiiRedactedAt\" IS NULL");
        }
    }
}
