using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnhancedContentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SprCategories - Category hierarchy
            migrationBuilder.CreateTable(
                name: "SprCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentCategoryId = table.Column<int>(type: "int", nullable: true),
                    Level = table.Column<int>(type: "int", nullable: false),
                    FullPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UnspscCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprCategories_SprCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "SprCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // SprContentUploads - Content import tracking
            migrationBuilder.CreateTable(
                name: "SprContentUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    ContentVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LocaleId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ZipFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ZipFileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ZipFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalProducts = table.Column<int>(type: "int", nullable: false),
                    ProcessedProducts = table.Column<int>(type: "int", nullable: false),
                    NewProducts = table.Column<int>(type: "int", nullable: false),
                    UpdatedProducts = table.Column<int>(type: "int", nullable: false),
                    SkippedProducts = table.Column<int>(type: "int", nullable: false),
                    ErrorProducts = table.Column<int>(type: "int", nullable: false),
                    ErrorDetails = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprContentUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprContentUploads_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // DealerContentSubscriptions - Dealer opt-in control
            migrationBuilder.CreateTable(
                name: "DealerContentSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    IsEnhancedContentEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SubscribedLocales = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EnabledContentTypes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastContentVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastFullRefreshAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastContentUploadId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerContentSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealerContentSubscriptions_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // SprProductContent - Main product content entity
            migrationBuilder.CreateTable(
                name: "SprProductContent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    ContentUploadId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LocaleId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Upc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BrandName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProductLine = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProductSeries = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description1 = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Description2 = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Description3 = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MarketingText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ManufacturerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ManufacturerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufacturerWebsite = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SprCategoryId = table.Column<int>(type: "int", nullable: true),
                    SubClassName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubClassNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClassName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClassNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DepartmentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DepartmentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MasterDepartmentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MasterDepartmentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UnspscCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CountryOfOrigin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RecycledPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    RecycledPcwPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AssemblyRequired = table.Column<bool>(type: "bit", nullable: true),
                    ImageUrl225 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageUrl75 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageUrl3 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Keywords = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentVersionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceLineNumber = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprProductContent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprProductContent_SprCategories_SprCategoryId",
                        column: x => x.SprCategoryId,
                        principalTable: "SprCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SprProductContent_SprContentUploads_ContentUploadId",
                        column: x => x.ContentUploadId,
                        principalTable: "SprContentUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // SprProductSpecifications - HTML specs (separate for performance)
            migrationBuilder.CreateTable(
                name: "SprProductSpecifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SprProductContentId = table.Column<long>(type: "bigint", nullable: false),
                    SpecificationsHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedCharCount = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprProductSpecifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprProductSpecifications_SprProductContent_SprProductContentId",
                        column: x => x.SprProductContentId,
                        principalTable: "SprProductContent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // SprProductFeatures - Marketing bullet points
            migrationBuilder.CreateTable(
                name: "SprProductFeatures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SprProductContentId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    BulletText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FeatureGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FeatureTypeId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprProductFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprProductFeatures_SprProductContent_SprProductContentId",
                        column: x => x.SprProductContentId,
                        principalTable: "SprProductContent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // SprProductRelationships - Related products
            migrationBuilder.CreateTable(
                name: "SprProductRelationships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SprProductContentId = table.Column<long>(type: "bigint", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RelatedProductId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RelatedSku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Score = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsBidirectional = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprProductRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprProductRelationships_SprProductContent_SprProductContentId",
                        column: x => x.SprProductContentId,
                        principalTable: "SprProductContent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Indexes for SprCategories
            migrationBuilder.CreateIndex(
                name: "IX_SprCategories_Code",
                table: "SprCategories",
                column: "CategoryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SprCategories_Parent",
                table: "SprCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SprCategories_Level",
                table: "SprCategories",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SprCategories_Active",
                table: "SprCategories",
                column: "IsActive");

            // Indexes for SprContentUploads
            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_Dealer",
                table: "SprContentUploads",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_Partner",
                table: "SprContentUploads",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_Status",
                table: "SprContentUploads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_Hash",
                table: "SprContentUploads",
                column: "ZipFileHash");

            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_Dealer_Partner_Locale",
                table: "SprContentUploads",
                columns: new[] { "DealerId", "TradingPartnerId", "LocaleId" });

            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_UploadedAt",
                table: "SprContentUploads",
                column: "UploadedAt");

            // Indexes for DealerContentSubscriptions
            migrationBuilder.CreateIndex(
                name: "IX_DealerContentSubscriptions_Dealer_Partner",
                table: "DealerContentSubscriptions",
                columns: new[] { "DealerId", "TradingPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DealerContentSubscriptions_Enabled",
                table: "DealerContentSubscriptions",
                column: "IsEnhancedContentEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_DealerContentSubscriptions_TradingPartnerId",
                table: "DealerContentSubscriptions",
                column: "TradingPartnerId");

            // Indexes for SprProductContent
            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Dealer_Product_Locale",
                table: "SprProductContent",
                columns: new[] { "DealerId", "ProductId", "LocaleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Dealer_Sku",
                table: "SprProductContent",
                columns: new[] { "DealerId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Dealer_Upc",
                table: "SprProductContent",
                columns: new[] { "DealerId", "Upc" });

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Upload",
                table: "SprProductContent",
                column: "ContentUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Dealer_Category",
                table: "SprProductContent",
                columns: new[] { "DealerId", "SprCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Dealer_Brand",
                table: "SprProductContent",
                columns: new[] { "DealerId", "BrandName" });

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_CreatedAt",
                table: "SprProductContent",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_SprCategoryId",
                table: "SprProductContent",
                column: "SprCategoryId");

            // Indexes for SprProductSpecifications
            migrationBuilder.CreateIndex(
                name: "IX_SprProductSpecifications_Content",
                table: "SprProductSpecifications",
                column: "SprProductContentId",
                unique: true);

            // Indexes for SprProductFeatures
            migrationBuilder.CreateIndex(
                name: "IX_SprProductFeatures_Content",
                table: "SprProductFeatures",
                column: "SprProductContentId");

            migrationBuilder.CreateIndex(
                name: "IX_SprProductFeatures_Content_Order",
                table: "SprProductFeatures",
                columns: new[] { "SprProductContentId", "SortOrder" });

            // Indexes for SprProductRelationships
            migrationBuilder.CreateIndex(
                name: "IX_SprProductRelationships_Content",
                table: "SprProductRelationships",
                column: "SprProductContentId");

            migrationBuilder.CreateIndex(
                name: "IX_SprProductRelationships_Content_Type",
                table: "SprProductRelationships",
                columns: new[] { "SprProductContentId", "RelationshipType" });

            migrationBuilder.CreateIndex(
                name: "IX_SprProductRelationships_Content_Type_Order",
                table: "SprProductRelationships",
                columns: new[] { "SprProductContentId", "RelationshipType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SprProductRelationships_Related",
                table: "SprProductRelationships",
                column: "RelatedProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SprProductRelationships");
            migrationBuilder.DropTable(name: "SprProductFeatures");
            migrationBuilder.DropTable(name: "SprProductSpecifications");
            migrationBuilder.DropTable(name: "SprProductContent");
            migrationBuilder.DropTable(name: "DealerContentSubscriptions");
            migrationBuilder.DropTable(name: "SprContentUploads");
            migrationBuilder.DropTable(name: "SprCategories");
        }
    }
}
