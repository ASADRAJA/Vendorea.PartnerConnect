using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.API.Controllers;

/// <summary>
/// API controller for managing price feed uploads.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Vendorea.PartnerConnect.Api.Authorization.RequireScope(Vendorea.PartnerConnect.Domain.Entities.ApiScopes.FeedsRead)]
public class PriceFeedsController : ControllerBase
{
    private readonly IPriceFeedService _priceFeedService;
    private readonly ILogger<PriceFeedsController> _logger;

    public PriceFeedsController(
        IPriceFeedService priceFeedService,
        ILogger<PriceFeedsController> logger)
    {
        _priceFeedService = priceFeedService;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a price feed file for a dealer.
    /// </summary>
    /// <param name="dealerId">The dealer/tenant ID.</param>
    /// <param name="tradingPartnerCode">The trading partner code (e.g., "SPR").</param>
    /// <param name="file">The price feed file.</param>
    [HttpPost("upload")]
    [Vendorea.PartnerConnect.Api.Authorization.RequireScope(Vendorea.PartnerConnect.Domain.Entities.ApiScopes.Admin)]
    [ProducesResponseType(typeof(PriceFeedUploadResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [RequestSizeLimit(100_000_000)] // 100MB limit
    public async Task<IActionResult> Upload(
        [FromQuery] int dealerId,
        [FromQuery] string tradingPartnerCode,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        if (string.IsNullOrWhiteSpace(tradingPartnerCode))
        {
            return BadRequest("Trading partner code is required");
        }

        _logger.LogInformation(
            "Received price feed upload: Dealer={DealerId}, Partner={PartnerCode}, File={FileName}, Size={Size}",
            dealerId, tradingPartnerCode, file.FileName, file.Length);

        using var stream = file.OpenReadStream();
        var result = await _priceFeedService.UploadAsync(
            dealerId,
            tradingPartnerCode,
            file.FileName,
            stream,
            User.Identity?.Name,
            cancellationToken);

        if (result.IsDuplicate)
        {
            return Conflict(new { message = result.ErrorMessage });
        }

        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        // The file is stored and queued; a background worker parses and inserts it. The client
        // polls upload status (GET history / {id}) to watch it move Pending -> Completed/Failed.
        return Accepted(result);
    }

    /// <summary>
    /// Gets upload history for a dealer.
    /// </summary>
    /// <param name="dealerId">The dealer/tenant ID.</param>
    /// <param name="tradingPartnerCode">Optional: filter by trading partner.</param>
    /// <param name="limit">Maximum number of records to return.</param>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IEnumerable<PriceFeedUploadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int dealerId,
        [FromQuery] string? tradingPartnerCode = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        var uploads = await _priceFeedService.GetUploadHistoryAsync(
            dealerId, tradingPartnerCode, limit, cancellationToken);

        return Ok(uploads);
    }

    /// <summary>
    /// Gets details of a specific upload.
    /// </summary>
    /// <param name="uploadId">The upload ID.</param>
    [HttpGet("{uploadId:int}")]
    [ProducesResponseType(typeof(PriceFeedUploadDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUploadDetails(
        int uploadId,
        CancellationToken cancellationToken)
    {
        var upload = await _priceFeedService.GetUploadDetailsAsync(uploadId, cancellationToken);

        if (upload == null)
        {
            return NotFound();
        }

        return Ok(upload);
    }

    /// <summary>
    /// Pushes an uploaded price feed to Merchant360.
    /// </summary>
    /// <param name="uploadId">The upload ID to push.</param>
    [HttpPost("{uploadId:int}/push-to-merchant360")]
    [Vendorea.PartnerConnect.Api.Authorization.RequireScope(Vendorea.PartnerConnect.Domain.Entities.ApiScopes.Admin)]
    [ProducesResponseType(typeof(PushToMerchant360Result), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PushToMerchant360(
        int uploadId,
        CancellationToken cancellationToken)
    {
        var result = await _priceFeedService.PushToMerchant360Async(uploadId, cancellationToken);

        if (result.ErrorMessage?.Contains("not found") == true)
        {
            return NotFound(new { message = result.ErrorMessage });
        }

        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(result);
    }

    /// <summary>Cancels a queued (Pending) upload so it will not be processed.</summary>
    [HttpPost("{uploadId:int}/cancel")]
    [Vendorea.PartnerConnect.Api.Authorization.RequireScope(Vendorea.PartnerConnect.Domain.Entities.ApiScopes.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int uploadId, CancellationToken cancellationToken)
    {
        var result = await _priceFeedService.CancelUploadAsync(uploadId, cancellationToken);
        return result.Status switch
        {
            PriceFeedActionStatus.NotFound => NotFound(new { message = result.Message }),
            PriceFeedActionStatus.Conflict => Conflict(new { message = result.Message }),
            _ => Ok(new { message = "Upload cancelled." })
        };
    }

    /// <summary>Deletes an upload, its price records, and its stored file. Not allowed while processing.</summary>
    [HttpDelete("{uploadId:int}")]
    [Vendorea.PartnerConnect.Api.Authorization.RequireScope(Vendorea.PartnerConnect.Domain.Entities.ApiScopes.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int uploadId, CancellationToken cancellationToken)
    {
        var result = await _priceFeedService.DeleteUploadAsync(uploadId, cancellationToken);
        return result.Status switch
        {
            PriceFeedActionStatus.NotFound => NotFound(new { message = result.Message }),
            PriceFeedActionStatus.Conflict => Conflict(new { message = result.Message }),
            _ => NoContent()
        };
    }

    /// <summary>
    /// Gets current prices for a dealer from a trading partner.
    /// </summary>
    /// <param name="dealerId">The dealer/tenant ID.</param>
    /// <param name="tradingPartnerCode">The trading partner code.</param>
    /// <param name="limit">Maximum number of records.</param>
    /// <param name="offset">Number of records to skip.</param>
    [HttpGet("prices")]
    [ProducesResponseType(typeof(IEnumerable<PriceRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentPrices(
        [FromQuery] int dealerId,
        [FromQuery] string tradingPartnerCode,
        [FromQuery] int? limit = 100,
        [FromQuery] int? offset = null,
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        if (string.IsNullOrWhiteSpace(tradingPartnerCode))
        {
            return BadRequest("Trading partner code is required");
        }

        var prices = await _priceFeedService.GetCurrentPricesAsync(
            dealerId, tradingPartnerCode, limit, offset, cancellationToken);

        return Ok(prices);
    }

    /// <summary>
    /// Searches prices by SKU or description.
    /// </summary>
    /// <param name="dealerId">The dealer/tenant ID.</param>
    /// <param name="tradingPartnerCode">The trading partner code.</param>
    /// <param name="q">Search term.</param>
    /// <param name="limit">Maximum number of records.</param>
    [HttpGet("prices/search")]
    [ProducesResponseType(typeof(IEnumerable<PriceRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchPrices(
        [FromQuery] int dealerId,
        [FromQuery] string tradingPartnerCode,
        [FromQuery] string q,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        if (string.IsNullOrWhiteSpace(tradingPartnerCode))
        {
            return BadRequest("Trading partner code is required");
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("Search term is required");
        }

        var prices = await _priceFeedService.SearchPricesAsync(
            dealerId, tradingPartnerCode, q, limit, cancellationToken);

        return Ok(prices);
    }
}
