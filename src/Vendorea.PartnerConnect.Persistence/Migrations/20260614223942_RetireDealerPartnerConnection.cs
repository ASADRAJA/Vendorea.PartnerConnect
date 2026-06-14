using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireDealerPartnerConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Test-data cleanup so the converged partner keys/indexes apply cleanly.
            // Fingerprints are a dedup cache; clearing them is safe. Re-home any document whose
            // backfilled partner key is missing onto the first known partner.
            migrationBuilder.Sql("DELETE FROM DocumentFingerprints;");
            migrationBuilder.Sql("UPDATE PartnerDocuments SET TradingPartnerId = (SELECT MIN(Id) FROM TradingPartners) WHERE TradingPartnerId NOT IN (SELECT Id FROM TradingPartners);");
            migrationBuilder.Sql("UPDATE QuarantinedDocuments SET TradingPartnerId = (SELECT MIN(Id) FROM TradingPartners) WHERE TradingPartnerId NOT IN (SELECT Id FROM TradingPartners);");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFingerprints_DealerPartnerConnections_DealerPartnerConnectionId",
                table: "DocumentFingerprints");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentIdempotencyKeys_DealerPartnerConnections_DealerPartnerConnectionId",
                table: "DocumentIdempotencyKeys");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerDocuments_DealerPartnerConnections_DealerPartnerConnectionId",
                table: "PartnerDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerDocuments_DealerPartnerConnections_DealerPartnerConnectionId1",
                table: "PartnerDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_QuarantinedDocuments_DealerPartnerConnections_DealerPartnerConnectionId",
                table: "QuarantinedDocuments");

            migrationBuilder.DropTable(
                name: "DealerPartnerConnections");

            migrationBuilder.DropIndex(
                name: "IX_QuarantinedDocuments_DealerPartnerConnectionId",
                table: "QuarantinedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_QuarantinedDocuments_DealerPartnerConnectionId_QuarantinedAt",
                table: "QuarantinedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId",
                table: "PartnerDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId_ReceivedAt",
                table: "PartnerDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId_State",
                table: "PartnerDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId1",
                table: "PartnerDocuments");

            migrationBuilder.DropIndex(
                name: "IX_DocumentIdempotencyKeys_DealerPartnerConnectionId",
                table: "DocumentIdempotencyKeys");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFingerprints_Connection_Type_Hash",
                table: "DocumentFingerprints");

            migrationBuilder.DropColumn(
                name: "DealerPartnerConnectionId",
                table: "QuarantinedDocuments");

            migrationBuilder.DropColumn(
                name: "DealerPartnerConnectionId",
                table: "PartnerDocuments");

            migrationBuilder.DropColumn(
                name: "DealerPartnerConnectionId1",
                table: "PartnerDocuments");

            migrationBuilder.DropColumn(
                name: "DealerPartnerConnectionId",
                table: "DocumentIdempotencyKeys");

            migrationBuilder.DropColumn(
                name: "DealerPartnerConnectionId",
                table: "DocumentFingerprints");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_TenantId",
                table: "QuarantinedDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_TradingPartnerId",
                table: "QuarantinedDocuments",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_TradingPartnerId_QuarantinedAt",
                table: "QuarantinedDocuments",
                columns: new[] { "TradingPartnerId", "QuarantinedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_TenantId",
                table: "PartnerDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_TradingPartnerId",
                table: "PartnerDocuments",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_TradingPartnerId_ReceivedAt",
                table: "PartnerDocuments",
                columns: new[] { "TradingPartnerId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_TradingPartnerId_State",
                table: "PartnerDocuments",
                columns: new[] { "TradingPartnerId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFingerprints_Partner_Type_Hash",
                table: "DocumentFingerprints",
                columns: new[] { "TradingPartnerId", "DocumentType", "ContentHash" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFingerprints_TradingPartners_TradingPartnerId",
                table: "DocumentFingerprints",
                column: "TradingPartnerId",
                principalTable: "TradingPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerDocuments_TradingPartners_TradingPartnerId",
                table: "PartnerDocuments",
                column: "TradingPartnerId",
                principalTable: "TradingPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuarantinedDocuments_TradingPartners_TradingPartnerId",
                table: "QuarantinedDocuments",
                column: "TradingPartnerId",
                principalTable: "TradingPartners",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFingerprints_TradingPartners_TradingPartnerId",
                table: "DocumentFingerprints");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerDocuments_TradingPartners_TradingPartnerId",
                table: "PartnerDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_QuarantinedDocuments_TradingPartners_TradingPartnerId",
                table: "QuarantinedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_QuarantinedDocuments_TenantId",
                table: "QuarantinedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_QuarantinedDocuments_TradingPartnerId",
                table: "QuarantinedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_QuarantinedDocuments_TradingPartnerId_QuarantinedAt",
                table: "QuarantinedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PartnerDocuments_TenantId",
                table: "PartnerDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PartnerDocuments_TradingPartnerId",
                table: "PartnerDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PartnerDocuments_TradingPartnerId_ReceivedAt",
                table: "PartnerDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PartnerDocuments_TradingPartnerId_State",
                table: "PartnerDocuments");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFingerprints_Partner_Type_Hash",
                table: "DocumentFingerprints");

            migrationBuilder.AddColumn<int>(
                name: "DealerPartnerConnectionId",
                table: "QuarantinedDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DealerPartnerConnectionId",
                table: "PartnerDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DealerPartnerConnectionId1",
                table: "PartnerDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DealerPartnerConnectionId",
                table: "DocumentIdempotencyKeys",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DealerPartnerConnectionId",
                table: "DocumentFingerprints",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DealerPartnerConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConnectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CredentialsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    DisconnectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastSuccessfulSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerPartnerConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealerPartnerConnections_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_DealerPartnerConnectionId",
                table: "QuarantinedDocuments",
                column: "DealerPartnerConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_DealerPartnerConnectionId_QuarantinedAt",
                table: "QuarantinedDocuments",
                columns: new[] { "DealerPartnerConnectionId", "QuarantinedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId",
                table: "PartnerDocuments",
                column: "DealerPartnerConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId_ReceivedAt",
                table: "PartnerDocuments",
                columns: new[] { "DealerPartnerConnectionId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId_State",
                table: "PartnerDocuments",
                columns: new[] { "DealerPartnerConnectionId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId1",
                table: "PartnerDocuments",
                column: "DealerPartnerConnectionId1");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIdempotencyKeys_DealerPartnerConnectionId",
                table: "DocumentIdempotencyKeys",
                column: "DealerPartnerConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFingerprints_Connection_Type_Hash",
                table: "DocumentFingerprints",
                columns: new[] { "DealerPartnerConnectionId", "DocumentType", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DealerPartnerConnections_DealerId",
                table: "DealerPartnerConnections",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_DealerPartnerConnections_DealerId_TradingPartnerId",
                table: "DealerPartnerConnections",
                columns: new[] { "DealerId", "TradingPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DealerPartnerConnections_Status",
                table: "DealerPartnerConnections",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DealerPartnerConnections_TradingPartnerId",
                table: "DealerPartnerConnections",
                column: "TradingPartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFingerprints_DealerPartnerConnections_DealerPartnerConnectionId",
                table: "DocumentFingerprints",
                column: "DealerPartnerConnectionId",
                principalTable: "DealerPartnerConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentIdempotencyKeys_DealerPartnerConnections_DealerPartnerConnectionId",
                table: "DocumentIdempotencyKeys",
                column: "DealerPartnerConnectionId",
                principalTable: "DealerPartnerConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerDocuments_DealerPartnerConnections_DealerPartnerConnectionId",
                table: "PartnerDocuments",
                column: "DealerPartnerConnectionId",
                principalTable: "DealerPartnerConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerDocuments_DealerPartnerConnections_DealerPartnerConnectionId1",
                table: "PartnerDocuments",
                column: "DealerPartnerConnectionId1",
                principalTable: "DealerPartnerConnections",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuarantinedDocuments_DealerPartnerConnections_DealerPartnerConnectionId",
                table: "QuarantinedDocuments",
                column: "DealerPartnerConnectionId",
                principalTable: "DealerPartnerConnections",
                principalColumn: "Id");
        }
    }
}
