using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// The account dimension on a price file.
/// </summary>
/// <remarks>
/// A dealer may hold several accounts with one partner, each with its own price file and its own
/// negotiated costs. This column is what makes "the latest upload for this dealer" resolvable into
/// "the latest upload for this account" — without it, loading one account's file makes another
/// account's prices disappear.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class PriceFeedUploadAccountTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private int _tenantId;
    private int _partnerId;
    private int _defaultAccountId;
    private int _secondAccountId;

    public PriceFeedUploadAccountTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        await _pg.TruncateAsync(
            "SprPriceRecords", "PriceFeedUploads", "TenantPartnerAccounts",
            "TradingPartners", "Tenants", "Organizations");

        await using var db = _pg.CreateContext();

        var org = new Organization { Code = "ACME", Name = "Acme", Status = OrganizationStatus.Active };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var tenant = new Tenant { OrganizationId = org.Id, Code = "D1", Name = "Gemini Office Products" };
        db.Tenants.Add(tenant);

        var partner = new TradingPartner
        {
            Code = "SPR", Name = "S.P. Richards", Status = TradingPartnerStatus.Active
        };
        db.TradingPartners.Add(partner);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _partnerId = partner.Id;

        // Two colon accounts within one customer account group.
        var defaultAccount = new TenantPartnerAccount
        {
            TenantId = tenant.Id, OrganizationId = org.Id, TradingPartnerId = partner.Id,
            ExternalTenantId = "ext-1", AccountNumber = "0033822.01",
            DisplayName = "Main", IsDefault = true, IsActive = true
        };
        var secondAccount = new TenantPartnerAccount
        {
            TenantId = tenant.Id, OrganizationId = org.Id, TradingPartnerId = partner.Id,
            ExternalTenantId = "ext-1", AccountNumber = "0033822.55",
            DisplayName = "Furniture", IsDefault = false, IsActive = true
        };
        db.TenantPartnerAccounts.AddRange(defaultAccount, secondAccount);
        await db.SaveChangesAsync();

        _defaultAccountId = defaultAccount.Id;
        _secondAccountId = secondAccount.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private PriceFeedUpload Upload(int? accountId, DateTime uploadedAt, string fileName) => new()
    {
        DealerId = _tenantId,
        TradingPartnerId = _partnerId,
        TenantPartnerAccountId = accountId,
        FileName = fileName,
        FileHash = Guid.NewGuid().ToString("n"),
        Status = PriceFeedUploadStatus.Completed,
        RecordCount = 46585,
        UploadedAt = uploadedAt
    };

    [Fact]
    public async Task An_upload_can_record_which_account_its_file_belongs_to()
    {
        await using (var db = _pg.CreateContext())
        {
            db.PriceFeedUploads.Add(Upload(_defaultAccountId, DateTime.UtcNow, "0033822.01-full.csv"));
            await db.SaveChangesAsync();
        }

        await using var read = _pg.CreateContext();
        var upload = await read.PriceFeedUploads
            .Include(u => u.TenantPartnerAccount)
            .SingleAsync();

        upload.TenantPartnerAccountId.Should().Be(_defaultAccountId);
        upload.TenantPartnerAccount!.AccountNumber.Should().Be("0033822.01");
        upload.TenantPartnerAccount.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Two_accounts_can_each_hold_their_own_current_file()
    {
        await using (var db = _pg.CreateContext())
        {
            db.PriceFeedUploads.Add(Upload(_defaultAccountId, DateTime.UtcNow.AddDays(-30), "main-aug.csv"));
            db.PriceFeedUploads.Add(Upload(_defaultAccountId, DateTime.UtcNow.AddDays(-1), "main-oct.csv"));
            db.PriceFeedUploads.Add(Upload(_secondAccountId, DateTime.UtcNow.AddDays(-2), "furniture-oct.csv"));
            await db.SaveChangesAsync();
        }

        await using var read = _pg.CreateContext();

        // "Latest for this dealer" would answer main-oct for everything, hiding the furniture
        // account's prices entirely. Scoped per account, each resolves to its own file.
        var latestMain = await read.PriceFeedUploads
            .Where(u => u.TenantPartnerAccountId == _defaultAccountId)
            .OrderByDescending(u => u.UploadedAt).FirstAsync();

        var latestFurniture = await read.PriceFeedUploads
            .Where(u => u.TenantPartnerAccountId == _secondAccountId)
            .OrderByDescending(u => u.UploadedAt).FirstAsync();

        latestMain.FileName.Should().Be("main-oct.csv");
        latestFurniture.FileName.Should().Be("furniture-oct.csv");
    }

    [Fact]
    public async Task An_upload_recorded_before_accounts_were_captured_keeps_a_null()
    {
        await using (var db = _pg.CreateContext())
        {
            db.PriceFeedUploads.Add(Upload(accountId: null, DateTime.UtcNow, "legacy.csv"));
            await db.SaveChangesAsync();
        }

        await using var read = _pg.CreateContext();

        // Null means "recorded before the account was captured". It is deliberately not the
        // default account, and nothing should silently treat it as one.
        (await read.PriceFeedUploads.SingleAsync()).TenantPartnerAccountId.Should().BeNull();
    }

    [Fact]
    public async Task An_account_holding_price_history_cannot_be_deleted_out_from_under_it()
    {
        await using (var db = _pg.CreateContext())
        {
            db.PriceFeedUploads.Add(Upload(_secondAccountId, DateTime.UtcNow, "furniture.csv"));
            await db.SaveChangesAsync();
        }

        await using var db2 = _pg.CreateContext();
        db2.TenantPartnerAccounts.Remove(
            await db2.TenantPartnerAccounts.SingleAsync(a => a.Id == _secondAccountId));

        // Restrict, not Cascade. Deleting an account must not silently take its price history —
        // and the SprPriceRecords hanging off these uploads would go with it.
        var act = async () => await db2.SaveChangesAsync();
        var thrown = await act.Should().ThrowAsync<DbUpdateException>();

        // 23001 restrict_violation, not the generic 23503 foreign_key_violation. The distinction
        // is worth asserting: it confirms the constraint is genuinely ON DELETE RESTRICT rather
        // than a plain reference that happened to be in the way.
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.RestrictViolation);
    }

    [Fact]
    public async Task An_upload_cannot_name_an_account_that_does_not_exist()
    {
        await using var db = _pg.CreateContext();
        db.PriceFeedUploads.Add(Upload(accountId: 999_999, DateTime.UtcNow, "bogus.csv"));

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
