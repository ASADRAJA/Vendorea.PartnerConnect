using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationRegistrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExternalPortalEnabled",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                table: "Organizations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PortalApiKey",
                table: "Organizations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortalBaseUrl",
                table: "Organizations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Organizations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationPartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationPartners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationPartners_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationPartners_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPartners_OrganizationId_TradingPartnerId",
                table: "OrganizationPartners",
                columns: new[] { "OrganizationId", "TradingPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPartners_TradingPartnerId",
                table: "OrganizationPartners",
                column: "TradingPartnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationPartners");

            migrationBuilder.DropColumn(
                name: "ExternalPortalEnabled",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PortalApiKey",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PortalBaseUrl",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Organizations");
        }
    }
}
