using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPartnerConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantPartnerAccounts_TenantId_TradingPartnerId_AccountNumber",
                table: "TenantPartnerAccounts");

            migrationBuilder.AddColumn<string>(
                name: "TenantConfirmationFieldsJson",
                table: "TradingPartners",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactFirstName",
                table: "Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactLastName",
                table: "Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "TenantPartnerAccounts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "TenantPartnerAccounts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationFieldsJson",
                table: "TenantPartnerAccounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactFirstName",
                table: "TenantPartnerAccounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactLastName",
                table: "TenantPartnerAccounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAt",
                table: "TenantPartnerAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "TenantPartnerAccounts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalTenantId",
                table: "TenantPartnerAccounts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "TenantPartnerAccounts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "TenantPartnerAccounts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialIdentifyingCode",
                table: "TenantPartnerAccounts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OrganizationId_ExternalId",
                table: "Tenants",
                columns: new[] { "OrganizationId", "ExternalId" },
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPartnerAccounts_OrganizationId_ApprovalStatus",
                table: "TenantPartnerAccounts",
                columns: new[] { "OrganizationId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantPartnerAccounts_TenantId_TradingPartnerId_AccountNumber",
                table: "TenantPartnerAccounts",
                columns: new[] { "TenantId", "TradingPartnerId", "AccountNumber" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantPartnerAccounts_Organizations_OrganizationId",
                table: "TenantPartnerAccounts",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantPartnerAccounts_Organizations_OrganizationId",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_OrganizationId_ExternalId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_TenantPartnerAccounts_OrganizationId_ApprovalStatus",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropIndex(
                name: "IX_TenantPartnerAccounts_TenantId_TradingPartnerId_AccountNumber",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropColumn(
                name: "TenantConfirmationFieldsJson",
                table: "TradingPartners");

            migrationBuilder.DropColumn(
                name: "ContactFirstName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ContactLastName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropColumn(
                name: "ConfirmationFieldsJson",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropColumn(
                name: "ContactFirstName",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropColumn(
                name: "ContactLastName",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalTenantId",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "TenantPartnerAccounts");

            migrationBuilder.DropColumn(
                name: "SpecialIdentifyingCode",
                table: "TenantPartnerAccounts");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "TenantPartnerAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantPartnerAccounts_TenantId_TradingPartnerId_AccountNumber",
                table: "TenantPartnerAccounts",
                columns: new[] { "TenantId", "TradingPartnerId", "AccountNumber" },
                unique: true);
        }
    }
}
