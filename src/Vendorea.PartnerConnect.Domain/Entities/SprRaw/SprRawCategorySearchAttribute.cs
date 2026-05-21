namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize category search attributes.
/// Defines which attributes are searchable for parametric/faceted search.
/// </summary>
public class SprRawCategorySearchAttribute
{
    public long Id { get; set; }
    public string? CategoryId { get; set; }
    public string? AttributeId { get; set; }
    public string? IsActive { get; set; }
    public string? IsPreferred { get; set; }
    public string? LastUpdated { get; set; }
}
