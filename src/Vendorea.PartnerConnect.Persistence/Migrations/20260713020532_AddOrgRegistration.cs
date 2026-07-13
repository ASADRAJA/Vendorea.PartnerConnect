using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrgRegistrationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    OrganizationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BillingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AdminDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdminEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecisionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionByAdmin = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgRegistrationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgRegistrationRequests_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgRegistrationRequests_OrganizationId",
                table: "OrgRegistrationRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgRegistrationRequests_Status",
                table: "OrgRegistrationRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrgRegistrationRequests");
        }
    }
}
