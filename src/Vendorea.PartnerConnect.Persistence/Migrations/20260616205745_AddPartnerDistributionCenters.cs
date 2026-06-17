using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vendorea.PartnerConnect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerDistributionCenters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartnerDistributionCenters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    DcNumber = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TollFreePhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdditionalContactInfo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerDistributionCenters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerDistributionCenters_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDistributionCenters_PostalCode",
                table: "PartnerDistributionCenters",
                column: "PostalCode");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerDistributionCenters_TradingPartnerId_DcNumber",
                table: "PartnerDistributionCenters",
                columns: new[] { "TradingPartnerId", "DcNumber" },
                unique: true);

            // Seed SPR's distribution centers from SPRCP-00103 "DC Address List" (V2, 2026-03-16).
            // Resolved against SPR by Code (Id differs per environment) and idempotent. Region is
            // intentionally left NULL — SPR has not provided a region mapping yet. Uncertain source
            // values are preserved verbatim with a note in AdditionalContactInfo rather than dropped.
            migrationBuilder.Sql(@"
DECLARE @spr INT = (SELECT Id FROM TradingPartners WHERE Code = 'SPR');
IF @spr IS NOT NULL AND NOT EXISTS (SELECT 1 FROM PartnerDistributionCenters WHERE TradingPartnerId = @spr)
BEGIN
    INSERT INTO PartnerDistributionCenters
        (TradingPartnerId, DcNumber, Label, Area, ContactName, AddressLine1, AddressLine2, City, State, PostalCode, Phone, TollFreePhone, Fax, AdditionalContactInfo, CreatedAt)
    VALUES
    (@spr, 1, 'Atlanta, GA', 'Lithia Springs', 'Rocky Carter', '440 Interstate W. Parkway, Ste. 100', NULL, 'Lithia Springs', 'GA', '30122', '(770) 434-4571', '(800) 282-6271', NULL, NULL, SYSUTCDATETIME()),
    (@spr, 2, 'Charlotte, NC', 'Kannapolis/Concord', 'Trey Smith', '6280 Glen Afton Blvd.', NULL, 'Concord', 'NC', '28027', '(704) 372-2815', '(800) 438-4072', '(704) 792-0225', NULL, SYSUTCDATETIME()),
    (@spr, 5, 'Dallas, TX', 'Coppell', 'Todd Clark', '611 S. Royal Lane, Ste. 100', NULL, 'Coppell', 'TX', '75019', '(469) 568-6444', '(800) 442-7774', '(469) 568-6459', NULL, SYSUTCDATETIME()),
    (@spr, 6, 'Memphis, TN', NULL, 'Will Chaney', '5820 East Shelby Drive, Ste. 101', NULL, 'Memphis', 'TN', '38141', '(901) 367-1388', '(800) 866-1377', '(901) 367-1751', NULL, SYSUTCDATETIME()),
    (@spr, 7, 'Kansas City, MO', NULL, 'Steve Wilson', '8750 Elmwood Avenue, Ste. 400', NULL, 'Kansas City', 'MO', '64132', '(816) 471-5422', '(800) 821-2347', '(816) 472-1654', NULL, SYSUTCDATETIME()),
    (@spr, 8, 'Houston, TX', NULL, 'Larry Tzrinske', '6555 Pine Vista Lane', NULL, 'Houston', 'TX', '77092', '(713) 996-7072', '(800) 392-7878', '(713) 996-7108', NULL, SYSUTCDATETIME()),
    (@spr, 9, 'Columbus, OH', 'Lockbourne', 'Dan Elko', '1815 Beggrow St. Ste. 400', NULL, 'Lockbourne', 'OH', '43137', '(614) 497-2270', '(800) 848-0004', '(614) 497-1433', NULL, SYSUTCDATETIME()),
    (@spr, 10, 'St. Paul, MN', NULL, 'Zack Hampton', '2416 Maplewood Drive', NULL, 'St. Paul', 'MN', '55109', '(651) 484-8459', '(800) 328-9559', '(651) 484-2275', NULL, SYSUTCDATETIME()),
    (@spr, 11, 'Denver, CO', NULL, 'Emanuel Martinez', '11600 East 56th Avenue', NULL, 'Denver', 'CO', '80239', '(303) 573-6000', '(800) 848-3506', '(303) 595-8073', NULL, SYSUTCDATETIME()),
    (@spr, 12, 'Carol Stream, IL', 'Chicago', 'Fred Benitez', '810 Kimberly Drive', NULL, 'Carol Stream', 'IL', '60188', '(200) 035-3003', '(800) 858-8681', NULL, 'Phone as printed on SPR DC list — verify (area code 200 is invalid): (200) 035-3003', SYSUTCDATETIME()),
    (@spr, 13, 'Oklahoma City, OK', NULL, 'Phillip Wood', '1326 Enterprise Avenue', NULL, 'Oklahoma City', 'OK', '73128', '(405) 949-0765', '(800) 562-2823', NULL, NULL, SYSUTCDATETIME()),
    (@spr, 15, 'San Antonio, TX', 'Schertz', 'Dan Vargas', '6409 Tri-County Parkway', NULL, 'Schertz', 'TX', '78154', '(210) 651-9400', '(800) 848-3504', '(210) 651-9407', NULL, SYSUTCDATETIME()),
    (@spr, 16, 'Orlando, FL', NULL, 'Nicholas Hurley', '2405 Commerce Park Dr.', NULL, 'Orlando', 'FL', '32819', '(813) 280-8630', NULL, NULL, NULL, SYSUTCDATETIME()),
    (@spr, 18, 'St. Louis, MO', 'Earth City', 'Michael Steele', '3673 Corporate Trail Drive', NULL, 'Earth City', 'MO', '63045', '(314) 567-7726', '(800) 325-4677', '(314) 567-3061', NULL, SYSUTCDATETIME()),
    (@spr, 19, 'Baltimore, MD', 'Hanover', 'Dylan Harrison', '7441 Candlewood Rd.', NULL, 'Hanover', 'MD', '21076', '(410) 792-4625', '(800) 638-2335', '(410) 792-9219', 'Baltimore Area phone: (410) 792-4625; Washington Area phone: (301) 953-9170', SYSUTCDATETIME()),
    (@spr, 24, 'Indianapolis, IN', NULL, 'Jason Barber', '8009 Allison Avenue', NULL, 'Indianapolis', 'IN', '46268', '(317) 876-9829', '(800) 331-9792', '(317) 876-1523', NULL, SYSUTCDATETIME()),
    (@spr, 26, 'Cranbury, NJ', NULL, 'Mark Twardzik', '100 Liberty Way', NULL, 'Cranbury', 'NJ', '08512', '(856) 866-1400', NULL, NULL, NULL, SYSUTCDATETIME()),
    (@spr, 28, 'Seattle, WA', 'Tukwila', 'Michael Antonion', '1100 Andover Park West', NULL, 'Tukwila', 'WA', '98188', '(206) 575-8108', '(800) 346-7444', '(206) 575-4539', NULL, SYSUTCDATETIME()),
    (@spr, 30, 'Sacramento, CA', 'Woodland', 'Keith Rau', '2190 Hanson Way', NULL, 'Woodland', 'CA', '95776', '(916) 641-1838', '(800) 548-1617', '(530) 406-7953', NULL, SYSUTCDATETIME()),
    (@spr, 33, 'Salt Lake City, UT', NULL, 'Astrid Artiga', '1970 South 3850 West, Unit B', NULL, 'Salt Lake City', 'UT', '84104', '(801) 956-0100', '(800) 845-8603', '(801) 956-0478', NULL, SYSUTCDATETIME()),
    (@spr, 34, 'Boston, MA', 'Nashua, NH', 'Tom Hobbs', '4 Capitol St.', NULL, 'Nashua', 'NH', '03063', '(603) 883-2147', '(800) 437-0967', '(603) 881-8176', NULL, SYSUTCDATETIME()),
    (@spr, 36, 'Phoenix, AZ', NULL, 'Benjamin Kelly', '1429 S. 40th. Avenue, Ste. C', NULL, 'Phoenix', 'AZ', '85009', '(602) 233-9208', '(800) 433-4101', '(602) 233-9240', NULL, SYSUTCDATETIME()),
    (@spr, 37, 'New York, NY', 'Middletown', 'Mark Twardzik', '24 Wes Warren Dr.', NULL, 'Middletown', 'NY', '10941', '(845) 692-5534', '(800) 833-8658', '(845) 692-2517', 'Street as printed — possibly ""West Warren Dr.""', SYSUTCDATETIME()),
    (@spr, 38, 'Perris, CA', 'Los Angeles', 'Matthew Isham', '4555 Redlands Avenue', NULL, 'Perris', 'CA', '92571', '(951) 681-3114', '(800) 331-5410', NULL, NULL, SYSUTCDATETIME()),
    (@spr, 39, 'Grand Rapids, MI', 'Kentwood', 'Amber Nelson', '4120 Brockton Dr., SE, Ste. 100', NULL, 'Kentwood', 'MI', '49512', '(616) 698-1851', '(800) 968-5200', '(800) 968-0602', 'Second fax: (616) 554-4234', SYSUTCDATETIME());
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerDistributionCenters");
        }
    }
}
