using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgPortalUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrgPortalUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AllTenants = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgPortalUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgPortalUsers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgPortalUserTenants",
                columns: table => new
                {
                    OrgPortalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgPortalUserTenants", x => new { x.OrgPortalUserId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_OrgPortalUserTenants_OrgPortalUsers_OrgPortalUserId",
                        column: x => x.OrgPortalUserId,
                        principalTable: "OrgPortalUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgPortalUsers_OrganizationId_Email",
                table: "OrgPortalUsers",
                columns: new[] { "OrganizationId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrgPortalUserTenants");

            migrationBuilder.DropTable(
                name: "OrgPortalUsers");
        }
    }
}
