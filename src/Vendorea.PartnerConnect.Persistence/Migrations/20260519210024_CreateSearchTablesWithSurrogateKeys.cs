using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateSearchTablesWithSurrogateKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing tables created by AddSprRawSchema migration (if they exist)
            // These tables were created with composite PKs, but we need surrogate identity PKs
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[search_attribute]");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [spr].[search_attribute_values]");

            migrationBuilder.CreateTable(
                name: "search_attribute",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    productid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    attributeid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    valueid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    absolutevalue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isabsolute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    localeid = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_attribute", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "search_attribute_values",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    valueid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    value = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    absolutevalue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    unitid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isabsolute = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_attribute_values", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_search_attribute_attributeid",
                schema: "spr",
                table: "search_attribute",
                column: "attributeid");

            migrationBuilder.CreateIndex(
                name: "IX_search_attribute_productid",
                schema: "spr",
                table: "search_attribute",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_search_attribute_valueid",
                schema: "spr",
                table: "search_attribute",
                column: "valueid");

            migrationBuilder.CreateIndex(
                name: "IX_search_attribute_values_value",
                schema: "spr",
                table: "search_attribute_values",
                column: "value");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_attribute",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "search_attribute_values",
                schema: "spr");
        }
    }
}
