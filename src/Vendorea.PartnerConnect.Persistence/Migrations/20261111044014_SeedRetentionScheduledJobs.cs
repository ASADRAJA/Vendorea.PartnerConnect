using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedRetentionScheduledJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Handlers are bound to rows by JobKey, and a handler with no row never runs. The
            // retention sweeps are the whole point of this work, so they are seeded rather than
            // left for somebody to remember to create in the admin portal — which is precisely
            // how RawDocumentArchive ended up as a table nothing ever wrote to.
            //
            // ON CONFLICT DO NOTHING because the deployed database already holds rows created
            // through the app, and the identity column assigns its own ids. Re-running this is
            // harmless, and an operator who has since retuned a schedule keeps their change.
            migrationBuilder.Sql("""
                INSERT INTO "ScheduledJobs"
                    ("JobKey", "DisplayName", "Description", "CronExpression", "TimeZoneId",
                     "IsEnabled", "CreatedAt")
                VALUES
                    ('integration-oauth-state-purge',
                     'Integration OAuth state purge',
                     'Deletes expired marketplace authorization state tokens. Hygiene: expired states are already rejected.',
                     '15 3 * * *', 'UTC', true, now()),
                    ('integration-payload-archive-purge',
                     'Provider payload archive purge',
                     'Deletes redacted provider payload captures past their retention window.',
                     '30 3 * * *', 'UTC', true, now()),
                    ('buyer-data-retention',
                     'Buyer data retention sweep',
                     'Redacts buyer identity and street address on settled sales orders past the retention window, and on any order past the hard limit.',
                     '45 3 * * *', 'UTC', true, now())
                ON CONFLICT ("JobKey") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "ScheduledJobs"
                WHERE "JobKey" IN ('integration-oauth-state-purge',
                                   'integration-payload-archive-purge',
                                   'buyer-data-retention');
                """);
        }
    }
}
