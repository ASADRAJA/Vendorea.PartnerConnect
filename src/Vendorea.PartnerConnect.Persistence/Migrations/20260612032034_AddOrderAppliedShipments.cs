using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAppliedShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderAppliedShipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ManifestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAppliedShipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderAppliedShipments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderAppliedShipments_OrderId_ManifestId",
                table: "OrderAppliedShipments",
                columns: new[] { "OrderId", "ManifestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderAppliedShipments");
        }
    }
}
