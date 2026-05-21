using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIdFromBusinessKeyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Id",
                schema: "spr",
                table: "search_attribute_values");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "spr",
                table: "search_attribute");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "spr",
                table: "mapped_category_taxonomy");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "spr",
                table: "mapped_category_names");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "spr",
                table: "mapped_category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Id",
                schema: "spr",
                table: "search_attribute_values",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Id",
                schema: "spr",
                table: "search_attribute",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Id",
                schema: "spr",
                table: "mapped_category_taxonomy",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Id",
                schema: "spr",
                table: "mapped_category_names",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Id",
                schema: "spr",
                table: "mapped_category",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
