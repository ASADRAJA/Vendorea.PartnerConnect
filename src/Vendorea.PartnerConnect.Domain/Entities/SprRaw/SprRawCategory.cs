namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize category hierarchy (Etilize taxonomy).
/// Categories exist in a tree-like hierarchical structure.
/// </summary>
public class SprRawCategory
{
    public long Id { get; set; }
    public string? CategoryId { get; set; }
    public string? ParentCategoryId { get; set; }
    public string? IsActive { get; set; }
    public string? OrderNumber { get; set; }
    public string? CatLevel { get; set; }
    public string? DisplayOrder { get; set; }
    public string? LastUpdated { get; set; }
}
