using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class SprProductContentRepository : ISprProductContentRepository
{
    private readonly PartnerConnectDbContext _context;

    public SprProductContentRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<SprProductContent?> GetByIdAsync(
        long id,
        bool includeSpecification = false,
        bool includeFeatures = false,
        bool includeRelationships = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprProductContent.AsQueryable();

        if (includeSpecification)
            query = query.Include(p => p.Specification);
        if (includeFeatures)
            query = query.Include(p => p.Features.OrderBy(f => f.SortOrder));
        if (includeRelationships)
            query = query.Include(p => p.Relationships.OrderBy(r => r.SortOrder));

        return await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<SprProductContent?> GetByProductIdAsync(
        string productId,
        string localeId = "EN_US",
        bool includeSpecification = false,
        bool includeFeatures = false,
        bool includeRelationships = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprProductContent.AsQueryable();

        if (includeSpecification)
            query = query.Include(p => p.Specification);
        if (includeFeatures)
            query = query.Include(p => p.Features.OrderBy(f => f.SortOrder));
        if (includeRelationships)
            query = query.Include(p => p.Relationships.OrderBy(r => r.SortOrder));

        return await query.FirstOrDefaultAsync(
            p => p.ProductId == productId && p.LocaleId == localeId,
            cancellationToken);
    }

    public async Task<SprProductContent?> GetBySkuAsync(
        string sku,
        string localeId = "EN_US",
        CancellationToken cancellationToken = default)
    {
        return await _context.SprProductContent
            .FirstOrDefaultAsync(
                p => p.Sku == sku && p.LocaleId == localeId,
                cancellationToken);
    }

    public async Task<SprProductContent?> GetByUpcAsync(
        string upc,
        string localeId = "EN_US",
        CancellationToken cancellationToken = default)
    {
        return await _context.SprProductContent
            .FirstOrDefaultAsync(
                p => p.Upc == upc && p.LocaleId == localeId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SprProductContent>> GetAllAsync(
        string localeId = "EN_US",
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprProductContent
            .Where(p => p.LocaleId == localeId)
            .OrderBy(p => p.ProductId);

        if (offset.HasValue)
            query = (IOrderedQueryable<SprProductContent>)query.Skip(offset.Value);
        if (limit.HasValue)
            query = (IOrderedQueryable<SprProductContent>)query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprProductContent>> GetByCategoryIdAsync(
        int categoryId,
        string localeId = "EN_US",
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprProductContent
            .Where(p => p.LocaleId == localeId &&
                       p.SprCategoryId == categoryId)
            .OrderBy(p => p.BrandName)
            .ThenBy(p => p.ProductId);

        if (offset.HasValue)
            query = (IOrderedQueryable<SprProductContent>)query.Skip(offset.Value);
        if (limit.HasValue)
            query = (IOrderedQueryable<SprProductContent>)query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprProductContent>> GetByBrandAsync(
        string brandName,
        string localeId = "EN_US",
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprProductContent
            .Where(p => p.LocaleId == localeId &&
                       p.BrandName == brandName)
            .OrderBy(p => p.ProductId);

        if (offset.HasValue)
            query = (IOrderedQueryable<SprProductContent>)query.Skip(offset.Value);
        if (limit.HasValue)
            query = (IOrderedQueryable<SprProductContent>)query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprProductContent>> SearchAsync(
        string searchTerm,
        string localeId = "EN_US",
        int? categoryId = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprProductContent
            .Where(p => p.LocaleId == localeId &&
                       (p.Description1.Contains(searchTerm) ||
                        p.BrandName.Contains(searchTerm) ||
                        p.ProductId.Contains(searchTerm) ||
                        (p.Keywords != null && p.Keywords.Contains(searchTerm))));

        if (categoryId.HasValue)
            query = query.Where(p => p.SprCategoryId == categoryId.Value);

        var orderedQuery = query.OrderBy(p => p.BrandName).ThenBy(p => p.ProductId);

        if (offset.HasValue)
            orderedQuery = (IOrderedQueryable<SprProductContent>)orderedQuery.Skip(offset.Value);
        if (limit.HasValue)
            orderedQuery = (IOrderedQueryable<SprProductContent>)orderedQuery.Take(limit.Value);

        return await orderedQuery.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprProductContent>> GetByUploadIdAsync(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprProductContent
            .Where(p => p.ContentUploadId == uploadId)
            .OrderBy(p => p.ProductId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprProductRelationship>> GetRelationshipsAsync(
        long productContentId,
        ProductRelationshipType? relationshipType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprProductRelationships
            .Where(r => r.SprProductContentId == productContentId);

        if (relationshipType.HasValue)
            query = query.Where(r => r.RelationshipType == relationshipType.Value);

        return await query
            .OrderBy(r => r.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprProductFeature>> GetFeaturesAsync(
        long productContentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprProductFeatures
            .Where(f => f.SprProductContentId == productContentId)
            .OrderBy(f => f.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<SprProductSpecification?> GetSpecificationAsync(
        long productContentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprProductSpecifications
            .FirstOrDefaultAsync(s => s.SprProductContentId == productContentId, cancellationToken);
    }

    public async Task BulkInsertAsync(
        IEnumerable<SprProductContent> products,
        CancellationToken cancellationToken = default)
    {
        const int batchSize = 500;
        var productList = products.ToList();

        for (int i = 0; i < productList.Count; i += batchSize)
        {
            var batch = productList.Skip(i).Take(batchSize);
            await _context.SprProductContent.AddRangeAsync(batch, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task BulkInsertFeaturesAsync(
        IEnumerable<SprProductFeature> features,
        CancellationToken cancellationToken = default)
    {
        const int batchSize = 1000;
        var featureList = features.ToList();

        for (int i = 0; i < featureList.Count; i += batchSize)
        {
            var batch = featureList.Skip(i).Take(batchSize);
            await _context.SprProductFeatures.AddRangeAsync(batch, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task BulkInsertRelationshipsAsync(
        IEnumerable<SprProductRelationship> relationships,
        CancellationToken cancellationToken = default)
    {
        const int batchSize = 1000;
        var relationshipList = relationships.ToList();

        for (int i = 0; i < relationshipList.Count; i += batchSize)
        {
            var batch = relationshipList.Skip(i).Take(batchSize);
            await _context.SprProductRelationships.AddRangeAsync(batch, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task BulkInsertSpecificationsAsync(
        IEnumerable<SprProductSpecification> specifications,
        CancellationToken cancellationToken = default)
    {
        const int batchSize = 500;
        var specList = specifications.ToList();

        for (int i = 0; i < specList.Count; i += batchSize)
        {
            var batch = specList.Skip(i).Take(batchSize);
            await _context.SprProductSpecifications.AddRangeAsync(batch, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task BulkUpdateDescriptionsAsync(
        Dictionary<long, string> descriptions,
        CancellationToken cancellationToken = default)
    {
        if (descriptions.Count == 0)
            return;

        // Use raw SQL with CASE expression for efficient bulk update
        // Process in batches to avoid SQL parameter limits
        const int batchSize = 100;
        var items = descriptions.ToList();
        var now = DateTime.UtcNow;

        for (int i = 0; i < items.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = items.Skip(i).Take(batchSize).ToList();

            // Build the CASE expression for descriptions
            var caseExpressions = new List<string>();
            var parameters = new List<object>();
            var paramIndex = 0;

            foreach (var (id, description) in batch)
            {
                caseExpressions.Add($"WHEN {id} THEN {{{paramIndex}}}");
                parameters.Add(description ?? string.Empty);
                paramIndex++;
            }

            var ids = string.Join(",", batch.Select(b => b.Key));
            var caseExpression = string.Join(" ", caseExpressions);

            var sql = $@"UPDATE SprProductContent
                         SET Description1 = CASE Id {caseExpression} END,
                             UpdatedAt = {{{paramIndex}}}
                         WHERE Id IN ({ids})";

            parameters.Add(now);

            await _context.Database.ExecuteSqlRawAsync(sql, parameters.ToArray(), cancellationToken);
        }
    }

    public async Task DeleteByUploadIdAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        // Delete in correct order due to FK constraints
        // First get all product content IDs for this upload
        var contentIds = await _context.SprProductContent
            .Where(p => p.ContentUploadId == uploadId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (contentIds.Count == 0)
            return;

        // Delete child records first
        await _context.SprProductRelationships
            .Where(r => contentIds.Contains(r.SprProductContentId))
            .ExecuteDeleteAsync(cancellationToken);

        await _context.SprProductFeatures
            .Where(f => contentIds.Contains(f.SprProductContentId))
            .ExecuteDeleteAsync(cancellationToken);

        await _context.SprProductSpecifications
            .Where(s => contentIds.Contains(s.SprProductContentId))
            .ExecuteDeleteAsync(cancellationToken);

        // Then delete parent records
        await _context.SprProductContent
            .Where(p => p.ContentUploadId == uploadId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> GetCountByUploadIdAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        return await _context.SprProductContent
            .CountAsync(p => p.ContentUploadId == uploadId, cancellationToken);
    }

    public async Task<int> GetCountAsync(
        string localeId = "EN_US",
        CancellationToken cancellationToken = default)
    {
        return await _context.SprProductContent
            .CountAsync(p => p.LocaleId == localeId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetDistinctBrandsAsync(
        string localeId = "EN_US",
        CancellationToken cancellationToken = default)
    {
        return await _context.SprProductContent
            .Where(p => p.LocaleId == localeId)
            .Select(p => p.BrandName)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasContentAsync(
        string localeId = "EN_US",
        CancellationToken cancellationToken = default)
    {
        return await _context.SprProductContent
            .AnyAsync(p => p.LocaleId == localeId, cancellationToken);
    }

    public async Task<IReadOnlyList<SprProductFeature>> GetFeaturesByProductIdsAsync(
        IEnumerable<long> productContentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = productContentIds.ToList();
        return await _context.SprProductFeatures
            .Where(f => ids.Contains(f.SprProductContentId))
            .OrderBy(f => f.SprProductContentId)
            .ThenBy(f => f.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprProductRelationship>> GetRelationshipsByProductIdsAsync(
        IEnumerable<long> productContentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = productContentIds.ToList();
        return await _context.SprProductRelationships
            .Where(r => ids.Contains(r.SprProductContentId))
            .OrderBy(r => r.SprProductContentId)
            .ThenBy(r => r.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprProductSpecification>> GetSpecificationsByProductIdsAsync(
        IEnumerable<long> productContentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = productContentIds.ToList();
        return await _context.SprProductSpecifications
            .Where(s => ids.Contains(s.SprProductContentId))
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetSkuToCategoryCodeMappingAsync(
        IEnumerable<string> skus,
        CancellationToken cancellationToken = default)
    {
        var skuList = skus.ToList();
        if (skuList.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Query products joined with categories to get SKU → CategoryCode mapping
        // Process in batches to avoid SQL parameter limits
        const int batchSize = 2000;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < skuList.Count; i += batchSize)
        {
            var batch = skuList.Skip(i).Take(batchSize).ToList();

            var mappings = await _context.SprProductContent
                .Where(p => p.Sku != null && batch.Contains(p.Sku) && p.SprCategoryId != null)
                .Join(
                    _context.SprCategories,
                    p => p.SprCategoryId,
                    c => c.Id,
                    (p, c) => new { p.Sku, c.CategoryCode })
                .ToListAsync(cancellationToken);

            foreach (var mapping in mappings)
            {
                if (!string.IsNullOrEmpty(mapping.Sku) && !result.ContainsKey(mapping.Sku))
                {
                    result[mapping.Sku] = mapping.CategoryCode;
                }
            }
        }

        return result;
    }
}
