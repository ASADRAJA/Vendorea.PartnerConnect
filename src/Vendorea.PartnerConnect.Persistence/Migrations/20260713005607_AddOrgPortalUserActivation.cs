using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgPortalUserActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "OrgPortalUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Invited");

            // Data-fix: existing portal users already have passwords, so treat them as Active — the new
            // column defaults new rows to Invited, but pre-existing rows (e.g. the seeded admin) must
            // keep working. Only rows created before this migration are affected.
            migrationBuilder.Sql("UPDATE [OrgPortalUsers] SET [Status] = 'Active';");

            migrationBuilder.CreateTable(
                name: "OrgPortalUserTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrgPortalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgPortalUserTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgPortalUserTokens_OrgPortalUsers_OrgPortalUserId",
                        column: x => x.OrgPortalUserId,
                        principalTable: "OrgPortalUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgPortalUserTokens_OrgPortalUserId",
                table: "OrgPortalUserTokens",
                column: "OrgPortalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgPortalUserTokens_TokenHash",
                table: "OrgPortalUserTokens",
                column: "TokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrgPortalUserTokens");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OrgPortalUsers");
        }
    }
}
