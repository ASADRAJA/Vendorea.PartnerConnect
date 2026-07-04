using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFtpIngestionRunStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phase",
                table: "FtpIngestionRuns",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FtpIngestionRuns",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Existing runs are historical. Any still-null CompletedAt were stranded, so mark Failed;
            // otherwise derive from Success.
            migrationBuilder.Sql("UPDATE FtpIngestionRuns SET Status = CASE WHEN CompletedAt IS NULL THEN 'Failed' WHEN Success = 1 THEN 'Succeeded' ELSE 'Failed' END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Phase",
                table: "FtpIngestionRuns");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FtpIngestionRuns");
        }
    }
}
