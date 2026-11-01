using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddM360PushRecordCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "M360PushRecordsCreated",
                table: "SprContentUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "M360PushRecordsSkipped",
                table: "SprContentUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "M360PushRecordsUpdated",
                table: "SprContentUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "M360PushRecordsCreated",
                table: "SprContentUploads");

            migrationBuilder.DropColumn(
                name: "M360PushRecordsSkipped",
                table: "SprContentUploads");

            migrationBuilder.DropColumn(
                name: "M360PushRecordsUpdated",
                table: "SprContentUploads");
        }
    }
}
