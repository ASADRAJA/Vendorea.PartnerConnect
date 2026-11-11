using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationEvents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EventVersion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    IntegrationConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PublicationReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EnvelopeJson = table.Column<string>(type: "text", nullable: false),
                    SigningKeyId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryAcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastDeliveryAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDeliveryError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProcessingResult = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ProcessingResultAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsumerOrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProcessingError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationEvents", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_IntegrationEvents_IntegrationConnections_IntegrationConnect~",
                        column: x => x.IntegrationConnectionId,
                        principalTable: "IntegrationConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntegrationEvents_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_AwaitingAcknowledgement",
                table: "IntegrationEvents",
                column: "PublishedAt",
                filter: "\"DeliveryAcknowledgedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_AwaitingProcessingResult",
                table: "IntegrationEvents",
                column: "DeliveryAcknowledgedAt",
                filter: "\"DeliveryAcknowledgedAt\" IS NOT NULL AND \"ProcessingResult\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_DeduplicationKey",
                table: "IntegrationEvents",
                column: "DeduplicationKey");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_IntegrationConnectionId",
                table: "IntegrationEvents",
                column: "IntegrationConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_SalesOrderId_PublishedAt",
                table: "IntegrationEvents",
                columns: new[] { "SalesOrderId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_TenantId_PublishedAt",
                table: "IntegrationEvents",
                columns: new[] { "TenantId", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationEvents");
        }
    }
}
