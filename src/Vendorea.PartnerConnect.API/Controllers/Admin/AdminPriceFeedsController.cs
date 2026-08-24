using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for price feed management.
/// </summary>
[ApiController]
[Route("api/admin/pricefeeds")]
public class AdminPriceFeedsController : ControllerBase
{
    private readonly IPriceFeedService _priceFeedService;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<AdminPriceFeedsController> _logger;

    public AdminPriceFeedsController(
        IPriceFeedService priceFeedService,
        ITenantRepository tenantRepository,
        ILogger<AdminPriceFeedsController> logger)
    {
        _priceFeedService = priceFeedService;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all price feed uploads with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllUploads(
        [FromQuery] int? dealerId = null,
        [FromQuery] string? tradingPartnerCode = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Admin getting price feed uploads: DealerId={DealerId}, Partner={Partner}, Limit={Limit}",
            dealerId, tradingPartnerCode, limit);

        var uploads = await _priceFeedService.GetAllUploadHistoryAsync(
            dealerId, tradingPartnerCode, limit, cancellationToken);

        // Dealer names come from our own tenants, keyed by our own id.
        //
        // These were read from Merchant360 and keyed by Merchant360's tenant id, then looked up with
        // DealerId - which is a PartnerConnect tenant id. Two numbering systems that overlap: an
        // upload for our tenant 1 (Merchant360 tenant 4, "Dealer1") displayed as Merchant360's
        // tenant 1, "Demo Merchant". Wrong, plausible, and silent.
        //
        // Tenants.Code holds the Merchant360 id when a translation is genuinely needed. It is not
        // needed to show a name: we hold one, and a local read cannot fail halfway through a render
        // the way the cross-system call could.
        var tenants = await _tenantRepository.GetAllAsync(cancellationToken);
        var merchantNames = tenants.ToDictionary(t => t.Id, t => t.Name);

        // Map with merchant names
        var result = uploads.Select(u => new
        {
            u.Id,
            u.DealerId,
            DealerName = merchantNames.TryGetValue(u.DealerId, out var name) ? name : $"Merchant #{u.DealerId}",
            u.TradingPartnerCode,
            u.TradingPartnerName,
            u.FileName,
            Status = u.Status.ToString(),
            u.RecordCount,
            u.ErrorCount,
            u.UploadedAt,
            u.ProcessedAt,
            u.PushedToMerchant360At
        });

        return Ok(result);
    }

    /// <summary>
    /// Gets filter options for price feeds.
    /// </summary>
    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(CancellationToken cancellationToken)
    {
        var partners = new List<object>();

        // Our own tenants, for the same reason as the history above - and here the id matters as
        // well as the name: this list feeds the dealer filter, which filters on DealerId. Offering
        // Merchant360's ids meant picking a dealer filtered by an id that means something else, so
        // the filter quietly returned the wrong uploads or none.
        var tenants = await _tenantRepository.GetAllAsync(cancellationToken);
        var merchants = tenants
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name, t.Code })
            .Cast<object>()
            .ToList();

        // Get distinct partners from uploads
        var uploads = await _priceFeedService.GetAllUploadHistoryAsync(limit: 1000, cancellationToken: cancellationToken);
        partners = uploads
            .Select(u => new { Code = u.TradingPartnerCode, Name = u.TradingPartnerName })
            .DistinctBy(p => p.Code)
            .Cast<object>()
            .ToList();

        return Ok(new { Merchants = merchants, Partners = partners });
    }
}
