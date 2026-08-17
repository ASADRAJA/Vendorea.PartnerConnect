using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spr");

            migrationBuilder.CreateTable(
                name: "AdminPortalUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminPortalUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Scopes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedIp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UsageCount = table.Column<long>(type: "bigint", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: true),
                    AllowedIps = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Metadata = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attributenames",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    attributeid = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attributenames", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OldValues = table.Column<string>(type: "text", nullable: true),
                    NewValues = table.Column<string>(type: "text", nullable: true),
                    ChangedProperties = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DealerId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MonthlyPriceCents = table.Column<long>(type: "bigint", nullable: false),
                    AnnualPriceCents = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IncludedDocuments = table.Column<int>(type: "integer", nullable: false),
                    OverageDocumentPriceCents = table.Column<long>(type: "bigint", nullable: false),
                    IncludedApiCalls = table.Column<int>(type: "integer", nullable: false),
                    OverageApiCallPriceCents = table.Column<long>(type: "bigint", nullable: false),
                    IncludedStorageGb = table.Column<int>(type: "integer", nullable: false),
                    OverageStoragePriceCents = table.Column<long>(type: "bigint", nullable: false),
                    MaxConnections = table.Column<int>(type: "integer", nullable: false),
                    MaxWebhooks = table.Column<int>(type: "integer", nullable: false),
                    Features = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsTrial = table.Column<bool>(type: "boolean", nullable: false),
                    TrialDays = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "category",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    categoryid = table.Column<string>(type: "text", nullable: true),
                    parentcategoryid = table.Column<string>(type: "text", nullable: true),
                    isactive = table.Column<string>(type: "text", nullable: true),
                    ordernumber = table.Column<string>(type: "text", nullable: true),
                    catlevel = table.Column<string>(type: "text", nullable: true),
                    displayorder = table.Column<string>(type: "text", nullable: true),
                    lastupdated = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categorydisplayattributes",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    headerid = table.Column<string>(type: "text", nullable: true),
                    categoryid = table.Column<string>(type: "text", nullable: true),
                    attributeid = table.Column<string>(type: "text", nullable: true),
                    isactive = table.Column<string>(type: "text", nullable: true),
                    templatetype = table.Column<string>(type: "text", nullable: true),
                    defaultdisplayorder = table.Column<string>(type: "text", nullable: true),
                    displayorder = table.Column<string>(type: "text", nullable: true),
                    lastupdated = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorydisplayattributes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categoryheader",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    headerid = table.Column<string>(type: "text", nullable: true),
                    categoryid = table.Column<string>(type: "text", nullable: true),
                    isactive = table.Column<string>(type: "text", nullable: true),
                    templatetype = table.Column<string>(type: "text", nullable: true),
                    defaultdisplayorder = table.Column<string>(type: "text", nullable: true),
                    displayorder = table.Column<string>(type: "text", nullable: true),
                    lastupdated = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categoryheader", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categorynames",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    categoryid = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorynames", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categorysearchattributes",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    categoryid = table.Column<string>(type: "text", nullable: true),
                    attributeid = table.Column<string>(type: "text", nullable: true),
                    isactive = table.Column<string>(type: "text", nullable: true),
                    ispreferred = table.Column<string>(type: "text", nullable: true),
                    lastupdated = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorysearchattributes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ContentSyncJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    SyncType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalProducts = table.Column<int>(type: "integer", nullable: false),
                    ProcessedProducts = table.Column<int>(type: "integer", nullable: false),
                    UpdatedProducts = table.Column<int>(type: "integer", nullable: false),
                    NewImagesDownloaded = table.Column<int>(type: "integer", nullable: false),
                    SkippedProducts = table.Column<int>(type: "integer", nullable: false),
                    ErrorProducts = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    TriggerSource = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentSyncJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DealerOnboardingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PrimaryContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedPlan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DealerId = table.Column<int>(type: "integer", nullable: true),
                    SubmitterIp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SubmitterUserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerOnboardingRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalDealers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PrimaryContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BillingPlanId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsEmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    EmailVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    VerificationTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalDealers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FtpIngestionRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Phase = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TriggeredBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FilesDownloaded = table.Column<int>(type: "integer", nullable: false),
                    BytesDownloaded = table.Column<long>(type: "bigint", nullable: false),
                    TablesImported = table.Column<int>(type: "integer", nullable: false),
                    RowsImported = table.Column<long>(type: "bigint", nullable: false),
                    ProductsTransformed = table.Column<int>(type: "integer", nullable: false),
                    CategoriesTransformed = table.Column<int>(type: "integer", nullable: false),
                    FeaturesTransformed = table.Column<int>(type: "integer", nullable: false),
                    RelationshipsTransformed = table.Column<int>(type: "integer", nullable: false),
                    SpecificationsTransformed = table.Column<int>(type: "integer", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FtpIngestionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "headernames",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    headerid = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_headernames", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "locales",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    localeid = table.Column<string>(type: "text", nullable: true),
                    isactive = table.Column<string>(type: "text", nullable: true),
                    languagecode = table.Column<string>(type: "text", nullable: true),
                    countrycode = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturer",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    manufacturerid = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    address1 = table.Column<string>(type: "text", nullable: true),
                    address2 = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    zip = table.Column<string>(type: "text", nullable: true),
                    url = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    fax = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: true),
                    lastupdated = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mapped_category",
                schema: "spr",
                columns: table => new
                {
                    productid = table.Column<string>(type: "text", nullable: false),
                    categoryid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapped_category", x => x.productid);
                });

            migrationBuilder.CreateTable(
                name: "mapped_category_names",
                schema: "spr",
                columns: table => new
                {
                    categoryid = table.Column<string>(type: "text", nullable: false),
                    localeid = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapped_category_names", x => new { x.categoryid, x.localeid });
                });

            migrationBuilder.CreateTable(
                name: "mapped_category_taxonomy",
                schema: "spr",
                columns: table => new
                {
                    categoryid = table.Column<string>(type: "text", nullable: false),
                    parentcategoryid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapped_category_taxonomy", x => x.categoryid);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BillingPlanId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsMultiTenant = table.Column<bool>(type: "boolean", nullable: false),
                    PaymentTerms = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExternalPortalEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PortalBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PortalApiKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PortalApiKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "US"),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Destination = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RelatedEntityId = table.Column<int>(type: "integer", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartnerIngestionConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FtpHost = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FtpPort = table.Column<int>(type: "integer", nullable: false),
                    FtpUsername = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FtpPassword = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LocalDownloadPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DatabaseType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    EnableScheduledRun = table.Column<bool>(type: "boolean", nullable: false),
                    ScheduledRunHourUtc = table.Column<int>(type: "integer", nullable: false),
                    CheckIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    ConnectionTimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    BulkInsertBatchSize = table.Column<int>(type: "integer", nullable: false),
                    CleanupAfterImport = table.Column<bool>(type: "boolean", nullable: false),
                    UseAzureBlobStorage = table.Column<bool>(type: "boolean", nullable: false),
                    AzureBlobConnectionString = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AzureBlobContainerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerIngestionConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    manufacturerid = table.Column<string>(type: "text", nullable: true),
                    isactive = table.Column<string>(type: "text", nullable: true),
                    mfgpartno = table.Column<string>(type: "text", nullable: true),
                    categoryid = table.Column<string>(type: "text", nullable: true),
                    isaccessory = table.Column<string>(type: "text", nullable: true),
                    equivalency = table.Column<string>(type: "text", nullable: true),
                    creationdate = table.Column<string>(type: "text", nullable: true),
                    modifieddate = table.Column<string>(type: "text", nullable: true),
                    lastupdated = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productaccessories",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    accessoryproductid = table.Column<string>(type: "text", nullable: true),
                    isactive = table.Column<string>(type: "text", nullable: true),
                    ispreferred = table.Column<string>(type: "text", nullable: true),
                    isoption = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    recommendation_weight = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productaccessories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productattribute",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    attributeid = table.Column<string>(type: "text", nullable: true),
                    categoryid = table.Column<string>(type: "text", nullable: true),
                    displayvalue = table.Column<string>(type: "text", nullable: true),
                    absolutevalue = table.Column<string>(type: "text", nullable: true),
                    unitid = table.Column<string>(type: "text", nullable: true),
                    isabsolute = table.Column<string>(type: "text", nullable: true),
                    isactive = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productattribute", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productdescriptions",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    isdefault = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productdescriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productfeatures",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true),
                    sequenceno = table.Column<string>(type: "text", nullable: true),
                    bullettext = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productfeatures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productimages",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productimages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productkeywords",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    keywords = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productkeywords", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productlocales",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true),
                    isactive = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productlocales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productresources",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    skuname = table.Column<string>(type: "text", nullable: true),
                    sku = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true),
                    url = table.Column<string>(type: "text", nullable: true),
                    text = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true),
                    startdate = table.Column<string>(type: "text", nullable: true),
                    enddate = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productresources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productsimilar",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    similarproductid = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productsimilar", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productskus",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    sku = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true),
                    addeddate = table.Column<string>(type: "text", nullable: true),
                    discontinueddate = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productskus", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "productupsell",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    upsellproductid = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productupsell", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CronExpression = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: true),
                    NextDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LastRunDetail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "search_attribute",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    productid = table.Column<string>(type: "text", nullable: true),
                    attributeid = table.Column<string>(type: "text", nullable: true),
                    valueid = table.Column<string>(type: "text", nullable: true),
                    absolutevalue = table.Column<string>(type: "text", nullable: true),
                    isabsolute = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true)
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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    valueid = table.Column<string>(type: "text", nullable: true),
                    value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    absolutevalue = table.Column<string>(type: "text", nullable: true),
                    unitid = table.Column<string>(type: "text", nullable: true),
                    isabsolute = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_attribute_values", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "SprCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentCategoryId = table.Column<int>(type: "integer", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    FullPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UnspscCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "TradingPartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PartnerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    TenantConfirmationFieldsJson = table.Column<string>(type: "text", nullable: true),
                    TransportConfigJson = table.Column<string>(type: "text", nullable: true),
                    TransportCredentialsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "unitnames",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    unitid = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    localeid = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unitnames", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "units",
                schema: "spr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    unitid = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    baseunitid = table.Column<string>(type: "text", nullable: true),
                    multiple = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "UsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    MetricType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsAggregated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    MetricType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Granularity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    MinValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MaxValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AverageValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DealerId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Preferences = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Secret = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Events = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FilterCriteria = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CustomHeaders = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailureAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    BillingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BillingInterval = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentPeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrialEndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_BillingPlans_BillingPlanId",
                        column: x => x.BillingPlanId,
                        principalTable: "BillingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrgAccessRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedOrganizationIdentifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgAccessRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgAccessRequests_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgPortalUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Invited"),
                    AllTenants = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    FailedLoginAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgPortalUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgPortalUsers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgRegistrationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BillingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AdminDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AdminEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionByAdmin = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgRegistrationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgRegistrationRequests_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    ContactFirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactLastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduledJobId = table.Column<int>(type: "integer", nullable: false),
                    JobKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledJobRuns_ScheduledJobs_ScheduledJobId",
                        column: x => x.ScheduledJobId,
                        principalTable: "ScheduledJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealerContentSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    IsEnhancedContentEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SubscribedLocales = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EnabledContentTypes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastContentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastFullRefreshAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastContentUploadId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "OrganizationPartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationPartners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationPartners_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationPartners_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartnerCapabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    Capability = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: true),
                    AdapterType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EndpointUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProtocolType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FileFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PollingIntervalMinutes = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerCapabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerCapabilities_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartnerDistributionCenters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    DcNumber = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Area = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TollFreePhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Fax = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AdditionalContactInfo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerDistributionCenters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerDistributionCenters_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartnerDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Received"),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CanonicalStoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RecordCount = table.Column<int>(type: "integer", nullable: true),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: true),
                    ErrorCount = table.Column<int>(type: "integer", nullable: true),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStateChangeAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ParentDocumentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerDocuments_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceFeedUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PushedToMerchant360At = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                name: "SprContentUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    ContentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LocaleId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ZipFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ZipFileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ZipFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalProducts = table.Column<int>(type: "integer", nullable: false),
                    ProcessedProducts = table.Column<int>(type: "integer", nullable: false),
                    NewProducts = table.Column<int>(type: "integer", nullable: false),
                    UpdatedProducts = table.Column<int>(type: "integer", nullable: false),
                    SkippedProducts = table.Column<int>(type: "integer", nullable: false),
                    ErrorProducts = table.Column<int>(type: "integer", nullable: false),
                    ErrorDetails = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PushedToM360At = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    M360PushStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "None"),
                    M360PushClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    M360PushTotalProducts = table.Column<int>(type: "integer", nullable: false),
                    M360PushProductsPushed = table.Column<int>(type: "integer", nullable: false),
                    M360PushCurrentBatch = table.Column<int>(type: "integer", nullable: false),
                    M360PushTotalBatches = table.Column<int>(type: "integer", nullable: false),
                    M360PushError = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WebhookSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseBody = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RelatedEntityId = table.Column<int>(type: "integer", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Signature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookDeliveries_WebhookSubscriptions_WebhookSubscriptionId",
                        column: x => x.WebhookSubscriptionId,
                        principalTable: "WebhookSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SubtotalCents = table.Column<long>(type: "bigint", nullable: false),
                    TaxCents = table.Column<long>(type: "bigint", nullable: false),
                    TotalCents = table.Column<long>(type: "bigint", nullable: false),
                    AmountPaidCents = table.Column<long>(type: "bigint", nullable: false),
                    AmountDueCents = table.Column<long>(type: "bigint", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HostedInvoiceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InvoicePdfUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrgPortalUserTenants",
                columns: table => new
                {
                    OrgPortalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgPortalUserTenants", x => new { x.OrgPortalUserId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_OrgPortalUserTenants_OrgPortalUsers_OrgPortalUserId",
                        column: x => x.OrgPortalUserId,
                        principalTable: "OrgPortalUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgPortalUserTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgPortalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgPortalUserTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgPortalUserTokens_OrgPortalUsers_OrgPortalUserId",
                        column: x => x.OrgPortalUserId,
                        principalTable: "OrgPortalUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantPartnerAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true),
                    ExternalTenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContactFirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactLastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SpecialIdentifyingCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConfirmationFieldsJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CredentialsJson = table.Column<string>(type: "text", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPartnerAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantPartnerAccounts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantPartnerAccounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantPartnerAccounts_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentCorrelations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceDocumentId = table.Column<int>(type: "integer", nullable: false),
                    TargetDocumentId = table.Column<int>(type: "integer", nullable: false),
                    CorrelationType = table.Column<int>(type: "integer", nullable: false),
                    BusinessReference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
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
                name: "DocumentFingerprints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StructuralHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OriginalDocumentId = table.Column<int>(type: "integer", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFingerprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentFingerprints_PartnerDocuments_OriginalDocumentId",
                        column: x => x.OriginalDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentFingerprints_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentIdempotencyKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    KeyType = table.Column<int>(type: "integer", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeenCount = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentIdempotencyKeys", x => x.Id);
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
                name: "DocumentStateHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: false),
                    FromState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ToState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PerformedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentStateHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentStateHistory_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EdiDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: false),
                    TransactionSetCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    InterchangeControlNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GroupControlNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionControlNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SenderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReceiverId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SenderQualifier = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReceiverQualifier = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CanonicalType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CanonicalJson = table.Column<string>(type: "text", nullable: true),
                    ResponseDocumentId = table.Column<int>(type: "integer", nullable: true),
                    OriginalDocumentId = table.Column<int>(type: "integer", nullable: true),
                    AcknowledgmentGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgmentSent = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgmentSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RawEdiContent = table.Column<string>(type: "text", nullable: true),
                    BusinessReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LineItemCount = table.Column<int>(type: "integer", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ProcessingErrors = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdiDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EdiDocuments_EdiDocuments_OriginalDocumentId",
                        column: x => x.OriginalDocumentId,
                        principalTable: "EdiDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EdiDocuments_EdiDocuments_ResponseDocumentId",
                        column: x => x.ResponseDocumentId,
                        principalTable: "EdiDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EdiDocuments_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryFeedBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: false),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalItems = table.Column<int>(type: "integer", nullable: false),
                    ProcessedItems = table.Column<int>(type: "integer", nullable: false),
                    MatchedItems = table.Column<int>(type: "integer", nullable: false),
                    UpdatedItems = table.Column<int>(type: "integer", nullable: false),
                    SkippedItems = table.Column<int>(type: "integer", nullable: false),
                    ErrorItems = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorSummary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryFeedBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryFeedBatches_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceFeedBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: false),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalItems = table.Column<int>(type: "integer", nullable: false),
                    ProcessedItems = table.Column<int>(type: "integer", nullable: false),
                    MatchedItems = table.Column<int>(type: "integer", nullable: false),
                    UpdatedItems = table.Column<int>(type: "integer", nullable: false),
                    SkippedItems = table.Column<int>(type: "integer", nullable: false),
                    ErrorItems = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorSummary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceFeedBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceFeedBatches_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    IsRetryable = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MachineName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
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
                name: "QuarantinedDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    QuarantinedFromState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    QuarantinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Resolution = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuarantinedDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuarantinedDocuments_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuarantinedDocuments_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RawDocumentArchives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HashAlgorithm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageType = table.Column<int>(type: "integer", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    InlineContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsCompressed = table.Column<bool>(type: "boolean", nullable: false),
                    CompressionAlgorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetentionPolicy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EnterpriseCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BuyerOrganizationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SellerOrganizationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalOrderReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ManifestNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CanonicalType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CanonicalJson = table.Column<string>(type: "text", nullable: true),
                    ResponseDocumentId = table.Column<int>(type: "integer", nullable: true),
                    OriginalDocumentId = table.Column<int>(type: "integer", nullable: true),
                    AcknowledgmentReceived = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgmentReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RawXmlContent = table.Column<string>(type: "text", nullable: true),
                    BusinessReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LineItemCount = table.Column<int>(type: "integer", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ProcessingErrors = table.Column<string>(type: "text", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: true),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    SnapshotId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    InventoryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalItemCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedItemCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    NewItemCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedItemCount = table.Column<int>(type: "integer", nullable: false),
                    RemovedItemCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsFullRefresh = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousSnapshotId = table.Column<int>(type: "integer", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInventorySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInventorySnapshots_PartnerDocuments_PartnerDocument~",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierInventorySnapshots_SupplierInventorySnapshots_Previ~",
                        column: x => x.PreviousSnapshotId,
                        principalTable: "SupplierInventorySnapshots",
                        principalColumn: "Id");
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: true),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PoNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierOrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerAccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ShipToName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ShipToAddress1 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ShipToAddress2 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ShipToCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShipToState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShipToPostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ShipToCountry = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ShipToPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShipToEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BillToName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BillToAddress1 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BillToAddress2 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BillToCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BillToState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BillToPostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BillToCountry = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ShippingMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CarrierCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: true),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    ManifestNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BillOfLading = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CarrierCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CarrierName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShippingMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShipFromLocationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShipFromName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ShipFromAddress1 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ShipFromAddress2 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ShipFromCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShipFromState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShipFromPostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ShipFromCountry = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    TotalCartons = table.Column<int>(type: "integer", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    WeightUom = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    OrderCount = table.Column<int>(type: "integer", nullable: false),
                    TotalLineCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "SprPriceRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PriceFeedUploadId = table.Column<int>(type: "integer", nullable: false),
                    DealerId = table.Column<int>(type: "integer", nullable: false),
                    StockNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StockNumberStripped = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProductStatus = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NewItemNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SellingUnitOfMeasure = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GeneralLineCatalogPage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SpecialFlyerCatalogPage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FurnitureCatalogPage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PackingQuantity1 = table.Column<int>(type: "integer", nullable: false),
                    PackingUom1 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PackedPerUom1 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PackingQuantity2 = table.Column<int>(type: "integer", nullable: false),
                    PackingUom2 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PackedPerUom2 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PackingQuantity3 = table.Column<int>(type: "integer", nullable: false),
                    PackingUom3 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PackedPerUom3 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    WeightLbs = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    HeightInches = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    LengthInches = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    WidthInches = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    CategoryCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CountryOfOrigin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsReadyToAssemble = table.Column<bool>(type: "boolean", nullable: false),
                    IsRecycled = table.Column<bool>(type: "boolean", nullable: false),
                    CanShipUps = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    BrokenQuantitiesAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    RetailListPrice = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    RetailUnitOfMeasure = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RetailUnitsPerSuom = table.Column<int>(type: "integer", nullable: false),
                    MsdsRequired = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    RecommendedSubstitutions = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OldItemNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CatalogListPrice = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    CatalogUom = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MinorityVendorFlag = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsCustom = table.Column<bool>(type: "boolean", nullable: false),
                    IsDatedGoods = table.Column<bool>(type: "boolean", nullable: false),
                    QuantityPerSuom = table.Column<int>(type: "integer", nullable: false),
                    IsNonReturnable = table.Column<bool>(type: "boolean", nullable: false),
                    IsAlwaysNet = table.Column<bool>(type: "boolean", nullable: false),
                    IsSpecialOrder = table.Column<bool>(type: "boolean", nullable: false),
                    HarmonizedCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FreightRestricted = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SingleUsePlastic = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Upc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UnitedPrefixStockNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MpcNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MoorePrefixStockNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpcRetailPackFactor = table.Column<int>(type: "integer", nullable: false),
                    UpcRetailPack = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UpcIntermediatePackFactor = table.Column<int>(type: "integer", nullable: false),
                    UpcIntermediatePack = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UpcCasePackFactor = table.Column<int>(type: "integer", nullable: false),
                    UpcCasePack = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BranchStockingStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OldModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NewModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PricingProgramName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PricingProgramCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PricingStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PricingEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PricingFlyerPage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MinimumSellingQuantity = table.Column<int>(type: "integer", nullable: false),
                    NetCostNonCcp = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    NetCostCcp3 = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    NetCostCcp4 = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    VendorDropShipFlag = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ShippingLeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    AutoProcureFromVendor = table.Column<bool>(type: "boolean", nullable: false),
                    ProjectNumberRequired = table.Column<bool>(type: "boolean", nullable: false),
                    PromoLevel1Quantity = table.Column<int>(type: "integer", nullable: false),
                    PromoLevel1Cost = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    PromoLevel2Quantity = table.Column<int>(type: "integer", nullable: false),
                    PromoLevel2Cost = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    PromoLevel3Quantity = table.Column<int>(type: "integer", nullable: false),
                    PromoLevel3Cost = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    ConsumerPrice1Quantity = table.Column<int>(type: "integer", nullable: false),
                    ConsumerPrice1 = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    ConsumerPrice2Quantity = table.Column<int>(type: "integer", nullable: false),
                    ConsumerPrice2 = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    ConsumerPrice3Quantity = table.Column<int>(type: "integer", nullable: false),
                    ConsumerPrice3 = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    ShippingLeadTimeDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConsumerPriceInCatalog = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    CatalogPriceUom = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PriceCodeIdentifier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsFirmCost = table.Column<bool>(type: "boolean", nullable: false),
                    IsNetCost = table.Column<bool>(type: "boolean", nullable: false),
                    SourceLineNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "SprProductContent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentUploadId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LocaleId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StockNumberStripped = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Upc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BrandName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProductLine = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProductSeries = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description1 = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Description2 = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Description3 = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MarketingText = table.Column<string>(type: "text", nullable: true),
                    ManufacturerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ManufacturerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ManufacturerWebsite = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SprCategoryId = table.Column<int>(type: "integer", nullable: true),
                    SubClassName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubClassNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClassName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClassNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DepartmentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DepartmentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MasterDepartmentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MasterDepartmentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnspscCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CountryOfOrigin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RecycledPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    RecycledPcwPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    AssemblyRequired = table.Column<bool>(type: "boolean", nullable: true),
                    ImageUrl225 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageUrl75 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageUrl3 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Keywords = table.Column<string>(type: "text", nullable: true),
                    ContentVersionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceLineNumber = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "InvoiceLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceCents = table.Column<long>(type: "bigint", nullable: false),
                    AmountCents = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    TenantPartnerAccountId = table.Column<int>(type: "integer", nullable: false),
                    SourcePlatform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExternalOrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubmittedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalReferencesJson = table.Column<string>(type: "text", nullable: true),
                    OrderType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "WrapAndLabel"),
                    DistributionCenterCode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    AllowPartialShipment = table.Column<bool>(type: "boolean", nullable: false),
                    AllowBackorder = table.Column<bool>(type: "boolean", nullable: false),
                    AllowSubstitutions = table.Column<bool>(type: "boolean", nullable: false),
                    FulfillmentPreference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PoNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShipToJson = table.Column<string>(type: "text", nullable: true),
                    BillToJson = table.Column<string>(type: "text", nullable: true),
                    ShipFromJson = table.Column<string>(type: "text", nullable: true),
                    Attn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LabelCommentsJson = table.Column<string>(type: "text", nullable: true),
                    ShippingMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SubTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    EdiDocumentId = table.Column<int>(type: "integer", nullable: true),
                    AcknowledgmentDocumentId = table.Column<int>(type: "integer", nullable: true),
                    PartnerOrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShippedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_TenantPartnerAccounts_TenantPartnerAccountId",
                        column: x => x.TenantPartnerAccountId,
                        principalTable: "TenantPartnerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentValidationErrors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: false),
                    ProcessingAttemptId = table.Column<int>(type: "integer", nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpectedValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ActualValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FieldName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    Resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                        name: "FK_DocumentValidationErrors_ProcessingAttempts_ProcessingAttem~",
                        column: x => x.ProcessingAttemptId,
                        principalTable: "ProcessingAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierInventorySnapshotId = table.Column<int>(type: "integer", nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Upc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QuantityAvailable = table.Column<int>(type: "integer", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "integer", nullable: true),
                    QuantityAllocated = table.Column<int>(type: "integer", nullable: true),
                    QuantityOnOrder = table.Column<int>(type: "integer", nullable: true),
                    QuantityBackordered = table.Column<int>(type: "integer", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ListPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpectedAvailabilityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    MinimumOrderQuantity = table.Column<int>(type: "integer", nullable: true),
                    OrderMultiple = table.Column<int>(type: "integer", nullable: true),
                    IsDiscontinued = table.Column<bool>(type: "boolean", nullable: false),
                    IsHazmat = table.Column<bool>(type: "boolean", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    WeightUom = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInventoryItems_SupplierInventorySnapshots_SupplierI~",
                        column: x => x.SupplierInventorySnapshotId,
                        principalTable: "SupplierInventorySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierOrderAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: true),
                    SupplierPurchaseOrderId = table.Column<int>(type: "integer", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    PoNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierOrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AcknowledgementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpectedShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsUpdate = table.Column<bool>(type: "boolean", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierOrderAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierOrderAcknowledgements_PartnerDocuments_PartnerDocum~",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierOrderAcknowledgements_SupplierPurchaseOrders_Suppli~",
                        column: x => x.SupplierPurchaseOrderId,
                        principalTable: "SupplierPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierOrderAcknowledgements_TradingPartners_TradingPartne~",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPurchaseOrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierPurchaseOrderId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QuantityOrdered = table.Column<int>(type: "integer", nullable: false),
                    QuantityAcknowledged = table.Column<int>(type: "integer", nullable: true),
                    QuantityShipped = table.Column<int>(type: "integer", nullable: true),
                    QuantityBackordered = table.Column<int>(type: "integer", nullable: true),
                    QuantityCancelled = table.Column<int>(type: "integer", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ExtendedPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RequestedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPurchaseOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPurchaseOrderLines_SupplierPurchaseOrders_SupplierP~",
                        column: x => x.SupplierPurchaseOrderId,
                        principalTable: "SupplierPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCartons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierShipmentManifestId = table.Column<int>(type: "integer", nullable: false),
                    CartonNumber = table.Column<int>(type: "integer", nullable: false),
                    Sscc18 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PackageType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    WeightUom = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    Length = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Width = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Height = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DimensionUom = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    ItemCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCartons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCartons_SupplierShipmentManifests_SupplierShipmentM~",
                        column: x => x.SupplierShipmentManifestId,
                        principalTable: "SupplierShipmentManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: true),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    SupplierPurchaseOrderId = table.Column<int>(type: "integer", nullable: true),
                    SupplierShipmentManifestId = table.Column<int>(type: "integer", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PoNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierOrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HandlingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BalanceDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PaymentTermsDescription = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EarlyPaymentDiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    EarlyPaymentDiscountDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RemitToName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RemitToAddress1 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RemitToAddress2 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RemitToCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RemitToState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RemitToPostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RemitToCountry = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                        name: "FK_SupplierInvoices_SupplierPurchaseOrders_SupplierPurchaseOrd~",
                        column: x => x.SupplierPurchaseOrderId,
                        principalTable: "SupplierPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierInvoices_SupplierShipmentManifests_SupplierShipment~",
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierShipmentManifestId = table.Column<int>(type: "integer", nullable: false),
                    SupplierPurchaseOrderId = table.Column<int>(type: "integer", nullable: true),
                    PoNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierOrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShipToName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ShipToAddress1 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ShipToAddress2 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ShipToCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShipToState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShipToPostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ShipToCountry = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    TotalQuantityShipped = table.Column<int>(type: "integer", nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierShipmentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierShipmentOrders_SupplierPurchaseOrders_SupplierPurch~",
                        column: x => x.SupplierPurchaseOrderId,
                        principalTable: "SupplierPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierShipmentOrders_SupplierShipmentManifests_SupplierSh~",
                        column: x => x.SupplierShipmentManifestId,
                        principalTable: "SupplierShipmentManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SprProductFeatures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SprProductContentId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    BulletText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FeatureGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FeatureTypeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "SprProductRelationships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SprProductContentId = table.Column<long>(type: "bigint", nullable: false),
                    RelationshipType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RelatedProductId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RelatedSku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Score = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsBidirectional = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprProductRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprProductRelationships_SprProductContent_SprProductContent~",
                        column: x => x.SprProductContentId,
                        principalTable: "SprProductContent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SprProductSpecifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SprProductContentId = table.Column<long>(type: "bigint", nullable: false),
                    SpecificationsHtml = table.Column<string>(type: "text", nullable: false),
                    EstimatedCharCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprProductSpecifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprProductSpecifications_SprProductContent_SprProductConten~",
                        column: x => x.SprProductContentId,
                        principalTable: "SprProductContent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderAppliedShipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    ManifestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "OrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VendorSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "EA"),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AcknowledgedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ShippedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    BackorderedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    AcknowledgmentCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AcknowledgmentMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EstimatedShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EdiDocumentId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInventoryLocationQuantities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierInventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    LocationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LocationName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    QuantityAvailable = table.Column<int>(type: "integer", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "integer", nullable: true),
                    QuantityAllocated = table.Column<int>(type: "integer", nullable: true),
                    EstimatedShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransitDays = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInventoryLocationQuantities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInventoryLocationQuantities_SupplierInventoryItems_~",
                        column: x => x.SupplierInventoryItemId,
                        principalTable: "SupplierInventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierOrderAcknowledgementLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierOrderAcknowledgementId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QuantityOrdered = table.Column<int>(type: "integer", nullable: false),
                    QuantityAcknowledged = table.Column<int>(type: "integer", nullable: false),
                    QuantityBackordered = table.Column<int>(type: "integer", nullable: true),
                    QuantityRejected = table.Column<int>(type: "integer", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OrderedUnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpectedShipDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubstitutionSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubstitutionDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierOrderAcknowledgementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierOrderAcknowledgementLines_SupplierOrderAcknowledgem~",
                        column: x => x.SupplierOrderAcknowledgementId,
                        principalTable: "SupplierOrderAcknowledgements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCreditMemos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartnerDocumentId = table.Column<int>(type: "integer", nullable: true),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    SupplierInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    SupplierPurchaseOrderId = table.Column<int>(type: "integer", nullable: true),
                    CreditMemoNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalInvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PoNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreditMemoDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    ReasonDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RmaNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                        name: "FK_SupplierCreditMemos_SupplierPurchaseOrders_SupplierPurchase~",
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierShipmentOrderId = table.Column<int>(type: "integer", nullable: false),
                    SupplierPurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QuantityShipped = table.Column<int>(type: "integer", nullable: false),
                    QuantityOrdered = table.Column<int>(type: "integer", nullable: true),
                    QuantityBackordered = table.Column<int>(type: "integer", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SerialNumbers = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierShipmentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierShipmentLines_SupplierPurchaseOrderLines_SupplierPu~",
                        column: x => x.SupplierPurchaseOrderLineId,
                        principalTable: "SupplierPurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierShipmentLines_SupplierShipmentOrders_SupplierShipme~",
                        column: x => x.SupplierShipmentOrderId,
                        principalTable: "SupplierShipmentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCartonItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierCartonId = table.Column<int>(type: "integer", nullable: false),
                    SupplierShipmentLineId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Upc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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
                        name: "FK_SupplierCartonItems_SupplierShipmentLines_SupplierShipmentL~",
                        column: x => x.SupplierShipmentLineId,
                        principalTable: "SupplierShipmentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    SupplierPurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    SupplierShipmentLineId = table.Column<int>(type: "integer", nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QuantityInvoiced = table.Column<int>(type: "integer", nullable: false),
                    QuantityShipped = table.Column<int>(type: "integer", nullable: true),
                    QuantityOrdered = table.Column<int>(type: "integer", nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ExtendedPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PoLineNumber = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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
                        name: "FK_SupplierInvoiceLines_SupplierPurchaseOrderLines_SupplierPur~",
                        column: x => x.SupplierPurchaseOrderLineId,
                        principalTable: "SupplierPurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierInvoiceLines_SupplierShipmentLines_SupplierShipment~",
                        column: x => x.SupplierShipmentLineId,
                        principalTable: "SupplierShipmentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCreditMemoLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierCreditMemoId = table.Column<int>(type: "integer", nullable: false),
                    SupplierInvoiceLineId = table.Column<int>(type: "integer", nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    SupplierSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Upc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QuantityCredited = table.Column<int>(type: "integer", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ExtendedCredit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxCredit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineReason = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCreditMemoLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCreditMemoLines_SupplierCreditMemos_SupplierCreditM~",
                        column: x => x.SupplierCreditMemoId,
                        principalTable: "SupplierCreditMemos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierCreditMemoLines_SupplierInvoiceLines_SupplierInvoic~",
                        column: x => x.SupplierInvoiceLineId,
                        principalTable: "SupplierInvoiceLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "BillingPlans",
                columns: new[] { "Id", "AnnualPriceCents", "Code", "CreatedAt", "Currency", "Description", "ExternalId", "Features", "IncludedApiCalls", "IncludedDocuments", "IncludedStorageGb", "IsActive", "IsTrial", "MaxConnections", "MaxWebhooks", "MonthlyPriceCents", "Name", "OverageApiCallPriceCents", "OverageDocumentPriceCents", "OverageStoragePriceCents", "SortOrder", "TrialDays", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), null, "trial", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "14-day free trial with limited features", null, "[]", 1000, 100, 1, true, true, 1, 2, 0L, "Free Trial", 0L, 0L, 0L, 0, 14, null },
                    { new Guid("30000000-0000-0000-0000-000000000002"), 99000L, "starter", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "For small dealers getting started with EDI", null, "[]", 10000, 500, 5, true, false, 3, 5, 9900L, "Starter", 1L, 10L, 500L, 1, null, null },
                    { new Guid("30000000-0000-0000-0000-000000000003"), 299000L, "professional", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "For growing dealers with multiple partners", null, "[]", 50000, 2500, 25, true, false, 10, 20, 29900L, "Professional", 1L, 8L, 400L, 2, null, null },
                    { new Guid("30000000-0000-0000-0000-000000000004"), 999000L, "enterprise", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "USD", "For large dealers with high volume requirements", null, "[]", 500000, 15000, 100, true, false, 100, 100, 99900L, "Enterprise", 0L, 5L, 300L, 3, null, null }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "Code", "Description", "Name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Documents", "documents:read", "View documents and their details", "Read Documents" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Documents", "documents:write", "Create and update documents", "Write Documents" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Documents", "documents:delete", "Delete documents", "Delete Documents" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Documents", "documents:reprocess", "Reprocess failed or quarantined documents", "Reprocess Documents" },
                    { new Guid("10000000-0000-0000-0000-000000000011"), "Partners", "partners:read", "View trading partners", "Read Partners" },
                    { new Guid("10000000-0000-0000-0000-000000000012"), "Partners", "partners:write", "Create and update trading partners", "Write Partners" },
                    { new Guid("10000000-0000-0000-0000-000000000013"), "Partners", "partners:delete", "Delete trading partners", "Delete Partners" },
                    { new Guid("10000000-0000-0000-0000-000000000021"), "Connections", "connections:read", "View partner connections", "Read Connections" },
                    { new Guid("10000000-0000-0000-0000-000000000022"), "Connections", "connections:write", "Create and update partner connections", "Write Connections" },
                    { new Guid("10000000-0000-0000-0000-000000000023"), "Connections", "connections:delete", "Delete partner connections", "Delete Connections" },
                    { new Guid("10000000-0000-0000-0000-000000000031"), "Webhooks", "webhooks:read", "View webhook subscriptions", "Read Webhooks" },
                    { new Guid("10000000-0000-0000-0000-000000000032"), "Webhooks", "webhooks:write", "Create and update webhook subscriptions", "Write Webhooks" },
                    { new Guid("10000000-0000-0000-0000-000000000033"), "Webhooks", "webhooks:delete", "Delete webhook subscriptions", "Delete Webhooks" },
                    { new Guid("10000000-0000-0000-0000-000000000041"), "API Keys", "apikeys:read", "View API keys", "Read API Keys" },
                    { new Guid("10000000-0000-0000-0000-000000000042"), "API Keys", "apikeys:write", "Create API keys", "Write API Keys" },
                    { new Guid("10000000-0000-0000-0000-000000000043"), "API Keys", "apikeys:delete", "Revoke API keys", "Delete API Keys" },
                    { new Guid("10000000-0000-0000-0000-000000000051"), "Quarantine", "quarantine:read", "View quarantined documents", "Read Quarantine" },
                    { new Guid("10000000-0000-0000-0000-000000000052"), "Quarantine", "quarantine:process", "Retry or discard quarantined documents", "Process Quarantine" },
                    { new Guid("10000000-0000-0000-0000-000000000061"), "Usage", "usage:read", "View usage metrics", "Read Usage" },
                    { new Guid("10000000-0000-0000-0000-000000000062"), "Usage", "usage:export", "Export usage data", "Export Usage" },
                    { new Guid("10000000-0000-0000-0000-000000000071"), "Audit", "audit:read", "View audit logs", "Read Audit Logs" },
                    { new Guid("10000000-0000-0000-0000-000000000081"), "Admin", "admin:full", "Full administrative access to all features", "Full Admin Access" },
                    { new Guid("10000000-0000-0000-0000-000000000082"), "Admin", "admin:users", "Create, update, and delete users", "Manage Users" },
                    { new Guid("10000000-0000-0000-0000-000000000083"), "Admin", "admin:roles", "Create, update, and delete roles", "Manage Roles" },
                    { new Guid("10000000-0000-0000-0000-000000000084"), "Admin", "admin:billing", "View and manage billing", "Manage Billing" },
                    { new Guid("10000000-0000-0000-0000-000000000085"), "Admin", "admin:onboarding", "Approve or reject onboarding requests", "Manage Onboarding" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "IsSystemRole", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "system_admin", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Full system access with all permissions", true, true, "System Administrator", null },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "tenant_admin", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Manages users and settings for their tenant", true, true, "Tenant Administrator", null },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "dealer", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Standard dealer user with access to their documents and connections", true, true, "Dealer", null },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "operator", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Read-only access for monitoring and support", true, true, "Operator", null },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "external_api", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Limited API access for external integrations", true, true, "External API User", null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId", "AssignedAt", "AssignedBy" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000081"), new Guid("20000000-0000-0000-0000-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000011"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000021"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000022"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000031"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000032"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000033"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000041"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000042"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000043"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000051"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000052"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000061"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000062"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000071"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000082"), new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000021"), new Guid("20000000-0000-0000-0000-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000031"), new Guid("20000000-0000-0000-0000-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000032"), new Guid("20000000-0000-0000-0000-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000051"), new Guid("20000000-0000-0000-0000-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000061"), new Guid("20000000-0000-0000-0000-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000011"), new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000021"), new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000031"), new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000041"), new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000051"), new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000061"), new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000071"), new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000005"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000021"), new Guid("20000000-0000-0000-0000-000000000005"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("10000000-0000-0000-0000-000000000061"), new Guid("20000000-0000-0000-0000-000000000005"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminPortalUsers_Username",
                table: "AdminPortalUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_DealerId",
                table: "ApiKeys",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_DealerId_IsActive",
                table: "ApiKeys",
                columns: new[] { "DealerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyPrefix",
                table: "ApiKeys",
                column: "KeyPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_attributenames_attributeid",
                schema: "spr",
                table: "attributenames",
                column: "attributeid");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_DealerId",
                table: "AuditLogs",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingPlans_Code",
                table: "BillingPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingPlans_IsActive_SortOrder",
                table: "BillingPlans",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_category_parentcategoryid",
                schema: "spr",
                table: "category",
                column: "parentcategoryid");

            migrationBuilder.CreateIndex(
                name: "IX_categorydisplayattributes_attributeid",
                schema: "spr",
                table: "categorydisplayattributes",
                column: "attributeid");

            migrationBuilder.CreateIndex(
                name: "IX_categorydisplayattributes_categoryid",
                schema: "spr",
                table: "categorydisplayattributes",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_categoryheader_categoryid",
                schema: "spr",
                table: "categoryheader",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_categoryheader_headerid",
                schema: "spr",
                table: "categoryheader",
                column: "headerid");

            migrationBuilder.CreateIndex(
                name: "IX_categorynames_categoryid",
                schema: "spr",
                table: "categorynames",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_categorysearchattributes_attributeid",
                schema: "spr",
                table: "categorysearchattributes",
                column: "attributeid");

            migrationBuilder.CreateIndex(
                name: "IX_categorysearchattributes_categoryid",
                schema: "spr",
                table: "categorysearchattributes",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_ContentSyncJobs_DealerId",
                table: "ContentSyncJobs",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentSyncJobs_ScheduledAt",
                table: "ContentSyncJobs",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentSyncJobs_Status",
                table: "ContentSyncJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContentSyncJobs_TradingPartnerId",
                table: "ContentSyncJobs",
                column: "TradingPartnerId");

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

            migrationBuilder.CreateIndex(
                name: "IX_DealerOnboardingRequests_Email",
                table: "DealerOnboardingRequests",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_DealerOnboardingRequests_Status",
                table: "DealerOnboardingRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DealerOnboardingRequests_SubmittedAt",
                table: "DealerOnboardingRequests",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCorrelations_BusinessReference",
                table: "DocumentCorrelations",
                column: "BusinessReference");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCorrelations_SourceDocumentId",
                table: "DocumentCorrelations",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCorrelations_SourceDocumentId_TargetDocumentId_Corr~",
                table: "DocumentCorrelations",
                columns: new[] { "SourceDocumentId", "TargetDocumentId", "CorrelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCorrelations_TargetDocumentId",
                table: "DocumentCorrelations",
                column: "TargetDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFingerprints_ContentHash",
                table: "DocumentFingerprints",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFingerprints_ExpiresAt",
                table: "DocumentFingerprints",
                column: "ExpiresAt",
                filter: "\"ExpiresAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFingerprints_OriginalDocumentId",
                table: "DocumentFingerprints",
                column: "OriginalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFingerprints_Partner_Type_Hash",
                table: "DocumentFingerprints",
                columns: new[] { "TradingPartnerId", "DocumentType", "ContentHash" },
                unique: true);

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
                name: "IX_DocumentStateHistory_OccurredAt",
                table: "DocumentStateHistory",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentStateHistory_PartnerDocumentId",
                table: "DocumentStateHistory",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentStateHistory_PartnerDocumentId_OccurredAt",
                table: "DocumentStateHistory",
                columns: new[] { "PartnerDocumentId", "OccurredAt" });

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
                name: "IX_EdiDocuments_BusinessReference",
                table: "EdiDocuments",
                column: "BusinessReference");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_Direction",
                table: "EdiDocuments",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_InterchangeControlNumber",
                table: "EdiDocuments",
                column: "InterchangeControlNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_OriginalDocumentId",
                table: "EdiDocuments",
                column: "OriginalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_PartnerDocumentId",
                table: "EdiDocuments",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_ResponseDocumentId",
                table: "EdiDocuments",
                column: "ResponseDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_SenderId_ReceiverId",
                table: "EdiDocuments",
                columns: new[] { "SenderId", "ReceiverId" });

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_TransactionSetCode",
                table: "EdiDocuments",
                column: "TransactionSetCode");

            migrationBuilder.CreateIndex(
                name: "IX_EdiDocuments_TransactionSetCode_Direction",
                table: "EdiDocuments",
                columns: new[] { "TransactionSetCode", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDealers_CompanyName",
                table: "ExternalDealers",
                column: "CompanyName");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDealers_Email",
                table: "ExternalDealers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalDealers_Status",
                table: "ExternalDealers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FtpIngestionRuns_StartedAt",
                table: "FtpIngestionRuns",
                column: "StartedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_FtpIngestionRuns_Success",
                table: "FtpIngestionRuns",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_headernames_headerid",
                schema: "spr",
                table: "headernames",
                column: "headerid");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryFeedBatches_DealerId",
                table: "InventoryFeedBatches",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryFeedBatches_PartnerDocumentId",
                table: "InventoryFeedBatches",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryFeedBatches_ReceivedAt",
                table: "InventoryFeedBatches",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryFeedBatches_Status",
                table: "InventoryFeedBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryFeedBatches_TradingPartnerId",
                table: "InventoryFeedBatches",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_InvoiceId",
                table: "InvoiceLineItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DealerId",
                table: "Invoices",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DueDate",
                table: "Invoices",
                column: "DueDate",
                filter: "\"DueDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status_DueDate",
                table: "Invoices",
                columns: new[] { "Status", "DueDate" },
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SubscriptionId",
                table: "Invoices",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_mapped_category_categoryid",
                schema: "spr",
                table: "mapped_category",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_mapped_category_names_categoryid",
                schema: "spr",
                table: "mapped_category_names",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_mapped_category_taxonomy_parentcategoryid",
                schema: "spr",
                table: "mapped_category_taxonomy",
                column: "parentcategoryid");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAppliedShipments_OrderId_ManifestId",
                table: "OrderAppliedShipments",
                columns: new[] { "OrderId", "ManifestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId_LineNumber",
                table: "OrderLines",
                columns: new[] { "OrderId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_Sku",
                table: "OrderLines",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CorrelationId",
                table: "Orders",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderDate",
                table: "Orders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId",
                table: "Orders",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrganizationId_IdempotencyKey",
                table: "Orders",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PoNumber",
                table: "Orders",
                column: "PoNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SourcePlatform_ExternalOrderId",
                table: "Orders",
                columns: new[] { "SourcePlatform", "ExternalOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId",
                table: "Orders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_Status",
                table: "Orders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantPartnerAccountId",
                table: "Orders",
                column: "TenantPartnerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TradingPartnerId",
                table: "Orders",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_ChangedAt",
                table: "OrderStatusHistory",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderId",
                table: "OrderStatusHistory",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgAccessRequests_OrganizationId_Status",
                table: "OrgAccessRequests",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPartners_OrganizationId_TradingPartnerId",
                table: "OrganizationPartners",
                columns: new[] { "OrganizationId", "TradingPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPartners_TradingPartnerId",
                table: "OrganizationPartners",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Code",
                table: "Organizations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_PortalApiKeyHash",
                table: "Organizations",
                column: "PortalApiKeyHash",
                unique: true,
                filter: "\"PortalApiKeyHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Status",
                table: "Organizations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrgPortalUsers_OrganizationId_Email",
                table: "OrgPortalUsers",
                columns: new[] { "OrganizationId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgPortalUserTokens_OrgPortalUserId",
                table: "OrgPortalUserTokens",
                column: "OrgPortalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgPortalUserTokens_TokenHash",
                table: "OrgPortalUserTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_OrgRegistrationRequests_OrganizationId",
                table: "OrgRegistrationRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgRegistrationRequests_Status",
                table: "OrgRegistrationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CorrelationId",
                table: "OutboxMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CreatedAt",
                table: "OutboxMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Polling",
                table: "OutboxMessages",
                columns: new[] { "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status",
                table: "OutboxMessages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextRetryAt",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerCapabilities_TradingPartnerId_Capability",
                table: "PartnerCapabilities",
                columns: new[] { "TradingPartnerId", "Capability" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDistributionCenters_PostalCode",
                table: "PartnerDistributionCenters",
                column: "PostalCode");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDistributionCenters_TradingPartnerId_DcNumber",
                table: "PartnerDistributionCenters",
                columns: new[] { "TradingPartnerId", "DcNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_ContentHash",
                table: "PartnerDocuments",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_CorrelationId",
                table: "PartnerDocuments",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_ReceivedAt",
                table: "PartnerDocuments",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_State",
                table: "PartnerDocuments",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_TenantId",
                table: "PartnerDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_TradingPartnerId",
                table: "PartnerDocuments",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_TradingPartnerId_ReceivedAt",
                table: "PartnerDocuments",
                columns: new[] { "TradingPartnerId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_TradingPartnerId_State",
                table: "PartnerDocuments",
                columns: new[] { "TradingPartnerId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerIngestionConfigs_PartnerCode",
                table: "PartnerIngestionConfigs",
                column: "PartnerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Category",
                table: "Permissions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedBatches_DealerId",
                table: "PriceFeedBatches",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedBatches_PartnerDocumentId",
                table: "PriceFeedBatches",
                column: "PartnerDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedBatches_ReceivedAt",
                table: "PriceFeedBatches",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedBatches_Status",
                table: "PriceFeedBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedBatches_TradingPartnerId",
                table: "PriceFeedBatches",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_DealerId",
                table: "PriceFeedUploads",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_DealerId_TradingPartnerId_FileHash",
                table: "PriceFeedUploads",
                columns: new[] { "DealerId", "TradingPartnerId", "FileHash" },
                unique: true,
                filter: "\"Status\" IN ('Completed', 'PushedToMerchant360')");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_DealerId_TradingPartnerId_UploadedAt",
                table: "PriceFeedUploads",
                columns: new[] { "DealerId", "TradingPartnerId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_FileHash",
                table: "PriceFeedUploads",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_Status",
                table: "PriceFeedUploads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_TradingPartnerId",
                table: "PriceFeedUploads",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceFeedUploads_UploadedAt",
                table: "PriceFeedUploads",
                column: "UploadedAt");

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
                name: "IX_product_categoryid",
                schema: "spr",
                table: "product",
                column: "categoryid");

            migrationBuilder.CreateIndex(
                name: "IX_product_manufacturerid",
                schema: "spr",
                table: "product",
                column: "manufacturerid");

            migrationBuilder.CreateIndex(
                name: "IX_product_mfgpartno",
                schema: "spr",
                table: "product",
                column: "mfgpartno");

            migrationBuilder.CreateIndex(
                name: "IX_productaccessories_accessoryproductid",
                schema: "spr",
                table: "productaccessories",
                column: "accessoryproductid");

            migrationBuilder.CreateIndex(
                name: "IX_productaccessories_productid",
                schema: "spr",
                table: "productaccessories",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productattribute_attributeid",
                schema: "spr",
                table: "productattribute",
                column: "attributeid");

            migrationBuilder.CreateIndex(
                name: "IX_productattribute_productid",
                schema: "spr",
                table: "productattribute",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productdescriptions_productid",
                schema: "spr",
                table: "productdescriptions",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productfeatures_productid",
                schema: "spr",
                table: "productfeatures",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productimages_productid",
                schema: "spr",
                table: "productimages",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productkeywords_productid",
                schema: "spr",
                table: "productkeywords",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productlocales_productid",
                schema: "spr",
                table: "productlocales",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productresources_productid",
                schema: "spr",
                table: "productresources",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productsimilar_productid",
                schema: "spr",
                table: "productsimilar",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productsimilar_similarproductid",
                schema: "spr",
                table: "productsimilar",
                column: "similarproductid");

            migrationBuilder.CreateIndex(
                name: "IX_productskus_name",
                schema: "spr",
                table: "productskus",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_productskus_productid",
                schema: "spr",
                table: "productskus",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productskus_sku",
                schema: "spr",
                table: "productskus",
                column: "sku");

            migrationBuilder.CreateIndex(
                name: "IX_productupsell_productid",
                schema: "spr",
                table: "productupsell",
                column: "productid");

            migrationBuilder.CreateIndex(
                name: "IX_productupsell_upsellproductid",
                schema: "spr",
                table: "productupsell",
                column: "upsellproductid");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_PartnerDocumentId",
                table: "QuarantinedDocuments",
                column: "PartnerDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_QuarantinedAt",
                table: "QuarantinedDocuments",
                column: "QuarantinedAt");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_Reason",
                table: "QuarantinedDocuments",
                column: "Reason");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_Resolution",
                table: "QuarantinedDocuments",
                column: "Resolution");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_TenantId",
                table: "QuarantinedDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_TradingPartnerId",
                table: "QuarantinedDocuments",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_TradingPartnerId_QuarantinedAt",
                table: "QuarantinedDocuments",
                columns: new[] { "TradingPartnerId", "QuarantinedAt" });

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
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Code",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_IsActive",
                table: "Roles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobRuns_ScheduledJobId_StartedAt",
                table: "ScheduledJobRuns",
                columns: new[] { "ScheduledJobId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_JobKey",
                table: "ScheduledJobs",
                column: "JobKey",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_SprCategories_Active",
                table: "SprCategories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SprCategories_Code",
                table: "SprCategories",
                column: "CategoryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SprCategories_Level",
                table: "SprCategories",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SprCategories_Parent",
                table: "SprCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_Hash",
                table: "SprContentUploads",
                column: "ZipFileHash");

            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_Partner",
                table: "SprContentUploads",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_Partner_Locale_Version",
                table: "SprContentUploads",
                columns: new[] { "TradingPartnerId", "LocaleId", "ContentVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_Status",
                table: "SprContentUploads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SprContentUploads_UploadedAt",
                table: "SprContentUploads",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SprPriceRecords_DealerId_CategoryCode",
                table: "SprPriceRecords",
                columns: new[] { "DealerId", "CategoryCode" });

            migrationBuilder.CreateIndex(
                name: "IX_SprPriceRecords_DealerId_StockNumber",
                table: "SprPriceRecords",
                columns: new[] { "DealerId", "StockNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SprPriceRecords_DealerId_Upc",
                table: "SprPriceRecords",
                columns: new[] { "DealerId", "Upc" });

            migrationBuilder.CreateIndex(
                name: "IX_SprPriceRecords_PriceFeedUploadId",
                table: "SprPriceRecords",
                column: "PriceFeedUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Brand_Locale",
                table: "SprProductContent",
                columns: new[] { "BrandName", "LocaleId" });

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Category_Locale",
                table: "SprProductContent",
                columns: new[] { "SprCategoryId", "LocaleId" });

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_CreatedAt",
                table: "SprProductContent",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Product_Locale",
                table: "SprProductContent",
                columns: new[] { "ProductId", "LocaleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Sku_Locale",
                table: "SprProductContent",
                columns: new[] { "Sku", "LocaleId" });

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_StockNumberStripped_Locale",
                table: "SprProductContent",
                columns: new[] { "StockNumberStripped", "LocaleId" },
                filter: "\"StockNumberStripped\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Upc_Locale",
                table: "SprProductContent",
                columns: new[] { "Upc", "LocaleId" });

            migrationBuilder.CreateIndex(
                name: "IX_SprProductContent_Upload",
                table: "SprProductContent",
                column: "ContentUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_SprProductFeatures_Content",
                table: "SprProductFeatures",
                column: "SprProductContentId");

            migrationBuilder.CreateIndex(
                name: "IX_SprProductFeatures_Content_Order",
                table: "SprProductFeatures",
                columns: new[] { "SprProductContentId", "SortOrder" });

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

            migrationBuilder.CreateIndex(
                name: "IX_SprProductSpecifications_Content",
                table: "SprProductSpecifications",
                column: "SprProductContentId",
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
                name: "IX_Subscriptions_BillingPlanId",
                table: "Subscriptions",
                column: "BillingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_CurrentPeriodEnd",
                table: "Subscriptions",
                column: "CurrentPeriodEnd");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_DealerId",
                table: "Subscriptions",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_DealerId_Status",
                table: "Subscriptions",
                columns: new[] { "DealerId", "Status" },
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Status",
                table: "Subscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TrialEndAt",
                table: "Subscriptions",
                column: "TrialEndAt",
                filter: "\"TrialEndAt\" IS NOT NULL");

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
                name: "IX_SupplierInventoryItems_SupplierInventorySnapshotId_Supplier~",
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
                name: "IX_SupplierInventoryLocationQuantities_SupplierInventoryItemId~",
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
                name: "IX_SupplierOrderAcknowledgementLines_SupplierOrderAcknowledge~1",
                table: "SupplierOrderAcknowledgementLines",
                columns: new[] { "SupplierOrderAcknowledgementId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOrderAcknowledgementLines_SupplierOrderAcknowledgem~",
                table: "SupplierOrderAcknowledgementLines",
                column: "SupplierOrderAcknowledgementId");

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
                name: "IX_SupplierOrderAcknowledgements_SupplierPurchaseOrderId_Seque~",
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
                name: "IX_SupplierPurchaseOrderLines_SupplierPurchaseOrderId_LineNumb~",
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

            migrationBuilder.CreateIndex(
                name: "IX_TenantPartnerAccounts_AccountNumber",
                table: "TenantPartnerAccounts",
                column: "AccountNumber");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPartnerAccounts_OrganizationId_ApprovalStatus",
                table: "TenantPartnerAccounts",
                columns: new[] { "OrganizationId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantPartnerAccounts_TenantId",
                table: "TenantPartnerAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPartnerAccounts_TenantId_TradingPartnerId_AccountNumb~",
                table: "TenantPartnerAccounts",
                columns: new[] { "TenantId", "TradingPartnerId", "AccountNumber" },
                unique: true,
                filter: "\"TenantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPartnerAccounts_TradingPartnerId",
                table: "TenantPartnerAccounts",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ExternalId",
                table: "Tenants",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OrganizationId",
                table: "Tenants",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OrganizationId_Code",
                table: "Tenants",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OrganizationId_ExternalId",
                table: "Tenants",
                columns: new[] { "OrganizationId", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                table: "Tenants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TradingPartners_Code",
                table: "TradingPartners",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradingPartners_Status",
                table: "TradingPartners",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_unitnames_unitid",
                schema: "spr",
                table: "unitnames",
                column: "unitid");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_DealerId",
                table: "UsageRecords",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_DealerId_MetricType_Timestamp",
                table: "UsageRecords",
                columns: new[] { "DealerId", "MetricType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_DealerId_Timestamp",
                table: "UsageRecords",
                columns: new[] { "DealerId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_IsAggregated",
                table: "UsageRecords",
                column: "IsAggregated");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_IsAggregated_Timestamp",
                table: "UsageRecords",
                columns: new[] { "IsAggregated", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_OrganizationId",
                table: "UsageRecords",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_OrganizationId_Timestamp",
                table: "UsageRecords",
                columns: new[] { "OrganizationId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_Timestamp",
                table: "UsageRecords",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_UsageSummaries_DealerId",
                table: "UsageSummaries",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageSummaries_DealerId_Period",
                table: "UsageSummaries",
                columns: new[] { "DealerId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageSummaries_OrganizationId",
                table: "UsageSummaries",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageSummaries_OrganizationId_Period",
                table: "UsageSummaries",
                columns: new[] { "OrganizationId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageSummaries_PeriodStart",
                table: "UsageSummaries",
                column: "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "UK_UsageSummaries_Unique",
                table: "UsageSummaries",
                columns: new[] { "DealerId", "MetricType", "Granularity", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_ExpiresAt",
                table: "UserRoles",
                column: "ExpiresAt",
                filter: "\"ExpiresAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DealerId",
                table: "Users",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DealerId_Status",
                table: "Users",
                columns: new[] { "DealerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalId",
                table: "Users",
                column: "ExternalId",
                filter: "\"ExternalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Status",
                table: "Users",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_CorrelationId",
                table: "WebhookDeliveries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_CreatedAt",
                table: "WebhookDeliveries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Status",
                table: "WebhookDeliveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Status_NextRetryAt",
                table: "WebhookDeliveries",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_SubscriptionId",
                table: "WebhookDeliveries",
                column: "WebhookSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_DealerId",
                table: "WebhookSubscriptions",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_DealerId_IsActive",
                table: "WebhookSubscriptions",
                columns: new[] { "DealerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_IsSuspended",
                table: "WebhookSubscriptions",
                column: "IsSuspended");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminPortalUsers");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "attributenames",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "category",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "categorydisplayattributes",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "categoryheader",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "categorynames",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "categorysearchattributes",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "ContentSyncJobs");

            migrationBuilder.DropTable(
                name: "DealerContentSubscriptions");

            migrationBuilder.DropTable(
                name: "DealerOnboardingRequests");

            migrationBuilder.DropTable(
                name: "DocumentCorrelations");

            migrationBuilder.DropTable(
                name: "DocumentFingerprints");

            migrationBuilder.DropTable(
                name: "DocumentIdempotencyKeys");

            migrationBuilder.DropTable(
                name: "DocumentStateHistory");

            migrationBuilder.DropTable(
                name: "DocumentValidationErrors");

            migrationBuilder.DropTable(
                name: "EdiDocuments");

            migrationBuilder.DropTable(
                name: "ExternalDealers");

            migrationBuilder.DropTable(
                name: "FtpIngestionRuns");

            migrationBuilder.DropTable(
                name: "headernames",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "InventoryFeedBatches");

            migrationBuilder.DropTable(
                name: "InvoiceLineItems");

            migrationBuilder.DropTable(
                name: "locales",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "manufacturer",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "mapped_category",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "mapped_category_names",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "mapped_category_taxonomy",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "OrderAppliedShipments");

            migrationBuilder.DropTable(
                name: "OrderLines");

            migrationBuilder.DropTable(
                name: "OrderStatusHistory");

            migrationBuilder.DropTable(
                name: "OrgAccessRequests");

            migrationBuilder.DropTable(
                name: "OrganizationPartners");

            migrationBuilder.DropTable(
                name: "OrgPortalUserTenants");

            migrationBuilder.DropTable(
                name: "OrgPortalUserTokens");

            migrationBuilder.DropTable(
                name: "OrgRegistrationRequests");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "PartnerCapabilities");

            migrationBuilder.DropTable(
                name: "PartnerDistributionCenters");

            migrationBuilder.DropTable(
                name: "PartnerIngestionConfigs");

            migrationBuilder.DropTable(
                name: "PriceFeedBatches");

            migrationBuilder.DropTable(
                name: "product",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productaccessories",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productattribute",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productdescriptions",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productfeatures",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productimages",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productkeywords",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productlocales",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productresources",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productsimilar",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productskus",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "productupsell",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "QuarantinedDocuments");

            migrationBuilder.DropTable(
                name: "RawDocumentArchives");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "ScheduledJobRuns");

            migrationBuilder.DropTable(
                name: "search_attribute",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "search_attribute_values",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "SprPriceRecords");

            migrationBuilder.DropTable(
                name: "SprProductFeatures");

            migrationBuilder.DropTable(
                name: "SprProductRelationships");

            migrationBuilder.DropTable(
                name: "SprProductSpecifications");

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
                name: "unitnames",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "units",
                schema: "spr");

            migrationBuilder.DropTable(
                name: "UsageRecords");

            migrationBuilder.DropTable(
                name: "UsageSummaries");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "WebhookDeliveries");

            migrationBuilder.DropTable(
                name: "ProcessingAttempts");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "OrgPortalUsers");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "ScheduledJobs");

            migrationBuilder.DropTable(
                name: "PriceFeedUploads");

            migrationBuilder.DropTable(
                name: "SprProductContent");

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
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WebhookSubscriptions");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "TenantPartnerAccounts");

            migrationBuilder.DropTable(
                name: "SprCategories");

            migrationBuilder.DropTable(
                name: "SprContentUploads");

            migrationBuilder.DropTable(
                name: "SupplierInvoices");

            migrationBuilder.DropTable(
                name: "SupplierShipmentLines");

            migrationBuilder.DropTable(
                name: "SupplierInventorySnapshots");

            migrationBuilder.DropTable(
                name: "BillingPlans");

            migrationBuilder.DropTable(
                name: "SupplierPurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "SupplierShipmentOrders");

            migrationBuilder.DropTable(
                name: "SupplierPurchaseOrders");

            migrationBuilder.DropTable(
                name: "SupplierShipmentManifests");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "PartnerDocuments");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "TradingPartners");
        }
    }
}
