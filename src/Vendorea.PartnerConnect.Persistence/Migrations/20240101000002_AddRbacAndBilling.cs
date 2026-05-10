using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Vendorea.PartnerConnect.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRbacAndBilling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Permissions
        migrationBuilder.CreateTable(
            name: "Permissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Permissions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Permissions_Code",
            table: "Permissions",
            column: "Code",
            unique: true);

        // Roles
        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsSystemRole = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Code",
            table: "Roles",
            column: "Code",
            unique: true);

        // Role Permissions
        migrationBuilder.CreateTable(
            name: "RolePermissions",
            columns: table => new
            {
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                table.ForeignKey(
                    name: "FK_RolePermissions_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_RolePermissions_Permissions_PermissionId",
                    column: x => x.PermissionId,
                    principalTable: "Permissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_PermissionId",
            table: "RolePermissions",
            column: "PermissionId");

        // Users
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                DealerId = table.Column<int>(type: "int", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_DealerId",
            table: "Users",
            column: "DealerId");

        // User Roles
        migrationBuilder.CreateTable(
            name: "UserRoles",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                AssignedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_UserRoles_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserRoles_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_RoleId",
            table: "UserRoles",
            column: "RoleId");

        // Billing Plans
        migrationBuilder.CreateTable(
            name: "BillingPlans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                MonthlyPriceCents = table.Column<long>(type: "bigint", nullable: false),
                AnnualPriceCents = table.Column<long>(type: "bigint", nullable: true),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                IncludedDocuments = table.Column<int>(type: "int", nullable: false),
                OverageDocumentPriceCents = table.Column<long>(type: "bigint", nullable: false),
                IncludedApiCalls = table.Column<int>(type: "int", nullable: false),
                OverageApiCallPriceCents = table.Column<long>(type: "bigint", nullable: false),
                IncludedStorageGb = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                OverageStoragePriceCents = table.Column<long>(type: "bigint", nullable: false),
                MaxConnections = table.Column<int>(type: "int", nullable: false),
                MaxWebhooks = table.Column<int>(type: "int", nullable: false),
                Features = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                IsTrial = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                TrialDays = table.Column<int>(type: "int", nullable: true),
                DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                StripePriceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                StripeAnnualPriceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BillingPlans", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BillingPlans_Code",
            table: "BillingPlans",
            column: "Code",
            unique: true);

        // Subscriptions
        migrationBuilder.CreateTable(
            name: "Subscriptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DealerId = table.Column<int>(type: "int", nullable: false),
                BillingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                BillingInterval = table.Column<int>(type: "int", nullable: false),
                StripeSubscriptionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                StripeCustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CurrentPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                CurrentPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                TrialEndAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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

        migrationBuilder.CreateIndex(
            name: "IX_Subscriptions_DealerId",
            table: "Subscriptions",
            column: "DealerId");

        migrationBuilder.CreateIndex(
            name: "IX_Subscriptions_BillingPlanId",
            table: "Subscriptions",
            column: "BillingPlanId");

        // Invoices
        migrationBuilder.CreateTable(
            name: "Invoices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                DealerId = table.Column<int>(type: "int", nullable: false),
                SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                SubtotalCents = table.Column<long>(type: "bigint", nullable: false),
                TaxCents = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0),
                TotalCents = table.Column<long>(type: "bigint", nullable: false),
                AmountPaidCents = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0),
                AmountDueCents = table.Column<long>(type: "bigint", nullable: false),
                PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                StripeInvoiceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                HostedInvoiceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                InvoicePdfUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invoices", x => x.Id);
                table.ForeignKey(
                    name: "FK_Invoices_Subscriptions_SubscriptionId",
                    column: x => x.SubscriptionId,
                    principalTable: "Subscriptions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_InvoiceNumber",
            table: "Invoices",
            column: "InvoiceNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_DealerId",
            table: "Invoices",
            column: "DealerId");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_SubscriptionId",
            table: "Invoices",
            column: "SubscriptionId");

        // Invoice Line Items
        migrationBuilder.CreateTable(
            name: "InvoiceLineItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false),
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

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLineItems_InvoiceId",
            table: "InvoiceLineItems",
            column: "InvoiceId");

        // Additional tables from Phase 1
        // Partner Capability Configurations
        migrationBuilder.CreateTable(
            name: "PartnerCapabilities",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                Capability = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Configuration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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

        migrationBuilder.CreateIndex(
            name: "IX_PartnerCapabilities_TradingPartnerId",
            table: "PartnerCapabilities",
            column: "TradingPartnerId");

        // Price Feed Batches
        migrationBuilder.CreateTable(
            name: "PriceFeedBatches",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DealerPartnerConnectionId = table.Column<int>(type: "int", nullable: false),
                PartnerDocumentId = table.Column<int>(type: "int", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                TotalItems = table.Column<int>(type: "int", nullable: false),
                ProcessedItems = table.Column<int>(type: "int", nullable: false),
                FailedItems = table.Column<int>(type: "int", nullable: false),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PriceFeedBatches", x => x.Id);
            });

        // Inventory Feed Batches
        migrationBuilder.CreateTable(
            name: "InventoryFeedBatches",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DealerPartnerConnectionId = table.Column<int>(type: "int", nullable: false),
                PartnerDocumentId = table.Column<int>(type: "int", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                TotalItems = table.Column<int>(type: "int", nullable: false),
                ProcessedItems = table.Column<int>(type: "int", nullable: false),
                FailedItems = table.Column<int>(type: "int", nullable: false),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InventoryFeedBatches", x => x.Id);
            });

        // Content Sync Jobs
        migrationBuilder.CreateTable(
            name: "ContentSyncJobs",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DealerPartnerConnectionId = table.Column<int>(type: "int", nullable: false),
                JobType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DocumentsProcessed = table.Column<int>(type: "int", nullable: false),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentSyncJobs", x => x.Id);
            });

        // Document State History
        migrationBuilder.CreateTable(
            name: "DocumentStateHistory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PartnerDocumentId = table.Column<int>(type: "int", nullable: false),
                PreviousStatus = table.Column<int>(type: "int", nullable: false),
                NewStatus = table.Column<int>(type: "int", nullable: false),
                ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ChangedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
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

        migrationBuilder.CreateIndex(
            name: "IX_DocumentStateHistory_PartnerDocumentId",
            table: "DocumentStateHistory",
            column: "PartnerDocumentId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DocumentStateHistory");
        migrationBuilder.DropTable(name: "ContentSyncJobs");
        migrationBuilder.DropTable(name: "InventoryFeedBatches");
        migrationBuilder.DropTable(name: "PriceFeedBatches");
        migrationBuilder.DropTable(name: "PartnerCapabilities");
        migrationBuilder.DropTable(name: "InvoiceLineItems");
        migrationBuilder.DropTable(name: "Invoices");
        migrationBuilder.DropTable(name: "Subscriptions");
        migrationBuilder.DropTable(name: "BillingPlans");
        migrationBuilder.DropTable(name: "UserRoles");
        migrationBuilder.DropTable(name: "Users");
        migrationBuilder.DropTable(name: "RolePermissions");
        migrationBuilder.DropTable(name: "Roles");
        migrationBuilder.DropTable(name: "Permissions");
    }
}
