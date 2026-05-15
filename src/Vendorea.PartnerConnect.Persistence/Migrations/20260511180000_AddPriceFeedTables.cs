using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceFeedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceFeedUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PushedToMerchant360At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceFeedUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceFeedUploads_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SprPriceRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PriceFeedUploadId = table.Column<int>(type: "int", nullable: false),
                    DealerId = table.Column<int>(type: "int", nullable: false),

                    // Record Type I - Master Item
                    StockNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StockNumberStripped = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProductStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    NewItemNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SellingUnitOfMeasure = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    GeneralLineCatalogPage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SpecialFlyerCatalogPage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FurnitureCatalogPage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PackingQuantity1 = table.Column<int>(type: "int", nullable: false),
                    PackingUom1 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PackedPerUom1 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PackingQuantity2 = table.Column<int>(type: "int", nullable: false),
                    PackingUom2 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PackedPerUom2 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PackingQuantity3 = table.Column<int>(type: "int", nullable: false),
                    PackingUom3 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PackedPerUom3 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    WeightLbs = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    HeightInches = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    LengthInches = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    WidthInches = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CountryOfOrigin = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsReadyToAssemble = table.Column<bool>(type: "bit", nullable: false),
                    IsRecycled = table.Column<bool>(type: "bit", nullable: false),
                    CanShipUps = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    BrokenQuantitiesAllowed = table.Column<bool>(type: "bit", nullable: false),
                    RetailListPrice = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    RetailUnitOfMeasure = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RetailUnitsPerSuom = table.Column<int>(type: "int", nullable: false),
                    MsdsRequired = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    RecommendedSubstitutions = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OldItemNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CatalogListPrice = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    CatalogUom = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MinorityVendorFlag = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsCustom = table.Column<bool>(type: "bit", nullable: false),
                    IsDatedGoods = table.Column<bool>(type: "bit", nullable: false),
                    QuantityPerSuom = table.Column<int>(type: "int", nullable: false),
                    IsNonReturnable = table.Column<bool>(type: "bit", nullable: false),
                    IsAlwaysNet = table.Column<bool>(type: "bit", nullable: false),
                    IsSpecialOrder = table.Column<bool>(type: "bit", nullable: false),
                    HarmonizedCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FreightRestricted = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SingleUsePlastic = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),

                    // Record Type X - Cross Reference
                    Upc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UnitedPrefixStockNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MpcNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MoorePrefixStockNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpcRetailPackFactor = table.Column<int>(type: "int", nullable: false),
                    UpcRetailPack = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpcIntermediatePackFactor = table.Column<int>(type: "int", nullable: false),
                    UpcIntermediatePack = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpcCasePackFactor = table.Column<int>(type: "int", nullable: false),
                    UpcCasePack = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BranchStockingStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OldModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NewModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),

                    // Record Type P - Pricing
                    PricingProgramName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PricingProgramCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PricingStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PricingEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PricingFlyerPage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MinimumSellingQuantity = table.Column<int>(type: "int", nullable: false),
                    NetCostNonCcp = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    NetCostCcp3 = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    NetCostCcp4 = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    VendorDropShipFlag = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ShippingLeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    AutoProcureFromVendor = table.Column<bool>(type: "bit", nullable: false),
                    ProjectNumberRequired = table.Column<bool>(type: "bit", nullable: false),
                    PromoLevel1Quantity = table.Column<int>(type: "int", nullable: false),
                    PromoLevel1Cost = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    PromoLevel2Quantity = table.Column<int>(type: "int", nullable: false),
                    PromoLevel2Cost = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    PromoLevel3Quantity = table.Column<int>(type: "int", nullable: false),
                    PromoLevel3Cost = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    ConsumerPrice1Quantity = table.Column<int>(type: "int", nullable: false),
                    ConsumerPrice1 = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    ConsumerPrice2Quantity = table.Column<int>(type: "int", nullable: false),
                    ConsumerPrice2 = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    ConsumerPrice3Quantity = table.Column<int>(type: "int", nullable: false),
                    ConsumerPrice3 = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    ShippingLeadTimeDescription = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConsumerPriceInCatalog = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    CatalogPriceUom = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PriceCodeIdentifier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsFirmCost = table.Column<bool>(type: "bit", nullable: false),
                    IsNetCost = table.Column<bool>(type: "bit", nullable: false),

                    // Metadata
                    SourceLineNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprPriceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprPriceRecords_PriceFeedUploads_PriceFeedUploadId",
                        column: x => x.PriceFeedUploadId,
                        principalTable: "PriceFeedUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // PriceFeedUploads indexes
            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_DealerId",
                table: "PriceFeedUploads",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_TradingPartnerId",
                table: "PriceFeedUploads",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_Status",
                table: "PriceFeedUploads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_UploadedAt",
                table: "PriceFeedUploads",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_FileHash",
                table: "PriceFeedUploads",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_DealerId_TradingPartnerId_UploadedAt",
                table: "PriceFeedUploads",
                columns: new[] { "DealerId", "TradingPartnerId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_DealerId_TradingPartnerId_FileHash",
                table: "PriceFeedUploads",
                columns: new[] { "DealerId", "TradingPartnerId", "FileHash" },
                unique: true);

            // SprPriceRecords indexes
            migrationBuilder.CreateIndex(
                name: "IX_SprPriceRecords_DealerId_StockNumber",
                table: "SprPriceRecords",
                columns: new[] { "DealerId", "StockNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SprPriceRecords_PriceFeedUploadId",
                table: "SprPriceRecords",
                column: "PriceFeedUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_SprPriceRecords_DealerId_Upc",
                table: "SprPriceRecords",
                columns: new[] { "DealerId", "Upc" });

            migrationBuilder.CreateIndex(
                name: "IX_SprPriceRecords_DealerId_CategoryCode",
                table: "SprPriceRecords",
                columns: new[] { "DealerId", "CategoryCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SprPriceRecords");

            migrationBuilder.DropTable(
                name: "PriceFeedUploads");
        }
    }
}
