using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIntegrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowBackorder",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowPartialShipment",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowSubstitutions",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderId",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReferencesJson",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentPreference",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePlatform",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedBy",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentCorrelations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceDocumentId = table.Column<int>(type: "int", nullable: false),
                    TargetDocumentId = table.Column<int>(type: "int", nullable: false),
                    CorrelationType = table.Column<int>(type: "int", nullable: false),
                    BusinessReference = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCorrelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentCorrelations_PartnerDocuments_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentCorrelations_PartnerDocuments_TargetDocumentId",
                        column: x => x.TargetDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentIdempotencyKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    KeyType = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    DealerPartnerConnectionId = table.Column<int>(type: "int", nullable: true),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SeenCount = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentIdempotencyKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentIdempotencyKeys_DealerPartnerConnections_DealerPartnerConnectionId",
                        column: x => x.DealerPartnerConnectionId,
                        principalTable: "DealerPartnerConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentIdempotencyKeys_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentIdempotencyKeys_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ErrorDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRetryable = table.Column<bool>(type: "bit", nullable: false),
                    ProcessorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingAttempts_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RawDocumentArchives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HashAlgorithm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageType = table.Column<int>(type: "int", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    InlineContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    IsCompressed = table.Column<bool>(type: "bit", nullable: false),
                    CompressionAlgorithm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetentionPolicy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawDocumentArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawDocumentArchives_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SprXmlDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EnterpriseCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BuyerOrganizationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SellerOrganizationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalOrderReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManifestNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CanonicalType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CanonicalJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseDocumentId = table.Column<int>(type: "int", nullable: true),
                    OriginalDocumentId = table.Column<int>(type: "int", nullable: true),
                    AcknowledgmentReceived = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgmentReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawXmlContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LineItemCount = table.Column<int>(type: "int", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ProcessingErrors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprXmlDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprXmlDocuments_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SprXmlDocuments_SprXmlDocuments_OriginalDocumentId",
                        column: x => x.OriginalDocumentId,
                        principalTable: "SprXmlDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SprXmlDocuments_SprXmlDocuments_ResponseDocumentId",
                        column: x => x.ResponseDocumentId,
                        principalTable: "SprXmlDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInventorySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: true),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    SnapshotId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    InventoryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalItemCount = table.Column<int>(type: "int", nullable: false),
                    ProcessedItemCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    NewItemCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedItemCount = table.Column<int>(type: "int", nullable: false),
                    RemovedItemCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsFullRefresh = table.Column<bool>(type: "bit", nullable: false),
                    PreviousSnapshotId = table.Column<int>(type: "int", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInventorySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInventorySnapshots_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierInventorySnapshots_SupplierInventorySnapshots_PreviousSnapshotId",
                        column: x => x.PreviousSnapshotId,
                        principalTable: "SupplierInventorySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierInventorySnapshots_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: true),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PoNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SupplierOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomerAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedShipDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ShipToName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipToAddress1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipToAddress2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipToCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShipToState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShipToPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ShipToCountry = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ShipToPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShipToEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BillToName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BillToAddress1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BillToAddress2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BillToCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BillToState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BillToPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BillToCountry = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ShippingMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CarrierCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ShippingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPurchaseOrders_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierPurchaseOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPurchaseOrders_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierShipmentManifests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: true),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    ManifestNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BillOfLading = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShipDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CarrierCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShippingMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShipFromLocationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShipFromName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipFromAddress1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipFromAddress2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipFromCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShipFromState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShipFromPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ShipFromCountry = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    TotalCartons = table.Column<int>(type: "int", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    WeightUom = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    OrderCount = table.Column<int>(type: "int", nullable: false),
                    TotalLineCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierShipmentManifests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierShipmentManifests_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierShipmentManifests_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentValidationErrors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                    ProcessingAttemptId = table.Column<int>(type: "int", nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpectedValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActualValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FieldName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: true),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentValidationErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentValidationErrors_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentValidationErrors_ProcessingAttempts_ProcessingAttemptId",
                        column: x => x.ProcessingAttemptId,
                        principalTable: "ProcessingAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierInventorySnapshotId = table.Column<int>(type: "int", nullable: false),
                    SupplierSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Upc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuantityAvailable = table.Column<int>(type: "int", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "int", nullable: true),
                    QuantityAllocated = table.Column<int>(type: "int", nullable: true),
                    QuantityOnOrder = table.Column<int>(type: "int", nullable: true),
                    QuantityBackordered = table.Column<int>(type: "int", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ListPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpectedAvailabilityDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: true),
                    MinimumOrderQuantity = table.Column<int>(type: "int", nullable: true),
                    OrderMultiple = table.Column<int>(type: "int", nullable: true),
                    IsDiscontinued = table.Column<bool>(type: "bit", nullable: false),
                    IsHazmat = table.Column<bool>(type: "bit", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    WeightUom = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInventoryItems_SupplierInventorySnapshots_SupplierInventorySnapshotId",
                        column: x => x.SupplierInventorySnapshotId,
                        principalTable: "SupplierInventorySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierOrderAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: true),
                    SupplierPurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    PoNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SupplierOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AcknowledgementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpectedShipDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsUpdate = table.Column<bool>(type: "bit", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierOrderAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierOrderAcknowledgements_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierOrderAcknowledgements_SupplierPurchaseOrders_SupplierPurchaseOrderId",
                        column: x => x.SupplierPurchaseOrderId,
                        principalTable: "SupplierPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierOrderAcknowledgements_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPurchaseOrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierPurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    SupplierSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuantityOrdered = table.Column<int>(type: "int", nullable: false),
                    QuantityAcknowledged = table.Column<int>(type: "int", nullable: true),
                    QuantityShipped = table.Column<int>(type: "int", nullable: true),
                    QuantityBackordered = table.Column<int>(type: "int", nullable: true),
                    QuantityCancelled = table.Column<int>(type: "int", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ExtendedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RequestedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpectedShipDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPurchaseOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPurchaseOrderLines_SupplierPurchaseOrders_SupplierPurchaseOrderId",
                        column: x => x.SupplierPurchaseOrderId,
                        principalTable: "SupplierPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCartons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierShipmentManifestId = table.Column<int>(type: "int", nullable: false),
                    CartonNumber = table.Column<int>(type: "int", nullable: false),
                    Sscc18 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PackageType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    WeightUom = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Length = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DimensionUom = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    ItemCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCartons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCartons_SupplierShipmentManifests_SupplierShipmentManifestId",
                        column: x => x.SupplierShipmentManifestId,
                        principalTable: "SupplierShipmentManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: true),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    SupplierPurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    SupplierShipmentManifestId = table.Column<int>(type: "int", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PoNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShipDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HandlingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BalanceDue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PaymentTermsDescription = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EarlyPaymentDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    EarlyPaymentDiscountDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemitToName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RemitToAddress1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RemitToAddress2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RemitToCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RemitToState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RemitToPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RemitToCountry = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInvoices_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierInvoices_SupplierPurchaseOrders_SupplierPurchaseOrderId",
                        column: x => x.SupplierPurchaseOrderId,
                        principalTable: "SupplierPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierInvoices_SupplierShipmentManifests_SupplierShipmentManifestId",
                        column: x => x.SupplierShipmentManifestId,
                        principalTable: "SupplierShipmentManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierInvoices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierInvoices_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierShipmentOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierShipmentManifestId = table.Column<int>(type: "int", nullable: false),
                    SupplierPurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    PoNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SupplierOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShipToName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipToAddress1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipToAddress2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ShipToCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShipToState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShipToPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ShipToCountry = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    TotalQuantityShipped = table.Column<int>(type: "int", nullable: false),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierShipmentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierShipmentOrders_SupplierPurchaseOrders_SupplierPurchaseOrderId",
                        column: x => x.SupplierPurchaseOrderId,
                        principalTable: "SupplierPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierShipmentOrders_SupplierShipmentManifests_SupplierShipmentManifestId",
                        column: x => x.SupplierShipmentManifestId,
                        principalTable: "SupplierShipmentManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInventoryLocationQuantities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierInventoryItemId = table.Column<int>(type: "int", nullable: false),
                    LocationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LocationName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    QuantityAvailable = table.Column<int>(type: "int", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "int", nullable: true),
                    QuantityAllocated = table.Column<int>(type: "int", nullable: true),
                    EstimatedShipDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransitDays = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInventoryLocationQuantities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInventoryLocationQuantities_SupplierInventoryItems_SupplierInventoryItemId",
                        column: x => x.SupplierInventoryItemId,
                        principalTable: "SupplierInventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierOrderAcknowledgementLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierOrderAcknowledgementId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    SupplierSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuantityOrdered = table.Column<int>(type: "int", nullable: false),
                    QuantityAcknowledged = table.Column<int>(type: "int", nullable: false),
                    QuantityBackordered = table.Column<int>(type: "int", nullable: true),
                    QuantityRejected = table.Column<int>(type: "int", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OrderedUnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpectedShipDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubstitutionSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubstitutionDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierOrderAcknowledgementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierOrderAcknowledgementLines_SupplierOrderAcknowledgements_SupplierOrderAcknowledgementId",
                        column: x => x.SupplierOrderAcknowledgementId,
                        principalTable: "SupplierOrderAcknowledgements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCreditMemos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: true),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    SupplierInvoiceId = table.Column<int>(type: "int", nullable: true),
                    SupplierPurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    CreditMemoNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OriginalInvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PoNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreditMemoDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    ReasonDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RmaNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCreditMemos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCreditMemos_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierCreditMemos_SupplierInvoices_SupplierInvoiceId",
                        column: x => x.SupplierInvoiceId,
                        principalTable: "SupplierInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierCreditMemos_SupplierPurchaseOrders_SupplierPurchaseOrderId",
                        column: x => x.SupplierPurchaseOrderId,
                        principalTable: "SupplierPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierCreditMemos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCreditMemos_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierShipmentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierShipmentOrderId = table.Column<int>(type: "int", nullable: false),
                    SupplierPurchaseOrderLineId = table.Column<int>(type: "int", nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    SupplierSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuantityShipped = table.Column<int>(type: "int", nullable: false),
                    QuantityOrdered = table.Column<int>(type: "int", nullable: true),
                    QuantityBackordered = table.Column<int>(type: "int", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumbers = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierShipmentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierShipmentLines_SupplierPurchaseOrderLines_SupplierPurchaseOrderLineId",
                        column: x => x.SupplierPurchaseOrderLineId,
                        principalTable: "SupplierPurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierShipmentLines_SupplierShipmentOrders_SupplierShipmentOrderId",
                        column: x => x.SupplierShipmentOrderId,
                        principalTable: "SupplierShipmentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCartonItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierCartonId = table.Column<int>(type: "int", nullable: false),
                    SupplierShipmentLineId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    SupplierSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Upc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCartonItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCartonItems_SupplierCartons_SupplierCartonId",
                        column: x => x.SupplierCartonId,
                        principalTable: "SupplierCartons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierCartonItems_SupplierShipmentLines_SupplierShipmentLineId",
                        column: x => x.SupplierShipmentLineId,
                        principalTable: "SupplierShipmentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierInvoiceId = table.Column<int>(type: "int", nullable: false),
                    SupplierPurchaseOrderLineId = table.Column<int>(type: "int", nullable: true),
                    SupplierShipmentLineId = table.Column<int>(type: "int", nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    SupplierSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuantityInvoiced = table.Column<int>(type: "int", nullable: false),
                    QuantityShipped = table.Column<int>(type: "int", nullable: true),
                    QuantityOrdered = table.Column<int>(type: "int", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ExtendedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PoLineNumber = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInvoiceLines_SupplierInvoices_SupplierInvoiceId",
                        column: x => x.SupplierInvoiceId,
                        principalTable: "SupplierInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierInvoiceLines_SupplierPurchaseOrderLines_SupplierPurchaseOrderLineId",
                        column: x => x.SupplierPurchaseOrderLineId,
                        principalTable: "SupplierPurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierInvoiceLines_SupplierShipmentLines_SupplierShipmentLineId",
                        column: x => x.SupplierShipmentLineId,
                        principalTable: "SupplierShipmentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCreditMemoLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierCreditMemoId = table.Column<int>(type: "int", nullable: false),
                    SupplierInvoiceLineId = table.Column<int>(type: "int", nullable: true),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    SupplierSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuantityCredited = table.Column<int>(type: "int", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ExtendedCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineReason = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCreditMemoLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCreditMemoLines_SupplierCreditMemos_SupplierCreditMemoId",
                        column: x => x.SupplierCreditMemoId,
                        principalTable: "SupplierCreditMemos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierCreditMemoLines_SupplierInvoiceLines_SupplierInvoiceLineId",
                        column: x => x.SupplierInvoiceLineId,
                        principalTable: "SupplierInvoiceLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CorrelationId",
                table: "Orders",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_IdempotencyKey",
                table: "Orders",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SourcePlatform_ExternalOrderId",
                table: "Orders",
                columns: new[] { "SourcePlatform", "ExternalOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCorrelations_BusinessReference",
                table: "DocumentCorrelations",
                column: "BusinessReference");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCorrelations_SourceDocumentId",
                table: "DocumentCorrelations",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCorrelations_SourceDocumentId_TargetDocumentId_CorrelationType",
                table: "DocumentCorrelations",
                columns: new[] { "SourceDocumentId", "TargetDocumentId", "CorrelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCorrelations_TargetDocumentId",
                table: "DocumentCorrelations",
                column: "TargetDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIdempotencyKeys_DealerPartnerConnectionId",
                table: "DocumentIdempotencyKeys",
                column: "DealerPartnerConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIdempotencyKeys_ExpiresAt",
                table: "DocumentIdempotencyKeys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIdempotencyKeys_FirstSeenAt",
                table: "DocumentIdempotencyKeys",
                column: "FirstSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIdempotencyKeys_Key_TradingPartnerId_DocumentType",
                table: "DocumentIdempotencyKeys",
                columns: new[] { "Key", "TradingPartnerId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIdempotencyKeys_PartnerDocumentId",
                table: "DocumentIdempotencyKeys",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIdempotencyKeys_TradingPartnerId",
                table: "DocumentIdempotencyKeys",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentValidationErrors_Category",
                table: "DocumentValidationErrors",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentValidationErrors_IsResolved",
                table: "DocumentValidationErrors",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentValidationErrors_PartnerDocumentId",
                table: "DocumentValidationErrors",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentValidationErrors_ProcessingAttemptId",
                table: "DocumentValidationErrors",
                column: "ProcessingAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentValidationErrors_Severity",
                table: "DocumentValidationErrors",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingAttempts_PartnerDocumentId",
                table: "ProcessingAttempts",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingAttempts_PartnerDocumentId_AttemptNumber",
                table: "ProcessingAttempts",
                columns: new[] { "PartnerDocumentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingAttempts_Result",
                table: "ProcessingAttempts",
                column: "Result");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingAttempts_StartedAt",
                table: "ProcessingAttempts",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RawDocumentArchives_ArchivedAt",
                table: "RawDocumentArchives",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RawDocumentArchives_ContentHash",
                table: "RawDocumentArchives",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_RawDocumentArchives_ExpiresAt",
                table: "RawDocumentArchives",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RawDocumentArchives_PartnerDocumentId",
                table: "RawDocumentArchives",
                column: "PartnerDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_BuyerOrganizationCode_OrderNumber",
                table: "SprXmlDocuments",
                columns: new[] { "BuyerOrganizationCode", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_CreatedAt",
                table: "SprXmlDocuments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_Direction",
                table: "SprXmlDocuments",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_DocumentType",
                table: "SprXmlDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_DocumentType_Direction_ProcessingStatus",
                table: "SprXmlDocuments",
                columns: new[] { "DocumentType", "Direction", "ProcessingStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_InvoiceNumber",
                table: "SprXmlDocuments",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_ManifestNumber",
                table: "SprXmlDocuments",
                column: "ManifestNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_OrderNumber",
                table: "SprXmlDocuments",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_OriginalDocumentId",
                table: "SprXmlDocuments",
                column: "OriginalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_PartnerDocumentId",
                table: "SprXmlDocuments",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_ProcessingStatus",
                table: "SprXmlDocuments",
                column: "ProcessingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SprXmlDocuments_ResponseDocumentId",
                table: "SprXmlDocuments",
                column: "ResponseDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCartonItems_SupplierCartonId",
                table: "SupplierCartonItems",
                column: "SupplierCartonId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCartonItems_SupplierShipmentLineId",
                table: "SupplierCartonItems",
                column: "SupplierShipmentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCartonItems_SupplierSku",
                table: "SupplierCartonItems",
                column: "SupplierSku");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCartons_Sscc18",
                table: "SupplierCartons",
                column: "Sscc18");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCartons_SupplierShipmentManifestId",
                table: "SupplierCartons",
                column: "SupplierShipmentManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCartons_SupplierShipmentManifestId_CartonNumber",
                table: "SupplierCartons",
                columns: new[] { "SupplierShipmentManifestId", "CartonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCartons_TrackingNumber",
                table: "SupplierCartons",
                column: "TrackingNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemoLines_SupplierCreditMemoId",
                table: "SupplierCreditMemoLines",
                column: "SupplierCreditMemoId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemoLines_SupplierCreditMemoId_LineNumber",
                table: "SupplierCreditMemoLines",
                columns: new[] { "SupplierCreditMemoId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemoLines_SupplierInvoiceLineId",
                table: "SupplierCreditMemoLines",
                column: "SupplierInvoiceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemoLines_SupplierSku",
                table: "SupplierCreditMemoLines",
                column: "SupplierSku");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_CorrelationId",
                table: "SupplierCreditMemos",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_CreditMemoDate",
                table: "SupplierCreditMemos",
                column: "CreditMemoDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_CreditMemoNumber",
                table: "SupplierCreditMemos",
                column: "CreditMemoNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_OriginalInvoiceNumber",
                table: "SupplierCreditMemos",
                column: "OriginalInvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_PartnerDocumentId",
                table: "SupplierCreditMemos",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_Status",
                table: "SupplierCreditMemos",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_SupplierInvoiceId",
                table: "SupplierCreditMemos",
                column: "SupplierInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_SupplierPurchaseOrderId",
                table: "SupplierCreditMemos",
                column: "SupplierPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_TenantId",
                table: "SupplierCreditMemos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_TradingPartnerId",
                table: "SupplierCreditMemos",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditMemos_TradingPartnerId_CreditMemoNumber",
                table: "SupplierCreditMemos",
                columns: new[] { "TradingPartnerId", "CreditMemoNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventoryItems_Status",
                table: "SupplierInventoryItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventoryItems_SupplierInventorySnapshotId",
                table: "SupplierInventoryItems",
                column: "SupplierInventorySnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventoryItems_SupplierInventorySnapshotId_SupplierSku",
                table: "SupplierInventoryItems",
                columns: new[] { "SupplierInventorySnapshotId", "SupplierSku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventoryItems_SupplierSku",
                table: "SupplierInventoryItems",
                column: "SupplierSku");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventoryItems_Upc",
                table: "SupplierInventoryItems",
                column: "Upc");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventoryLocationQuantities_LocationCode",
                table: "SupplierInventoryLocationQuantities",
                column: "LocationCode");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventoryLocationQuantities_SupplierInventoryItemId",
                table: "SupplierInventoryLocationQuantities",
                column: "SupplierInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventoryLocationQuantities_SupplierInventoryItemId_LocationCode",
                table: "SupplierInventoryLocationQuantities",
                columns: new[] { "SupplierInventoryItemId", "LocationCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventorySnapshots_CorrelationId",
                table: "SupplierInventorySnapshots",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventorySnapshots_InventoryDate",
                table: "SupplierInventorySnapshots",
                column: "InventoryDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventorySnapshots_PartnerDocumentId",
                table: "SupplierInventorySnapshots",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventorySnapshots_PreviousSnapshotId",
                table: "SupplierInventorySnapshots",
                column: "PreviousSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventorySnapshots_ReceivedAt",
                table: "SupplierInventorySnapshots",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventorySnapshots_Status",
                table: "SupplierInventorySnapshots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventorySnapshots_TradingPartnerId",
                table: "SupplierInventorySnapshots",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInventorySnapshots_TradingPartnerId_SnapshotId",
                table: "SupplierInventorySnapshots",
                columns: new[] { "TradingPartnerId", "SnapshotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceLines_SupplierInvoiceId",
                table: "SupplierInvoiceLines",
                column: "SupplierInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceLines_SupplierInvoiceId_LineNumber",
                table: "SupplierInvoiceLines",
                columns: new[] { "SupplierInvoiceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceLines_SupplierPurchaseOrderLineId",
                table: "SupplierInvoiceLines",
                column: "SupplierPurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceLines_SupplierShipmentLineId",
                table: "SupplierInvoiceLines",
                column: "SupplierShipmentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceLines_SupplierSku",
                table: "SupplierInvoiceLines",
                column: "SupplierSku");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_CorrelationId",
                table: "SupplierInvoices",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_InvoiceDate",
                table: "SupplierInvoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_InvoiceNumber",
                table: "SupplierInvoices",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_PartnerDocumentId",
                table: "SupplierInvoices",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_PoNumber",
                table: "SupplierInvoices",
                column: "PoNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_Status",
                table: "SupplierInvoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_SupplierPurchaseOrderId",
                table: "SupplierInvoices",
                column: "SupplierPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_SupplierShipmentManifestId",
                table: "SupplierInvoices",
                column: "SupplierShipmentManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_TenantId",
                table: "SupplierInvoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_TradingPartnerId",
                table: "SupplierInvoices",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_TradingPartnerId_InvoiceNumber",
                table: "SupplierInvoices",
                columns: new[] { "TradingPartnerId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgementLines_SupplierOrderAcknowledgementId",
                table: "SupplierOrderAcknowledgementLines",
                column: "SupplierOrderAcknowledgementId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgementLines_SupplierOrderAcknowledgementId_LineNumber",
                table: "SupplierOrderAcknowledgementLines",
                columns: new[] { "SupplierOrderAcknowledgementId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgementLines_SupplierSku",
                table: "SupplierOrderAcknowledgementLines",
                column: "SupplierSku");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgements_AcknowledgementDate",
                table: "SupplierOrderAcknowledgements",
                column: "AcknowledgementDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgements_CorrelationId",
                table: "SupplierOrderAcknowledgements",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgements_PartnerDocumentId",
                table: "SupplierOrderAcknowledgements",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgements_PoNumber",
                table: "SupplierOrderAcknowledgements",
                column: "PoNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgements_SupplierPurchaseOrderId",
                table: "SupplierOrderAcknowledgements",
                column: "SupplierPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgements_SupplierPurchaseOrderId_Sequence",
                table: "SupplierOrderAcknowledgements",
                columns: new[] { "SupplierPurchaseOrderId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgements_TradingPartnerId",
                table: "SupplierOrderAcknowledgements",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrderLines_SupplierPurchaseOrderId",
                table: "SupplierPurchaseOrderLines",
                column: "SupplierPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrderLines_SupplierPurchaseOrderId_LineNumber",
                table: "SupplierPurchaseOrderLines",
                columns: new[] { "SupplierPurchaseOrderId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrderLines_SupplierSku",
                table: "SupplierPurchaseOrderLines",
                column: "SupplierSku");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrders_CorrelationId",
                table: "SupplierPurchaseOrders",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrders_OrderDate",
                table: "SupplierPurchaseOrders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrders_PartnerDocumentId",
                table: "SupplierPurchaseOrders",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrders_PoNumber",
                table: "SupplierPurchaseOrders",
                column: "PoNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrders_Status",
                table: "SupplierPurchaseOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrders_TenantId",
                table: "SupplierPurchaseOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrders_TradingPartnerId",
                table: "SupplierPurchaseOrders",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPurchaseOrders_TradingPartnerId_PoNumber",
                table: "SupplierPurchaseOrders",
                columns: new[] { "TradingPartnerId", "PoNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentLines_SupplierPurchaseOrderLineId",
                table: "SupplierShipmentLines",
                column: "SupplierPurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentLines_SupplierShipmentOrderId",
                table: "SupplierShipmentLines",
                column: "SupplierShipmentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentLines_SupplierShipmentOrderId_LineNumber",
                table: "SupplierShipmentLines",
                columns: new[] { "SupplierShipmentOrderId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentLines_SupplierSku",
                table: "SupplierShipmentLines",
                column: "SupplierSku");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentManifests_CorrelationId",
                table: "SupplierShipmentManifests",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentManifests_ManifestNumber",
                table: "SupplierShipmentManifests",
                column: "ManifestNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentManifests_PartnerDocumentId",
                table: "SupplierShipmentManifests",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentManifests_ShipDate",
                table: "SupplierShipmentManifests",
                column: "ShipDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentManifests_Status",
                table: "SupplierShipmentManifests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentManifests_TradingPartnerId",
                table: "SupplierShipmentManifests",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentManifests_TradingPartnerId_ManifestNumber",
                table: "SupplierShipmentManifests",
                columns: new[] { "TradingPartnerId", "ManifestNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentOrders_PoNumber",
                table: "SupplierShipmentOrders",
                column: "PoNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentOrders_SupplierPurchaseOrderId",
                table: "SupplierShipmentOrders",
                column: "SupplierPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierShipmentOrders_SupplierShipmentManifestId",
                table: "SupplierShipmentOrders",
                column: "SupplierShipmentManifestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentCorrelations");

            migrationBuilder.DropTable(
                name: "DocumentIdempotencyKeys");

            migrationBuilder.DropTable(
                name: "DocumentValidationErrors");

            migrationBuilder.DropTable(
                name: "RawDocumentArchives");

            migrationBuilder.DropTable(
                name: "SprXmlDocuments");

            migrationBuilder.DropTable(
                name: "SupplierCartonItems");

            migrationBuilder.DropTable(
                name: "SupplierCreditMemoLines");

            migrationBuilder.DropTable(
                name: "SupplierInventoryLocationQuantities");

            migrationBuilder.DropTable(
                name: "SupplierOrderAcknowledgementLines");

            migrationBuilder.DropTable(
                name: "ProcessingAttempts");

            migrationBuilder.DropTable(
                name: "SupplierCartons");

            migrationBuilder.DropTable(
                name: "SupplierCreditMemos");

            migrationBuilder.DropTable(
                name: "SupplierInvoiceLines");

            migrationBuilder.DropTable(
                name: "SupplierInventoryItems");

            migrationBuilder.DropTable(
                name: "SupplierOrderAcknowledgements");

            migrationBuilder.DropTable(
                name: "SupplierInvoices");

            migrationBuilder.DropTable(
                name: "SupplierShipmentLines");

            migrationBuilder.DropTable(
                name: "SupplierInventorySnapshots");

            migrationBuilder.DropTable(
                name: "SupplierPurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "SupplierShipmentOrders");

            migrationBuilder.DropTable(
                name: "SupplierPurchaseOrders");

            migrationBuilder.DropTable(
                name: "SupplierShipmentManifests");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CorrelationId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrganizationId_IdempotencyKey",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SourcePlatform_ExternalOrderId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AllowBackorder",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AllowPartialShipment",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AllowSubstitutions",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExternalOrderId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExternalReferencesJson",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentPreference",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SourcePlatform",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                table: "Orders");
        }
    }
}
