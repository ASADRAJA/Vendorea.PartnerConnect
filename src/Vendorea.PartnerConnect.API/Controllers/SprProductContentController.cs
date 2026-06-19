using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.API.Controllers;

/// <summary>
/// API controller for querying SPR enhanced product content.
/// </summary>
[ApiController]
[Route("api/v1/dealers/{dealerId}/spr/products")]
[Vendorea.PartnerConnect.Api.Authorization.RequireScope(Vendorea.PartnerConnect.Domain.Entities.ApiScopes.ContentRead)]
public class SprProductContentController : ControllerBase
{
    private readonly ISprContentQueryService _queryService;
    private readonly ILogger<SprProductContentController> _logger;

    public SprProductContentController(
        ISprContentQueryService queryService,
        ILogger<SprProductContentController> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    /// <summary>
    /// Searches products by text, category, or brand.
    /// </summary>
    /// <param name="dealerId">The dealer/tenant ID.</param>
    /// <param name="search">Search text.</param>
    /// <param name="locale">Locale (default: EN_US).</param>
    /// <param name="categoryId">Filter by category.</param>
    /// <param name="brand">Filter by brand name.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 20).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductContentSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        int dealerId,
        [FromQuery] string? search = null,
        [FromQuery] string locale = "EN_US",
        [FromQuery] int? categoryId = null,
        [FromQuery] string? brand = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        var request = new ProductContentSearchRequest
        {
            SearchTerm = search,
            LocaleId = locale,
            CategoryId = categoryId,
            BrandName = brand,
            Page = page,
            PageSize = Math.Min(pageSize, 100)
        };

        var result = await _queryService.SearchProductsAsync(dealerId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a product by product ID.
    /// </summary>
    /// <param name="dealerId">The dealer/tenant ID.</param>
    /// <param name="productId">The SPR product ID.</param>
    /// <param name="locale">Locale (default: EN_US).</param>
    /// <param name="include">Comma-separated includes: specs,features,relationships.</param>
    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(ProductContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByProductId(
        int dealerId,
        string productId,
        [FromQuery] string locale = "EN_US",
        [FromQuery] string? include = null,
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        var includes = (include ?? "specs,features").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var includeSpecs = includes.Contains("specs", StringComparer.OrdinalIgnoreCase);
        var includeFeatures = includes.Contains("features", StringComparer.OrdinalIgnoreCase);
        var includeRelationships = includes.Contains("relationships", StringComparer.OrdinalIgnoreCase);

        var product = await _queryService.GetProductAsync(
            dealerId, productId, locale,
            includeSpecs, includeFeatures, includeRelationships,
            cancellationToken);

        if (product == null)
        {
            return NotFound($"Product {productId} not found");
        }

        return Ok(product);
    }

    /// <summary>
    /// Gets a product by SKU.
    /// </summary>
    [HttpGet("by-sku/{sku}")]
    [ProducesResponseType(typeof(ProductContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySku(
        int dealerId,
        string sku,
        [FromQuery] string locale = "EN_US",
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        var product = await _queryService.GetProductBySkuAsync(
            dealerId, sku, locale, cancellationToken);

        if (product == null)
        {
            return NotFound($"Product with SKU {sku} not found");
        }

        return Ok(product);
    }

    /// <summary>
    /// Gets accessories for a product.
    /// </summary>
    [HttpGet("{productId}/accessories")]
    [ProducesResponseType(typeof(IReadOnlyList<RelatedProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccessories(
        int dealerId,
        string productId,
        [FromQuery] string locale = "EN_US",
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        var accessories = await _queryService.GetAccessoriesAsync(
            dealerId, productId, locale, Math.Min(limit, 50), cancellationToken);

        return Ok(accessories);
    }

    /// <summary>
    /// Gets similar products.
    /// </summary>
    [HttpGet("{productId}/similar")]
    [ProducesResponseType(typeof(IReadOnlyList<RelatedProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSimilar(
        int dealerId,
        string productId,
        [FromQuery] string locale = "EN_US",
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        var similar = await _queryService.GetSimilarProductsAsync(
            dealerId, productId, locale, Math.Min(limit, 50), cancellationToken);

        return Ok(similar);
    }

    /// <summary>
    /// Gets upsell recommendations.
    /// </summary>
    [HttpGet("{productId}/upsells")]
    [ProducesResponseType(typeof(IReadOnlyList<RelatedProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpsells(
        int dealerId,
        string productId,
        [FromQuery] string locale = "EN_US",
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        var upsells = await _queryService.GetUpsellsAsync(
            dealerId, productId, locale, Math.Min(limit, 50), cancellationToken);

        return Ok(upsells);
    }

    /// <summary>
    /// Gets distinct brands available for a dealer.
    /// </summary>
    [HttpGet("brands")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBrands(
        int dealerId,
        [FromQuery] string locale = "EN_US",
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        var brands = await _queryService.GetBrandsAsync(dealerId, locale, cancellationToken);
        return Ok(brands);
    }

    /// <summary>
    /// Gets content statistics for a dealer.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ContentStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(
        int dealerId,
        [FromQuery] string locale = "EN_US",
        CancellationToken cancellationToken = default)
    {
        if (dealerId <= 0)
        {
            return BadRequest("Invalid dealer ID");
        }

        var stats = await _queryService.GetStatisticsAsync(dealerId, locale, cancellationToken);
        return Ok(stats);
    }
}
