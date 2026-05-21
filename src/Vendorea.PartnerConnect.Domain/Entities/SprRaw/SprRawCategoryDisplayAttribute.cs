namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize category display attributes.
/// Defines which attributes are shown for products in a category,
/// grouped by headers, with template type (0=Basic, 1=Detailed).
/// </summary>
public class SprRawCategoryDisplayAttribute
{
    public long Id { get; set; }
    public string? HeaderId { get; set; }
    public string? CategoryId { get; set; }
    public string? AttributeId { get; set; }
    public string? IsActive { get; set; }
    public string? TemplateType { get; set; }
    public string? DefaultDisplayOrder { get; set; }
    public string? DisplayOrder { get; set; }
    public string? LastUpdated { get; set; }
}
