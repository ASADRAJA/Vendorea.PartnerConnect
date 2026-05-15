namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// SPR product category hierarchy.
/// Categories are organized in levels: Master -> Department -> Class -> SubClass
/// </summary>
public class SprCategory
{
    public int Id { get; set; }

    /// <summary>
    /// SPR category code identifier.
    /// </summary>
    public string CategoryCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the category.
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Parent category ID for hierarchy. Null for root categories.
    /// </summary>
    public int? ParentCategoryId { get; set; }

    /// <summary>
    /// Navigation property to parent category.
    /// </summary>
    public SprCategory? ParentCategory { get; set; }

    /// <summary>
    /// Child categories in the hierarchy.
    /// </summary>
    public ICollection<SprCategory> ChildCategories { get; set; } = new List<SprCategory>();

    /// <summary>
    /// Hierarchy level: 0=Root, 1=Master, 2=Department, 3=Class, 4=SubClass
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Materialized full path for efficient ancestor queries (e.g., "1/24/156/789").
    /// </summary>
    public string? FullPath { get; set; }

    /// <summary>
    /// Whether this category is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// UNSPSC code if mapped.
    /// </summary>
    public string? UnspscCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
