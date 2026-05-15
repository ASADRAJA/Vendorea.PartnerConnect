using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.API.Controllers;

/// <summary>
/// API controller for querying SPR product categories.
/// </summary>
[ApiController]
[Route("api/v1/spr/categories")]
public class SprCategoriesController : ControllerBase
{
    private readonly ISprCategoryRepository _categoryRepository;
    private readonly ILogger<SprCategoriesController> _logger;

    public SprCategoriesController(
        ISprCategoryRepository categoryRepository,
        ILogger<SprCategoriesController> logger)
    {
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all root categories (top-level).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(
        [FromQuery] int? parentId = null,
        [FromQuery] int? level = null,
        CancellationToken cancellationToken = default)
    {
        if (parentId.HasValue)
        {
            var children = await _categoryRepository.GetChildCategoriesAsync(parentId.Value, cancellationToken);
            return Ok(children.Select(MapToDto));
        }

        if (level.HasValue)
        {
            var levelCategories = await _categoryRepository.GetByLevelAsync(level.Value, cancellationToken);
            return Ok(levelCategories.Select(MapToDto));
        }

        var rootCategories = await _categoryRepository.GetRootCategoriesAsync(cancellationToken);
        return Ok(rootCategories.Select(MapToDto));
    }

    /// <summary>
    /// Gets a category by ID.
    /// </summary>
    [HttpGet("{categoryId:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        if (category == null)
        {
            return NotFound($"Category {categoryId} not found");
        }

        return Ok(MapToDto(category));
    }

    /// <summary>
    /// Gets all descendants of a category.
    /// </summary>
    [HttpGet("{categoryId:int}/descendants")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDescendants(int categoryId, CancellationToken cancellationToken = default)
    {
        var descendants = await _categoryRepository.GetDescendantsAsync(categoryId, cancellationToken);
        return Ok(descendants.Select(MapToDto));
    }

    /// <summary>
    /// Gets the ancestor path for a category (breadcrumb).
    /// </summary>
    [HttpGet("{categoryId:int}/ancestors")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAncestors(int categoryId, CancellationToken cancellationToken = default)
    {
        var ancestors = await _categoryRepository.GetAncestorsAsync(categoryId, cancellationToken);
        return Ok(ancestors.Select(MapToDto));
    }

    /// <summary>
    /// Searches categories by name.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("Search query is required");
        }

        var categories = await _categoryRepository.SearchByNameAsync(q, Math.Min(limit, 100), cancellationToken);
        return Ok(categories.Select(MapToDto));
    }

    /// <summary>
    /// Gets category counts by level.
    /// </summary>
    [HttpGet("counts")]
    [ProducesResponseType(typeof(Dictionary<int, int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCounts(CancellationToken cancellationToken = default)
    {
        var counts = await _categoryRepository.GetCountsByLevelAsync(cancellationToken);
        return Ok(counts);
    }

    private static CategoryDto MapToDto(Domain.Entities.SprCategory category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            CategoryCode = category.CategoryCode,
            CategoryName = category.CategoryName,
            ParentCategoryId = category.ParentCategoryId,
            Level = category.Level,
            FullPath = category.FullPath,
            UnspscCode = category.UnspscCode
        };
    }
}

/// <summary>
/// Category DTO.
/// </summary>
public class CategoryDto
{
    public int Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }
    public int Level { get; set; }
    public string? FullPath { get; set; }
    public string? UnspscCode { get; set; }
}
