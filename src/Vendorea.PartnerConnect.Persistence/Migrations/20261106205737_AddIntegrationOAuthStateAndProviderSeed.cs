using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationOAuthStateAndProviderSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationOAuthStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    IntegrationConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOAuthStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationOAuthStates_IntegrationConnections_IntegrationCo~",
                        column: x => x.IntegrationConnectionId,
                        principalTable: "IntegrationConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "IntegrationProviders",
                columns: new[] { "Id", "AuthKind", "Capabilities", "Code", "ConfigSchemaJson", "CreatedAt", "IsEnabled", "Kind", "Name", "UpdatedAt" },
                values: new object[] { 1, "OAuth2", 15, "AMAZON_SP", null, new DateTime(2026, 11, 6, 0, 0, 0, 0, DateTimeKind.Utc), true, "Marketplace", "Amazon Selling Partner", null });

            migrationBuilder.InsertData(
                table: "IntegrationProviders",
                // IsEnabled is listed explicitly. EF omits a column whose seed value equals the
                // CLR default, which here is false while the column default is true — leaving it
                // out would enable Amazon Business on every environment, months before its
                // onboarding completes.
                columns: new[] { "Id", "AuthKind", "Capabilities", "Code", "ConfigSchemaJson", "CreatedAt", "IsEnabled", "Kind", "Name", "UpdatedAt" },
                values: new object[] { 2, "OAuth2", 112, "AMAZON_BUSINESS", null, new DateTime(2026, 11, 6, 0, 0, 0, 0, DateTimeKind.Utc), false, "Procurement", "Amazon Business", null });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOAuthStates_ExpiresAt",
                table: "IntegrationOAuthStates",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOAuthStates_IntegrationConnectionId",
                table: "IntegrationOAuthStates",
                column: "IntegrationConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOAuthStates_TokenHash",
                table: "IntegrationOAuthStates",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationOAuthStates");

            migrationBuilder.DeleteData(
                table: "IntegrationProviders",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "IntegrationProviders",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
