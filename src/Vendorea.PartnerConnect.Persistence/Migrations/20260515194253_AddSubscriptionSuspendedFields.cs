using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionSuspendedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "MerchantSubscriptionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuspendedByUserId",
                table: "MerchantSubscriptionRequests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "MerchantSubscriptionRequests");

            migrationBuilder.DropColumn(
                name: "SuspendedByUserId",
                table: "MerchantSubscriptionRequests");
        }
    }
}
