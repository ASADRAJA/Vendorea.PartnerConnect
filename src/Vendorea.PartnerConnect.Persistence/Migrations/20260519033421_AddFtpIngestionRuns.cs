using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFtpIngestionRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FtpIngestionRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FilesDownloaded = table.Column<int>(type: "int", nullable: false),
                    BytesDownloaded = table.Column<long>(type: "bigint", nullable: false),
                    TablesImported = table.Column<int>(type: "int", nullable: false),
                    RowsImported = table.Column<long>(type: "bigint", nullable: false),
                    ProductsTransformed = table.Column<int>(type: "int", nullable: false),
                    CategoriesTransformed = table.Column<int>(type: "int", nullable: false),
                    FeaturesTransformed = table.Column<int>(type: "int", nullable: false),
                    RelationshipsTransformed = table.Column<int>(type: "int", nullable: false),
                    SpecificationsTransformed = table.Column<int>(type: "int", nullable: false),
                    Errors = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FtpIngestionRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FtpIngestionRuns_StartedAt",
                table: "FtpIngestionRuns",
                column: "StartedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_FtpIngestionRuns_Success",
                table: "FtpIngestionRuns",
                column: "Success");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FtpIngestionRuns");
        }
    }
}
