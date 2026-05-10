using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Vendorea.PartnerConnect.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Trading Partners
        migrationBuilder.CreateTable(
            name: "TradingPartners",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                AdapterType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SupportedTransports = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                SupportedDocumentTypes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                WebsiteUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                SupportUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TradingPartners", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TradingPartners_Code",
            table: "TradingPartners",
            column: "Code",
            unique: true);

        // Dealer Partner Connections
        migrationBuilder.CreateTable(
            name: "DealerPartnerConnections",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DealerId = table.Column<int>(type: "int", nullable: false),
                TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                TransportType = table.Column<int>(type: "int", nullable: false),
                ConnectionDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastSyncStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                LastSyncError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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

        migrationBuilder.CreateIndex(
            name: "IX_DealerPartnerConnections_DealerId",
            table: "DealerPartnerConnections",
            column: "DealerId");

        migrationBuilder.CreateIndex(
            name: "IX_DealerPartnerConnections_TradingPartnerId",
            table: "DealerPartnerConnections",
            column: "TradingPartnerId");

        migrationBuilder.CreateIndex(
            name: "IX_DealerPartnerConnections_DealerId_TradingPartnerId",
            table: "DealerPartnerConnections",
            columns: new[] { "DealerId", "TradingPartnerId" });

        // Partner Documents
        migrationBuilder.CreateTable(
            name: "PartnerDocuments",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DealerPartnerConnectionId = table.Column<int>(type: "int", nullable: false),
                DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Direction = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PartnerDocuments", x => x.Id);
                table.ForeignKey(
                    name: "FK_PartnerDocuments_DealerPartnerConnections_DealerPartnerConnectionId",
                    column: x => x.DealerPartnerConnectionId,
                    principalTable: "DealerPartnerConnections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PartnerDocuments_DealerPartnerConnectionId",
            table: "PartnerDocuments",
            column: "DealerPartnerConnectionId");

        migrationBuilder.CreateIndex(
            name: "IX_PartnerDocuments_Status",
            table: "PartnerDocuments",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_PartnerDocuments_ContentHash",
            table: "PartnerDocuments",
            column: "ContentHash");

        // Document Fingerprints
        migrationBuilder.CreateTable(
            name: "DocumentFingerprints",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DealerPartnerConnectionId = table.Column<int>(type: "int", nullable: false),
                DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                OriginalDocumentId = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DocumentFingerprints", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentFingerprints_ContentHash",
            table: "DocumentFingerprints",
            column: "ContentHash");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentFingerprints_DealerPartnerConnectionId_DocumentType_ContentHash",
            table: "DocumentFingerprints",
            columns: new[] { "DealerPartnerConnectionId", "DocumentType", "ContentHash" });

        // Quarantined Documents
        migrationBuilder.CreateTable(
            name: "QuarantinedDocuments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                QuarantinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Resolution = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ResolutionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
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
            });

        migrationBuilder.CreateIndex(
            name: "IX_QuarantinedDocuments_PartnerDocumentId",
            table: "QuarantinedDocuments",
            column: "PartnerDocumentId");

        // Outbox Messages
        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MessageType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Destination = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                NextRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_Status",
            table: "OutboxMessages",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_NextRetryAt",
            table: "OutboxMessages",
            column: "NextRetryAt");

        // Webhook Subscriptions
        migrationBuilder.CreateTable(
            name: "WebhookSubscriptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DealerId = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Secret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                EventTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastDeliveryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SuccessfulDeliveries = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                FailedDeliveries = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                ConsecutiveFailures = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WebhookSubscriptions_DealerId",
            table: "WebhookSubscriptions",
            column: "DealerId");

        // Webhook Deliveries
        migrationBuilder.CreateTable(
            name: "WebhookDeliveries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WebhookSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                Attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                NextRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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

        migrationBuilder.CreateIndex(
            name: "IX_WebhookDeliveries_WebhookSubscriptionId",
            table: "WebhookDeliveries",
            column: "WebhookSubscriptionId");

        // Audit Logs
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<int>(type: "int", nullable: false),
                EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RequestPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                DealerId = table.Column<int>(type: "int", nullable: true),
                OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsSuccess = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                DurationMs = table.Column<int>(type: "int", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_EntityType_EntityId",
            table: "AuditLogs",
            columns: new[] { "EntityType", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_DealerId",
            table: "AuditLogs",
            column: "DealerId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_Timestamp",
            table: "AuditLogs",
            column: "Timestamp");

        // Usage Records
        migrationBuilder.CreateTable(
            name: "UsageRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DealerId = table.Column<int>(type: "int", nullable: false),
                MetricType = table.Column<int>(type: "int", nullable: false),
                Value = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ResourceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UsageRecords", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UsageRecords_DealerId_RecordedAt",
            table: "UsageRecords",
            columns: new[] { "DealerId", "RecordedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UsageRecords_MetricType_RecordedAt",
            table: "UsageRecords",
            columns: new[] { "MetricType", "RecordedAt" });

        // Usage Summaries
        migrationBuilder.CreateTable(
            name: "UsageSummaries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DealerId = table.Column<int>(type: "int", nullable: false),
                MetricType = table.Column<int>(type: "int", nullable: false),
                PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                TotalValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                Count = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UsageSummaries", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UsageSummaries_DealerId_MetricType_PeriodStart",
            table: "UsageSummaries",
            columns: new[] { "DealerId", "MetricType", "PeriodStart" });

        // API Keys
        migrationBuilder.CreateTable(
            name: "ApiKeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DealerId = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                KeyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                KeyPrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Scopes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RevokedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                RevokedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiKeys", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_DealerId",
            table: "ApiKeys",
            column: "DealerId");

        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_KeyHash",
            table: "ApiKeys",
            column: "KeyHash",
            unique: true);

        // External Dealers
        migrationBuilder.CreateTable(
            name: "ExternalDealers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DealerId = table.Column<int>(type: "int", nullable: false),
                CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                StripeCustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                OnboardedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExternalDealers", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExternalDealers_DealerId",
            table: "ExternalDealers",
            column: "DealerId",
            unique: true);

        // Dealer Onboarding Requests
        migrationBuilder.CreateTable(
            name: "DealerOnboardingRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                RequestedPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedDealerId = table.Column<int>(type: "int", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RejectedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DealerOnboardingRequests", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DealerOnboardingRequests_ContactEmail",
            table: "DealerOnboardingRequests",
            column: "ContactEmail");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DealerOnboardingRequests");
        migrationBuilder.DropTable(name: "ExternalDealers");
        migrationBuilder.DropTable(name: "ApiKeys");
        migrationBuilder.DropTable(name: "UsageSummaries");
        migrationBuilder.DropTable(name: "UsageRecords");
        migrationBuilder.DropTable(name: "AuditLogs");
        migrationBuilder.DropTable(name: "WebhookDeliveries");
        migrationBuilder.DropTable(name: "WebhookSubscriptions");
        migrationBuilder.DropTable(name: "OutboxMessages");
        migrationBuilder.DropTable(name: "QuarantinedDocuments");
        migrationBuilder.DropTable(name: "DocumentFingerprints");
        migrationBuilder.DropTable(name: "PartnerDocuments");
        migrationBuilder.DropTable(name: "DealerPartnerConnections");
        migrationBuilder.DropTable(name: "TradingPartners");
    }
}
