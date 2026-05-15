using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for managing SPR product categories.
/// </summary>
public interface ISprCategoryRepository
{
    /// <summary>
    /// Gets a category by ID.
    /// </summary>
    Task<SprCategory?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a category by category code.
    /// </summary>
    Task<SprCategory?> GetByCodeAsync(string categoryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all root categories (no parent).
    /// </summary>
    Task<IReadOnlyList<SprCategory>> GetRootCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets child categories for a parent.
    /// </summary>
    Task<IReadOnlyList<SprCategory>> GetChildCategoriesAsync(
        int parentCategoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all categories at a specific level.
    /// </summary>
    Task<IReadOnlyList<SprCategory>> GetByLevelAsync(
        int level,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full category hierarchy (tree).
    /// </summary>
    Task<IReadOnlyList<SprCategory>> GetFullHierarchyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all descendants of a category (including grandchildren, etc.).
    /// </summary>
    Task<IReadOnlyList<SprCategory>> GetDescendantsAsync(
        int categoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ancestor path for a category (from root to parent).
    /// </summary>
    Task<IReadOnlyList<SprCategory>> GetAncestorsAsync(
        int categoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches categories by name.
    /// </summary>
    Task<IReadOnlyList<SprCategory>> SearchByNameAsync(
        string searchTerm,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a category.
    /// </summary>
    Task<SprCategory> UpsertAsync(
        SprCategory category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk upserts categories (for import).
    /// </summary>
    Task BulkUpsertAsync(
        IEnumerable<SprCategory> categories,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active categories.
    /// </summary>
    Task<IReadOnlyList<SprCategory>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks categories as inactive if not in the provided list.
    /// </summary>
    Task DeactivateExceptAsync(
        IEnumerable<string> activeCategoryCodes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets category counts by level.
    /// </summary>
    Task<Dictionary<int, int>> GetCountsByLevelAsync(CancellationToken cancellationToken = default);
}
