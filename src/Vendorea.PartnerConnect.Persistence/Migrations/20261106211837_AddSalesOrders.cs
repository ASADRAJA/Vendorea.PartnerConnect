using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ExternalOrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MarketplaceId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ExternalStatusRaw = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ExternalFulfillmentStatusRaw = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FulfillmentChannel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MappingStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProgramsRawJson = table.Column<string>(type: "jsonb", nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceLastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OrderTotalAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    BuyerDataAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    BuyerEmailRef = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    BuyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ShipToName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ShipToLine1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ShipToLine2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ShipToCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShipToStateOrRegion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShipToPostalCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ShipToCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    PiiRedactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastProcessingResult = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrders_IntegrationConnections_IntegrationConnectionId",
                        column: x => x.IntegrationConnectionId,
                        principalTable: "IntegrationConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalLineId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SellerSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Asin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QuantityOrdered = table.Column<int>(type: "integer", nullable: false),
                    QuantityShipped = table.Column<int>(type: "integer", nullable: false),
                    MappingStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    M360CatalogItemId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ItemPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ItemTax = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ShippingPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PromotionDiscount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ProceedsRawJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrderLines_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ExternalStatusRaw = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SourceLastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrderStatusHistory_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_SalesOrderId_ExternalLineId",
                table: "SalesOrderLines",
                columns: new[] { "SalesOrderId", "ExternalLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_SellerSku",
                table: "SalesOrderLines",
                column: "SellerSku");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_IntegrationConnectionId_ExternalOrderId",
                table: "SalesOrders",
                columns: new[] { "IntegrationConnectionId", "ExternalOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_IntegrationConnectionId_SourceLastUpdatedAt",
                table: "SalesOrders",
                columns: new[] { "IntegrationConnectionId", "SourceLastUpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_MappingStatus",
                table: "SalesOrders",
                column: "MappingStatus",
                filter: "\"MappingStatus\" <> 'Mapped'");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_PiiRedactedAt",
                table: "SalesOrders",
                column: "PiiRedactedAt",
                filter: "\"PiiRedactedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_PurchaseDate",
                table: "SalesOrders",
                columns: new[] { "TenantId", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_Status",
                table: "SalesOrders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderStatusHistory_SalesOrderId_RecordedAt",
                table: "SalesOrderStatusHistory",
                columns: new[] { "SalesOrderId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesOrderLines");

            migrationBuilder.DropTable(
                name: "SalesOrderStatusHistory");

            migrationBuilder.DropTable(
                name: "SalesOrders");
        }
    }
}
