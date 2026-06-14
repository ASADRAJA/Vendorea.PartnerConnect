using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerKeysToDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "QuarantinedDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TradingPartnerId",
                table: "QuarantinedDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PartnerDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TradingPartnerId",
                table: "PartnerDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TradingPartnerId",
                table: "DocumentFingerprints",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill the converged partner key on existing rows from each document's connection.
            migrationBuilder.Sql(@"
                UPDATE pd SET pd.TradingPartnerId = c.TradingPartnerId
                FROM PartnerDocuments pd
                JOIN DealerPartnerConnections c ON c.Id = pd.DealerPartnerConnectionId;");
            migrationBuilder.Sql(@"
                UPDATE df SET df.TradingPartnerId = c.TradingPartnerId
                FROM DocumentFingerprints df
                JOIN DealerPartnerConnections c ON c.Id = df.DealerPartnerConnectionId;");
            migrationBuilder.Sql(@"
                UPDATE qd SET qd.TradingPartnerId = c.TradingPartnerId
                FROM QuarantinedDocuments qd
                JOIN DealerPartnerConnections c ON c.Id = qd.DealerPartnerConnectionId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "QuarantinedDocuments");

            migrationBuilder.DropColumn(
                name: "TradingPartnerId",
                table: "QuarantinedDocuments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PartnerDocuments");

            migrationBuilder.DropColumn(
                name: "TradingPartnerId",
                table: "PartnerDocuments");

            migrationBuilder.DropColumn(
                name: "TradingPartnerId",
                table: "DocumentFingerprints");
        }
    }
}
