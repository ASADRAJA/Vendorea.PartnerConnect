using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for managing SPR product content.
/// Content is SHARED MASTER DATA - not dealer-specific.
/// </summary>
public interface ISprProductContentRepository
{
    /// <summary>
    /// Gets product content by ID.
    /// </summary>
    Task<SprProductContent?> GetByIdAsync(
        long id,
        bool includeSpecification = false,
        bool includeFeatures = false,
        bool includeRelationships = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product content by product ID.
    /// </summary>
    Task<SprProductContent?> GetByProductIdAsync(
        string productId,
        string localeId = "EN_US",
        bool includeSpecification = false,
        bool includeFeatures = false,
        bool includeRelationships = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product content by SKU.
    /// </summary>
    Task<SprProductContent?> GetBySkuAsync(
        string sku,
        string localeId = "EN_US",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product content by UPC.
    /// </summary>
    Task<SprProductContent?> GetByUpcAsync(
        string upc,
        string localeId = "EN_US",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all product content for a locale.
    /// </summary>
    Task<IReadOnlyList<SprProductContent>> GetAllAsync(
        string localeId = "EN_US",
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product content by category.
    /// </summary>
    Task<IReadOnlyList<SprProductContent>> GetByCategoryIdAsync(
        int categoryId,
        string localeId = "EN_US",
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product content by brand.
    /// </summary>
    Task<IReadOnlyList<SprProductContent>> GetByBrandAsync(
        string brandName,
        string localeId = "EN_US",
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches product content by description or keywords.
    /// </summary>
    Task<IReadOnlyList<SprProductContent>> SearchAsync(
        string searchTerm,
        string localeId = "EN_US",
        int? categoryId = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product content for an upload batch.
    /// </summary>
    Task<IReadOnlyList<SprProductContent>> GetByUploadIdAsync(
        int uploadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product relationships by type.
    /// </summary>
    Task<IReadOnlyList<SprProductRelationship>> GetRelationshipsAsync(
        long productContentId,
        ProductRelationshipType? relationshipType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product features.
    /// </summary>
    Task<IReadOnlyList<SprProductFeature>> GetFeaturesAsync(
        long productContentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets product specification HTML.
    /// </summary>
    Task<SprProductSpecification?> GetSpecificationAsync(
        long productContentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk inserts product content for an import.
    /// </summary>
    Task BulkInsertAsync(
        IEnumerable<SprProductContent> products,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk inserts product features.
    /// </summary>
    Task BulkInsertFeaturesAsync(
        IEnumerable<SprProductFeature> features,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk inserts product relationships.
    /// </summary>
    Task BulkInsertRelationshipsAsync(
        IEnumerable<SprProductRelationship> relationships,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk inserts product specifications.
    /// </summary>
    Task BulkInsertSpecificationsAsync(
        IEnumerable<SprProductSpecification> specifications,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk updates product descriptions.
    /// </summary>
    Task BulkUpdateDescriptionsAsync(
        Dictionary<long, string> descriptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all product content for an upload.
    /// </summary>
    Task DeleteByUploadIdAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of products for an upload.
    /// </summary>
    Task<int> GetCountByUploadIdAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of all products for a locale.
    /// </summary>
    Task<int> GetCountAsync(
        string localeId = "EN_US",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets distinct brands for a locale.
    /// </summary>
    Task<IReadOnlyList<string>> GetDistinctBrandsAsync(
        string localeId = "EN_US",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if content exists for a locale.
    /// </summary>
    Task<bool> HasContentAsync(
        string localeId = "EN_US",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets features for multiple products at once.
    /// </summary>
    Task<IReadOnlyList<SprProductFeature>> GetFeaturesByProductIdsAsync(
        IEnumerable<long> productContentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets relationships for multiple products at once.
    /// </summary>
    Task<IReadOnlyList<SprProductRelationship>> GetRelationshipsByProductIdsAsync(
        IEnumerable<long> productContentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets specifications for multiple products at once.
    /// </summary>
    Task<IReadOnlyList<SprProductSpecification>> GetSpecificationsByProductIdsAsync(
        IEnumerable<long> productContentIds,
        CancellationToken cancellationToken = default);
}
