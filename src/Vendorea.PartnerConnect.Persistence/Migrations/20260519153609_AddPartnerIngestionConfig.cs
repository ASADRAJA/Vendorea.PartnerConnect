using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerIngestionConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartnerIngestionConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FtpHost = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FtpPort = table.Column<int>(type: "int", nullable: false),
                    FtpUsername = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FtpPassword = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LocalDownloadPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DatabaseType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    EnableScheduledRun = table.Column<bool>(type: "bit", nullable: false),
                    ScheduledRunHourUtc = table.Column<int>(type: "int", nullable: false),
                    CheckIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    ConnectionTimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    BulkInsertBatchSize = table.Column<int>(type: "int", nullable: false),
                    CleanupAfterImport = table.Column<bool>(type: "bit", nullable: false),
                    UseAzureBlobStorage = table.Column<bool>(type: "bit", nullable: false),
                    AzureBlobConnectionString = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AzureBlobContainerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerIngestionConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerIngestionConfigs_PartnerCode",
                table: "PartnerIngestionConfigs",
                column: "PartnerCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerIngestionConfigs");
        }
    }
}
