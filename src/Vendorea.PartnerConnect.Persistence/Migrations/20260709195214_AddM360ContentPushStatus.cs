using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddM360ContentPushStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "M360PushClaimedAt",
                table: "SprContentUploads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "M360PushCurrentBatch",
                table: "SprContentUploads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "M360PushError",
                table: "SprContentUploads",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "M360PushProductsPushed",
                table: "SprContentUploads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "M360PushStatus",
                table: "SprContentUploads",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "M360PushTotalBatches",
                table: "SprContentUploads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "M360PushTotalProducts",
                table: "SprContentUploads",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "M360PushClaimedAt",
                table: "SprContentUploads");

            migrationBuilder.DropColumn(
                name: "M360PushCurrentBatch",
                table: "SprContentUploads");

            migrationBuilder.DropColumn(
                name: "M360PushError",
                table: "SprContentUploads");

            migrationBuilder.DropColumn(
                name: "M360PushProductsPushed",
                table: "SprContentUploads");

            migrationBuilder.DropColumn(
                name: "M360PushStatus",
                table: "SprContentUploads");

            migrationBuilder.DropColumn(
                name: "M360PushTotalBatches",
                table: "SprContentUploads");

            migrationBuilder.DropColumn(
                name: "M360PushTotalProducts",
                table: "SprContentUploads");
        }
    }
}
