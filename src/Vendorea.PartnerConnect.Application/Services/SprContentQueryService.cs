using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for querying SPR product content.
/// </summary>
public class SprContentQueryService : ISprContentQueryService
{
    private readonly ILogger<SprContentQueryService> _logger;
    private readonly ISprProductContentRepository _productContentRepository;
    private readonly ISprCategoryRepository _categoryRepository;
    private readonly ISprContentUploadRepository _uploadRepository;

    public SprContentQueryService(
        ILogger<SprContentQueryService> logger,
        ISprProductContentRepository productContentRepository,
        ISprCategoryRepository categoryRepository,
        ISprContentUploadRepository uploadRepository)
    {
        _logger = logger;
        _productContentRepository = productContentRepository;
        _categoryRepository = categoryRepository;
        _uploadRepository = uploadRepository;
    }

    public async Task<ProductContentDto?> GetProductAsync(
        int dealerId,
        string productId,
        string localeId = "EN_US",
        bool includeSpecification = true,
        bool includeFeatures = true,
        bool includeRelationships = false,
        CancellationToken cancellationToken = default)
    {
        var product = await _productContentRepository.GetByProductIdAsync(
            productId, localeId,
            includeSpecification, includeFeatures, includeRelationships,
            cancellationToken);

        if (product == null) return null;

        return await MapToDto(product, includeRelationships, cancellationToken);
    }

    public async Task<ProductContentDto?> GetProductBySkuAsync(
        int dealerId,
        string sku,
        string localeId = "EN_US",
        CancellationToken cancellationToken = default)
    {
        var product = await _productContentRepository.GetBySkuAsync(
            sku, localeId, cancellationToken);

        if (product == null) return null;

        // Load full details
        return await GetProductAsync(dealerId, product.ProductId, localeId,
            includeSpecification: true, includeFeatures: true, includeRelationships: true,
            cancellationToken);
    }

    public async Task<PagedResult<ProductContentSummaryDto>> SearchProductsAsync(
        int dealerId,
        ProductContentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        int offset = (request.Page - 1) * request.PageSize;

        var products = await _productContentRepository.SearchAsync(
            request.SearchTerm ?? string.Empty,
            request.LocaleId,
            request.CategoryId,
            request.PageSize + 1, // Get one extra to check for more
            offset,
            cancellationToken);

        var hasMore = products.Count > request.PageSize;
        var items = products.Take(request.PageSize)
            .Select(MapToSummaryDto)
            .ToList();

        // Get total count (expensive but needed for pagination)
        var totalCount = await _productContentRepository.GetCountAsync(
            request.LocaleId, cancellationToken);

        return new PagedResult<ProductContentSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<PagedResult<ProductContentSummaryDto>> GetProductsByCategoryAsync(
        int dealerId,
        int categoryId,
        string localeId = "EN_US",
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        int offset = (page - 1) * pageSize;

        var products = await _productContentRepository.GetByCategoryIdAsync(
            categoryId, localeId, pageSize, offset, cancellationToken);

        var items = products.Select(MapToSummaryDto).ToList();

        return new PagedResult<ProductContentSummaryDto>
        {
            Items = items,
            TotalCount = items.Count, // Approximate
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<RelatedProductDto>> GetAccessoriesAsync(
        int dealerId,
        string productId,
        string localeId = "EN_US",
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return await GetRelatedProductsAsync(
            dealerId, productId, localeId, ProductRelationshipType.Accessory, limit, cancellationToken);
    }

    public async Task<IReadOnlyList<RelatedProductDto>> GetSimilarProductsAsync(
        int dealerId,
        string productId,
        string localeId = "EN_US",
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return await GetRelatedProductsAsync(
            dealerId, productId, localeId, ProductRelationshipType.Similar, limit, cancellationToken);
    }

    public async Task<IReadOnlyList<RelatedProductDto>> GetUpsellsAsync(
        int dealerId,
        string productId,
        string localeId = "EN_US",
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return await GetRelatedProductsAsync(
            dealerId, productId, localeId, ProductRelationshipType.Upsell, limit, cancellationToken);
    }

    private async Task<IReadOnlyList<RelatedProductDto>> GetRelatedProductsAsync(
        int dealerId,
        string productId,
        string localeId,
        ProductRelationshipType relType,
        int limit,
        CancellationToken cancellationToken)
    {
        var product = await _productContentRepository.GetByProductIdAsync(
            productId, localeId, cancellationToken: cancellationToken);

        if (product == null) return Array.Empty<RelatedProductDto>();

        var relationships = await _productContentRepository.GetRelationshipsAsync(
            product.Id, relType, cancellationToken);

        var results = new List<RelatedProductDto>();

        foreach (var rel in relationships.Take(limit))
        {
            var relatedProduct = await _productContentRepository.GetByProductIdAsync(
                rel.RelatedProductId, localeId, cancellationToken: cancellationToken);

            results.Add(new RelatedProductDto
            {
                ProductId = rel.RelatedProductId,
                Sku = rel.RelatedSku ?? relatedProduct?.Sku,
                BrandName = relatedProduct?.BrandName,
                Description = relatedProduct?.Description1,
                ImageUrl75 = relatedProduct?.ImageUrl75,
                RelationshipType = rel.RelationshipType,
                Score = rel.Score,
                SortOrder = rel.SortOrder
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<string>> GetBrandsAsync(
        int dealerId,
        string localeId = "EN_US",
        CancellationToken cancellationToken = default)
    {
        return await _productContentRepository.GetDistinctBrandsAsync(
            localeId, cancellationToken);
    }

    public async Task<ContentStatisticsDto> GetStatisticsAsync(
        int dealerId,
        string localeId = "EN_US",
        CancellationToken cancellationToken = default)
    {
        var productCount = await _productContentRepository.GetCountAsync(
            localeId, cancellationToken);

        var brands = await _productContentRepository.GetDistinctBrandsAsync(
            localeId, cancellationToken);

        var latestUpload = await _uploadRepository.GetLatestCompletedAsync(
            localeId, cancellationToken);

        var categories = await _categoryRepository.GetAllActiveAsync(cancellationToken);

        return new ContentStatisticsDto
        {
            TotalProducts = productCount,
            TotalBrands = brands.Count,
            TotalCategories = categories.Count,
            LastImportDate = latestUpload?.ProcessingCompletedAt,
            LastContentVersion = latestUpload?.ContentVersion
        };
    }

    private async Task<ProductContentDto> MapToDto(
        SprProductContent product,
        bool includeRelationships,
        CancellationToken cancellationToken)
    {
        var dto = new ProductContentDto
        {
            Id = product.Id,
            ProductId = product.ProductId,
            LocaleId = product.LocaleId,
            Sku = product.Sku,
            Upc = product.Upc,
            BrandName = product.BrandName,
            ProductType = product.ProductType,
            ProductLine = product.ProductLine,
            ProductSeries = product.ProductSeries,
            Description1 = product.Description1,
            Description2 = product.Description2,
            Description3 = product.Description3,
            MarketingText = product.MarketingText,
            ManufacturerId = product.ManufacturerId,
            ManufacturerName = product.ManufacturerName,
            CountryOfOrigin = product.CountryOfOrigin,
            CategoryId = product.SprCategoryId,
            SubClass = product.SubClassName,
            Class = product.ClassName,
            Department = product.DepartmentName,
            ImageUrl225 = product.ImageUrl225,
            ImageUrl75 = product.ImageUrl75,
            ContentVersionDate = product.ContentVersionDate ?? DateTime.UtcNow
        };

        // Add category name
        if (product.SprCategoryId.HasValue)
        {
            var category = await _categoryRepository.GetByIdAsync(product.SprCategoryId.Value, cancellationToken);
            dto.CategoryName = category?.CategoryName;
        }

        // Add specification
        if (product.Specification != null)
        {
            dto.SpecificationsHtml = product.Specification.SpecificationsHtml;
        }

        // Add features
        if (product.Features?.Count > 0)
        {
            dto.Features = product.Features
                .OrderBy(f => f.SortOrder)
                .Select(f => new ProductFeatureDto
                {
                    SortOrder = f.SortOrder,
                    BulletText = f.BulletText,
                    FeatureGroup = f.FeatureGroup
                })
                .ToList();
        }

        // Add relationships if requested
        if (includeRelationships && product.Relationships?.Count > 0)
        {
            dto.Accessories = product.Relationships
                .Where(r => r.RelationshipType == ProductRelationshipType.Accessory)
                .OrderBy(r => r.SortOrder)
                .Select(MapRelationshipToDto)
                .ToList();

            dto.SimilarProducts = product.Relationships
                .Where(r => r.RelationshipType == ProductRelationshipType.Similar)
                .OrderBy(r => r.SortOrder)
                .Select(MapRelationshipToDto)
                .ToList();

            dto.Upsells = product.Relationships
                .Where(r => r.RelationshipType == ProductRelationshipType.Upsell)
                .OrderBy(r => r.SortOrder)
                .Select(MapRelationshipToDto)
                .ToList();
        }

        return dto;
    }

    private static ProductContentSummaryDto MapToSummaryDto(SprProductContent product)
    {
        return new ProductContentSummaryDto
        {
            Id = product.Id,
            ProductId = product.ProductId,
            Sku = product.Sku,
            BrandName = product.BrandName,
            Description1 = product.Description1,
            ImageUrl75 = product.ImageUrl75,
            CategoryId = product.SprCategoryId
        };
    }

    private static RelatedProductDto MapRelationshipToDto(SprProductRelationship rel)
    {
        return new RelatedProductDto
        {
            ProductId = rel.RelatedProductId,
            Sku = rel.RelatedSku,
            RelationshipType = rel.RelationshipType,
            Score = rel.Score,
            SortOrder = rel.SortOrder
        };
    }
}
