using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerAccountOwnershipAndDelegation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationConnections_IntegrationProviderId",
                table: "IntegrationConnections");

            migrationBuilder.AddColumn<string>(
                name: "AccountOwnership",
                table: "IntegrationConnections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Primary");

            migrationBuilder.CreateTable(
                name: "IntegrationAccountDelegations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationProviderId = table.Column<int>(type: "integer", nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerTenantId = table.Column<int>(type: "integer", nullable: false),
                    DelegateTenantId = table.Column<int>(type: "integer", nullable: false),
                    GrantedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationAccountDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationAccountDelegations_IntegrationProviders_Integrat~",
                        column: x => x.IntegrationProviderId,
                        principalTable: "IntegrationProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_IntegrationConnections_PrimaryAccountOwner",
                table: "IntegrationConnections",
                columns: new[] { "IntegrationProviderId", "ExternalAccountId" },
                unique: true,
                filter: "\"ExternalAccountId\" IS NOT NULL AND \"AccountOwnership\" = 'Primary' AND \"Status\" <> 'Revoked'");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationAccountDelegations_DelegateTenantId",
                table: "IntegrationAccountDelegations",
                column: "DelegateTenantId");

            migrationBuilder.CreateIndex(
                name: "UX_IntegrationAccountDelegations_ActiveGrant",
                table: "IntegrationAccountDelegations",
                columns: new[] { "IntegrationProviderId", "ExternalAccountId", "DelegateTenantId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationAccountDelegations");

            migrationBuilder.DropIndex(
                name: "UX_IntegrationConnections_PrimaryAccountOwner",
                table: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "AccountOwnership",
                table: "IntegrationConnections");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConnections_IntegrationProviderId",
                table: "IntegrationConnections",
                column: "IntegrationProviderId");
        }
    }
}
