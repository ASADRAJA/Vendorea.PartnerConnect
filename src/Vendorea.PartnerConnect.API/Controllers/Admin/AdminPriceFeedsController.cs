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
[AllowAnonymous] // TODO: Restore [Authorize(Policy = "RequireSystemAdmin")] in production
public class AdminPriceFeedsController : ControllerBase
{
    private readonly IPriceFeedService _priceFeedService;
    private readonly IMerchant360Client _merchant360Client;
    private readonly ILogger<AdminPriceFeedsController> _logger;

    public AdminPriceFeedsController(
        IPriceFeedService priceFeedService,
        IMerchant360Client merchant360Client,
        ILogger<AdminPriceFeedsController> logger)
    {
        _priceFeedService = priceFeedService;
        _merchant360Client = merchant360Client;
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

        // Get merchant names from Merchant360
        var dealerIds = uploads.Select(u => u.DealerId).Distinct().ToList();
        var merchantNames = new Dictionary<int, string>();

        try
        {
            var merchants = await _merchant360Client.GetMerchantsAsync(true, cancellationToken);
            foreach (var merchant in merchants)
            {
                merchantNames[merchant.Id] = merchant.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load merchant names from Merchant360");
        }

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
        var merchants = new List<object>();
        var partners = new List<object>();

        try
        {
            var merchantList = await _merchant360Client.GetMerchantsAsync(true, cancellationToken);
            merchants = merchantList.Select(m => new { m.Id, m.Name, m.Code }).Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load merchants from Merchant360");
        }

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
