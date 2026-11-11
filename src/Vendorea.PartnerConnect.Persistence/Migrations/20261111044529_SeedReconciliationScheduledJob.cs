using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedReconciliationScheduledJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 04:15 UTC — after the three retention sweeps at 03:15/03:30/03:45, so the nightly
            // housekeeping does not all contend for a database shared with Merchant360.
            migrationBuilder.Sql("""
                INSERT INTO "ScheduledJobs"
                    ("JobKey", "DisplayName", "Description", "CronExpression", "TimeZoneId",
                     "IsEnabled", "CreatedAt")
                VALUES
                    ('sales-order-reconciliation',
                     'Marketplace order reconciliation',
                     'Re-reads stale non-terminal orders and searches a lagging window for orders the live path never ingested. Recovered orders indicate a problem in polling or notifications.',
                     '15 4 * * *', 'UTC', true, now())
                ON CONFLICT ("JobKey") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "ScheduledJobs" WHERE "JobKey" = 'sales-order-reconciliation';
                """);
        }
    }
}
