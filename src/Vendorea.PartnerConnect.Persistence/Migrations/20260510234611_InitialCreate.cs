using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KeyPrefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UsageCount = table.Column<long>(type: "bigint", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RateLimitPerMinute = table.Column<int>(type: "int", nullable: true),
                    AllowedIps = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedProperties = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DealerId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MonthlyPriceCents = table.Column<long>(type: "bigint", nullable: false),
                    AnnualPriceCents = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IncludedDocuments = table.Column<int>(type: "int", nullable: false),
                    OverageDocumentPriceCents = table.Column<long>(type: "bigint", nullable: false),
                    IncludedApiCalls = table.Column<int>(type: "int", nullable: false),
                    OverageApiCallPriceCents = table.Column<long>(type: "bigint", nullable: false),
                    IncludedStorageGb = table.Column<int>(type: "int", nullable: false),
                    OverageStoragePriceCents = table.Column<long>(type: "bigint", nullable: false),
                    MaxConnections = table.Column<int>(type: "int", nullable: false),
                    MaxWebhooks = table.Column<int>(type: "int", nullable: false),
                    Features = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsTrial = table.Column<bool>(type: "bit", nullable: false),
                    TrialDays = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentSyncJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    SyncType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalProducts = table.Column<int>(type: "int", nullable: false),
                    ProcessedProducts = table.Column<int>(type: "int", nullable: false),
                    UpdatedProducts = table.Column<int>(type: "int", nullable: false),
                    NewImagesDownloaded = table.Column<int>(type: "int", nullable: false),
                    SkippedProducts = table.Column<int>(type: "int", nullable: false),
                    ErrorProducts = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TriggerSource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentSyncJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DealerOnboardingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PrimaryContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestedPlan = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DealerId = table.Column<int>(type: "int", nullable: true),
                    SubmitterIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubmitterUserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerOnboardingRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalDealers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PrimaryContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BillingPlanId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspensionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsEmailVerified = table.Column<bool>(type: "bit", nullable: false),
                    EmailVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerificationToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    VerificationTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalDealers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RelatedEntityId = table.Column<int>(type: "int", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingPartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PartnerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    MetricType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsAggregated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    MetricType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Granularity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    MinValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MaxValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AverageValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DealerId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Preferences = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Secret = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Events = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FilterCriteria = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CustomHeaders = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuccessCount = table.Column<int>(type: "int", nullable: false),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                    IsSuspended = table.Column<bool>(type: "bit", nullable: false),
                    SuspendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspensionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    BillingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BillingInterval = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrialEndAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
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
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
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
                name: "DealerPartnerConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    ExternalAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisconnectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSuccessfulSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CredentialsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerPartnerConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealerPartnerConnections_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartnerCapabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    Capability = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdapterType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EndpointUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProtocolType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FileFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PollingIntervalMinutes = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WebhookSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RelatedEntityId = table.Column<int>(type: "int", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Signature = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SubtotalCents = table.Column<long>(type: "bigint", nullable: false),
                    TaxCents = table.Column<long>(type: "bigint", nullable: false),
                    TotalCents = table.Column<long>(type: "bigint", nullable: false),
                    AmountPaidCents = table.Column<long>(type: "bigint", nullable: false),
                    AmountDueCents = table.Column<long>(type: "bigint", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HostedInvoiceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InvoicePdfUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
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
                name: "PartnerDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealerPartnerConnectionId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Received"),
                    ExternalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CanonicalStoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecordCount = table.Column<int>(type: "int", nullable: true),
                    ProcessedCount = table.Column<int>(type: "int", nullable: true),
                    ErrorCount = table.Column<int>(type: "int", nullable: true),
                    ErrorDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastStateChangeAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ParentDocumentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DealerPartnerConnectionId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerDocuments_DealerPartnerConnections_DealerPartnerConnectionId",
                        column: x => x.DealerPartnerConnectionId,
                        principalTable: "DealerPartnerConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartnerDocuments_DealerPartnerConnections_DealerPartnerConnectionId1",
                        column: x => x.DealerPartnerConnectionId1,
                        principalTable: "DealerPartnerConnections",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceCents = table.Column<long>(type: "bigint", nullable: false),
                    AmountCents = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                name: "DocumentFingerprints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealerPartnerConnectionId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StructuralHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OriginalDocumentId = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFingerprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentFingerprints_DealerPartnerConnections_DealerPartnerConnectionId",
                        column: x => x.DealerPartnerConnectionId,
                        principalTable: "DealerPartnerConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentFingerprints_PartnerDocuments_OriginalDocumentId",
                        column: x => x.OriginalDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentStateHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                    FromState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                name: "InventoryFeedBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    ProcessedItems = table.Column<int>(type: "int", nullable: false),
                    MatchedItems = table.Column<int>(type: "int", nullable: false),
                    UpdatedItems = table.Column<int>(type: "int", nullable: false),
                    SkippedItems = table.Column<int>(type: "int", nullable: false),
                    ErrorItems = table.Column<int>(type: "int", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                    DealerId = table.Column<int>(type: "int", nullable: false),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    ProcessedItems = table.Column<int>(type: "int", nullable: false),
                    MatchedItems = table.Column<int>(type: "int", nullable: false),
                    UpdatedItems = table.Column<int>(type: "int", nullable: false),
                    SkippedItems = table.Column<int>(type: "int", nullable: false),
                    ErrorItems = table.Column<int>(type: "int", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                name: "QuarantinedDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                    DealerPartnerConnectionId = table.Column<int>(type: "int", nullable: false),
                    QuarantinedFromState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ErrorDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    QuarantinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuarantinedDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuarantinedDocuments_DealerPartnerConnections_DealerPartnerConnectionId",
                        column: x => x.DealerPartnerConnectionId,
                        principalTable: "DealerPartnerConnections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuarantinedDocuments_PartnerDocuments_PartnerDocumentId",
                        column: x => x.PartnerDocumentId,
                        principalTable: "PartnerDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_DealerPartnerConnections_DealerId",
                table: "DealerPartnerConnections",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_DealerPartnerConnections_DealerId_TradingPartnerId",
                table: "DealerPartnerConnections",
                columns: new[] { "DealerId", "TradingPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DealerPartnerConnections_Status",
                table: "DealerPartnerConnections",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DealerPartnerConnections_TradingPartnerId",
                table: "DealerPartnerConnections",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFingerprints_Connection_Type_Hash",
                table: "DocumentFingerprints",
                columns: new[] { "DealerPartnerConnectionId", "DocumentType", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFingerprints_ContentHash",
                table: "DocumentFingerprints",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFingerprints_ExpiresAt",
                table: "DocumentFingerprints",
                column: "ExpiresAt",
                filter: "[ExpiresAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFingerprints_OriginalDocumentId",
                table: "DocumentFingerprints",
                column: "OriginalDocumentId");

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
                filter: "[DueDate] IS NOT NULL");

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
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SubscriptionId",
                table: "Invoices",
                column: "SubscriptionId");

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
                name: "IX_PartnerDocuments_ContentHash",
                table: "PartnerDocuments",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_CorrelationId",
                table: "PartnerDocuments",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId",
                table: "PartnerDocuments",
                column: "DealerPartnerConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId_ReceivedAt",
                table: "PartnerDocuments",
                columns: new[] { "DealerPartnerConnectionId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId_State",
                table: "PartnerDocuments",
                columns: new[] { "DealerPartnerConnectionId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_DealerPartnerConnectionId1",
                table: "PartnerDocuments",
                column: "DealerPartnerConnectionId1");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_ReceivedAt",
                table: "PartnerDocuments",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDocuments_State",
                table: "PartnerDocuments",
                column: "State");

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
                name: "IX_QuarantinedDocuments_DealerPartnerConnectionId",
                table: "QuarantinedDocuments",
                column: "DealerPartnerConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantinedDocuments_DealerPartnerConnectionId_QuarantinedAt",
                table: "QuarantinedDocuments",
                columns: new[] { "DealerPartnerConnectionId", "QuarantinedAt" });

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
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Status",
                table: "Subscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TrialEndAt",
                table: "Subscriptions",
                column: "TrialEndAt",
                filter: "[TrialEndAt] IS NOT NULL");

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
                filter: "[ExpiresAt] IS NOT NULL");

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
                filter: "[ExternalId] IS NOT NULL");

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
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ContentSyncJobs");

            migrationBuilder.DropTable(
                name: "DealerOnboardingRequests");

            migrationBuilder.DropTable(
                name: "DocumentFingerprints");

            migrationBuilder.DropTable(
                name: "DocumentStateHistory");

            migrationBuilder.DropTable(
                name: "ExternalDealers");

            migrationBuilder.DropTable(
                name: "InventoryFeedBatches");

            migrationBuilder.DropTable(
                name: "InvoiceLineItems");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "PartnerCapabilities");

            migrationBuilder.DropTable(
                name: "PriceFeedBatches");

            migrationBuilder.DropTable(
                name: "QuarantinedDocuments");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UsageRecords");

            migrationBuilder.DropTable(
                name: "UsageSummaries");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "WebhookDeliveries");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "PartnerDocuments");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WebhookSubscriptions");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "DealerPartnerConnections");

            migrationBuilder.DropTable(
                name: "BillingPlans");

            migrationBuilder.DropTable(
                name: "TradingPartners");
        }
    }
}
