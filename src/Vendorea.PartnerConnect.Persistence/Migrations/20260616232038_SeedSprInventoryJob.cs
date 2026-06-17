using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedSprInventoryJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed the SPR inventory import cron job. Idempotent. Enabled with a once-daily schedule
            // (staff can adjust the cron/timezone from the Cron Jobs tab). ConfigJson carries the
            // SPR FTP connection details (shared, non-secret onhand/onhand credentials).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM ScheduledJobs WHERE JobKey = 'spr-inventory')
INSERT INTO ScheduledJobs (JobKey, DisplayName, Description, CronExpression, TimeZoneId, IsEnabled, ConfigJson, CreatedAt)
VALUES (
    'spr-inventory',
    'SPR Inventory Import',
    'Imports SPR''s detailed per-DC on-hand inventory (sprfull.ezoh) over FTP and applies it as a full-refresh snapshot.',
    '0 6 * * *',
    'UTC',
    1,
    '{""FtpHost"":""ftp.sprich.com"",""FtpPort"":21,""FtpUsername"":""onhand"",""FtpPassword"":""onhand"",""RemoteFileName"":""sprfull.ezoh"",""ConnectionTimeoutSeconds"":60}',
    SYSUTCDATETIME());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ScheduledJobs WHERE JobKey = 'spr-inventory';");
        }
    }
}
