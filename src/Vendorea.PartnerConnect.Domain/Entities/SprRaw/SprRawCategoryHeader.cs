namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize category headers.
/// Headers group attributes for display (e.g., "General Information", "Technical Specifications").
/// </summary>
public class SprRawCategoryHeader
{
    public long Id { get; set; }
    public string? HeaderId { get; set; }
    public string? CategoryId { get; set; }
    public string? IsActive { get; set; }
    public string? TemplateType { get; set; }
    public string? DefaultDisplayOrder { get; set; }
    public string? DisplayOrder { get; set; }
    public string? LastUpdated { get; set; }
}
