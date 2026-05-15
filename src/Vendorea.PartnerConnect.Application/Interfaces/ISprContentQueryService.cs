using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service for querying SPR product content.
/// </summary>
public interface ISprContentQueryService
{
    /// <summary>
    /// Gets product content by product ID with optional includes.
    /// </summary>
    Task<ProductContentDto?> GetProductAsync(
        int dealerId,
        string productId,
        string localeId = "EN_US",
        bool includeSpecification = true,
        bool includeFeatures = true,
        bool includeRelationships = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product content by SKU.
    /// </summary>
    Task<ProductContentDto?> GetProductBySkuAsync(
        int dealerId,
        string sku,
        string localeId = "EN_US",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches products by text.
    /// </summary>
    Task<PagedResult<ProductContentSummaryDto>> SearchProductsAsync(
        int dealerId,
        ProductContentSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets products by category.
    /// </summary>
    Task<PagedResult<ProductContentSummaryDto>> GetProductsByCategoryAsync(
        int dealerId,
        int categoryId,
        string localeId = "EN_US",
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product accessories.
    /// </summary>
    Task<IReadOnlyList<RelatedProductDto>> GetAccessoriesAsync(
        int dealerId,
        string productId,
        string localeId = "EN_US",
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets similar products.
    /// </summary>
    Task<IReadOnlyList<RelatedProductDto>> GetSimilarProductsAsync(
        int dealerId,
        string productId,
        string localeId = "EN_US",
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upsell recommendations.
    /// </summary>
    Task<IReadOnlyList<RelatedProductDto>> GetUpsellsAsync(
        int dealerId,
        string productId,
        string localeId = "EN_US",
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets distinct brands for a dealer.
    /// </summary>
    Task<IReadOnlyList<string>> GetBrandsAsync(
        int dealerId,
        string localeId = "EN_US",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets content statistics for a dealer.
    /// </summary>
    Task<ContentStatisticsDto> GetStatisticsAsync(
        int dealerId,
        string localeId = "EN_US",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Search request for product content.
/// </summary>
public class ProductContentSearchRequest
{
    public string? SearchTerm { get; set; }
    public string LocaleId { get; set; } = "EN_US";
    public int? CategoryId { get; set; }
    public string? BrandName { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Paged result container.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Full product content DTO.
/// </summary>
public class ProductContentDto
{
    public long Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string LocaleId { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Upc { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string? ProductLine { get; set; }
    public string? ProductSeries { get; set; }
    public string Description1 { get; set; } = string.Empty;
    public string? Description2 { get; set; }
    public string? Description3 { get; set; }
    public string? MarketingText { get; set; }
    public string ManufacturerId { get; set; } = string.Empty;
    public string ManufacturerName { get; set; } = string.Empty;
    public string? CountryOfOrigin { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? SubClass { get; set; }
    public string? Class { get; set; }
    public string? Department { get; set; }
    public string? ImageUrl225 { get; set; }
    public string? ImageUrl75 { get; set; }
    public string? SpecificationsHtml { get; set; }
    public IReadOnlyList<ProductFeatureDto> Features { get; set; } = Array.Empty<ProductFeatureDto>();
    public IReadOnlyList<RelatedProductDto> Accessories { get; set; } = Array.Empty<RelatedProductDto>();
    public IReadOnlyList<RelatedProductDto> SimilarProducts { get; set; } = Array.Empty<RelatedProductDto>();
    public IReadOnlyList<RelatedProductDto> Upsells { get; set; } = Array.Empty<RelatedProductDto>();
    public DateTime ContentVersionDate { get; set; }
}

/// <summary>
/// Summary DTO for product listings.
/// </summary>
public class ProductContentSummaryDto
{
    public long Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string Description1 { get; set; } = string.Empty;
    public string? ImageUrl75 { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
}

/// <summary>
/// Product feature DTO.
/// </summary>
public class ProductFeatureDto
{
    public int SortOrder { get; set; }
    public string BulletText { get; set; } = string.Empty;
    public string? FeatureGroup { get; set; }
}

/// <summary>
/// Related product DTO.
/// </summary>
public class RelatedProductDto
{
    public string ProductId { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? BrandName { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl75 { get; set; }
    public ProductRelationshipType RelationshipType { get; set; }
    public decimal? Score { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// Content statistics DTO.
/// </summary>
public class ContentStatisticsDto
{
    public int TotalProducts { get; set; }
    public int ProductsWithSpecs { get; set; }
    public int ProductsWithFeatures { get; set; }
    public int ProductsWithAccessories { get; set; }
    public int TotalFeatures { get; set; }
    public int TotalRelationships { get; set; }
    public int TotalCategories { get; set; }
    public int TotalBrands { get; set; }
    public DateTime? LastImportDate { get; set; }
    public string? LastContentVersion { get; set; }
}
