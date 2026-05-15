using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class SprCategoryRepository : ISprCategoryRepository
{
    private readonly PartnerConnectDbContext _context;

    public SprCategoryRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<SprCategory?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.SprCategories
            .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<SprCategory?> GetByCodeAsync(string categoryCode, CancellationToken cancellationToken = default)
    {
        return await _context.SprCategories
            .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.CategoryCode == categoryCode, cancellationToken);
    }

    public async Task<IReadOnlyList<SprCategory>> GetRootCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SprCategories
            .Where(c => c.ParentCategoryId == null && c.IsActive)
            .OrderBy(c => c.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprCategory>> GetChildCategoriesAsync(
        int parentCategoryId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprCategories
            .Where(c => c.ParentCategoryId == parentCategoryId && c.IsActive)
            .OrderBy(c => c.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprCategory>> GetByLevelAsync(
        int level,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprCategories
            .Where(c => c.Level == level && c.IsActive)
            .OrderBy(c => c.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprCategory>> GetFullHierarchyAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SprCategories
            .Include(c => c.ChildCategories.Where(cc => cc.IsActive))
            .Where(c => c.IsActive)
            .OrderBy(c => c.Level)
            .ThenBy(c => c.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprCategory>> GetDescendantsAsync(
        int categoryId,
        CancellationToken cancellationToken = default)
    {
        // Use materialized path for efficient descendant lookup
        var category = await _context.SprCategories
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

        if (category == null || string.IsNullOrEmpty(category.FullPath))
            return Array.Empty<SprCategory>();

        var pathPrefix = category.FullPath + "/";
        return await _context.SprCategories
            .Where(c => c.FullPath != null &&
                       c.FullPath.StartsWith(pathPrefix) &&
                       c.IsActive)
            .OrderBy(c => c.Level)
            .ThenBy(c => c.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprCategory>> GetAncestorsAsync(
        int categoryId,
        CancellationToken cancellationToken = default)
    {
        var ancestors = new List<SprCategory>();
        var currentCategory = await _context.SprCategories
            .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

        while (currentCategory?.ParentCategory != null)
        {
            ancestors.Insert(0, currentCategory.ParentCategory);
            currentCategory = await _context.SprCategories
                .Include(c => c.ParentCategory)
                .FirstOrDefaultAsync(c => c.Id == currentCategory.ParentCategoryId, cancellationToken);
        }

        return ancestors;
    }

    public async Task<IReadOnlyList<SprCategory>> SearchByNameAsync(
        string searchTerm,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprCategories
            .Where(c => c.IsActive && c.CategoryName.Contains(searchTerm))
            .OrderBy(c => c.CategoryName);

        if (limit.HasValue)
        {
            return await query.Take(limit.Value).ToListAsync(cancellationToken);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<SprCategory> UpsertAsync(
        SprCategory category,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.SprCategories
            .FirstOrDefaultAsync(c => c.CategoryCode == category.CategoryCode, cancellationToken);

        if (existing != null)
        {
            existing.CategoryName = category.CategoryName;
            existing.ParentCategoryId = category.ParentCategoryId;
            existing.Level = category.Level;
            existing.FullPath = category.FullPath;
            existing.IsActive = category.IsActive;
            existing.UnspscCode = category.UnspscCode;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            category.CreatedAt = DateTime.UtcNow;
            _context.SprCategories.Add(category);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return existing ?? category;
    }

    public async Task BulkUpsertAsync(
        IEnumerable<SprCategory> categories,
        CancellationToken cancellationToken = default)
    {
        const int batchSize = 500;
        var categoryList = categories.ToList();

        // Get all existing categories by code
        var existingCodes = await _context.SprCategories
            .Select(c => c.CategoryCode)
            .ToListAsync(cancellationToken);

        var existingCodesSet = existingCodes.ToHashSet();

        for (int i = 0; i < categoryList.Count; i += batchSize)
        {
            var batch = categoryList.Skip(i).Take(batchSize).ToList();

            foreach (var category in batch)
            {
                if (existingCodesSet.Contains(category.CategoryCode))
                {
                    // Update existing
                    var existing = await _context.SprCategories
                        .FirstOrDefaultAsync(c => c.CategoryCode == category.CategoryCode, cancellationToken);

                    if (existing != null)
                    {
                        existing.CategoryName = category.CategoryName;
                        existing.ParentCategoryId = category.ParentCategoryId;
                        existing.Level = category.Level;
                        existing.FullPath = category.FullPath;
                        existing.IsActive = category.IsActive;
                        existing.UnspscCode = category.UnspscCode;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Insert new
                    category.CreatedAt = DateTime.UtcNow;
                    _context.SprCategories.Add(category);
                    existingCodesSet.Add(category.CategoryCode);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SprCategory>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SprCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Level)
            .ThenBy(c => c.CategoryName)
            .ToListAsync(cancellationToken);
    }

    public async Task DeactivateExceptAsync(
        IEnumerable<string> activeCategoryCodes,
        CancellationToken cancellationToken = default)
    {
        var activeCodesSet = activeCategoryCodes.ToHashSet();

        await _context.SprCategories
            .Where(c => c.IsActive && !activeCodesSet.Contains(c.CategoryCode))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.IsActive, false)
                    .SetProperty(c => c.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<Dictionary<int, int>> GetCountsByLevelAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SprCategories
            .Where(c => c.IsActive)
            .GroupBy(c => c.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Level, x => x.Count, cancellationToken);
    }
}
