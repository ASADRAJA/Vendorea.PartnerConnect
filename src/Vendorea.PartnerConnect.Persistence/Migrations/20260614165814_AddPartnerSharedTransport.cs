using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerSharedTransport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransportConfigJson",
                table: "TradingPartners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportCredentialsJson",
                table: "TradingPartners",
                type: "nvarchar(max)",
                nullable: true);

            // Convergence: lift each partner's shared transport up from its dealer connections.
            // Copies config/credentials from one existing dealer connection per partner (legacy
            // credentials are plaintext; the protector reads them tolerantly until re-saved).
            migrationBuilder.Sql(@"
                UPDATE tp
                SET tp.TransportConfigJson = c.ConfigurationJson,
                    tp.TransportCredentialsJson = c.CredentialsJson
                FROM TradingPartners tp
                CROSS APPLY (
                    SELECT TOP 1 ConfigurationJson, CredentialsJson
                    FROM DealerPartnerConnections d
                    WHERE d.TradingPartnerId = tp.Id AND d.ConfigurationJson IS NOT NULL
                    ORDER BY d.Id
                ) c;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransportConfigJson",
                table: "TradingPartners");

            migrationBuilder.DropColumn(
                name: "TransportCredentialsJson",
                table: "TradingPartners");
        }
    }
}
