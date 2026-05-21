namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize product attribute values.
/// Each product has a set of attributes with display values and optional absolute values.
/// </summary>
public class SprRawProductAttribute
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? AttributeId { get; set; }
    public string? CategoryId { get; set; }
    public string? DisplayValue { get; set; }
    public string? AbsoluteValue { get; set; }
    public string? UnitId { get; set; }
    public string? IsAbsolute { get; set; }
    public string? IsActive { get; set; }
    public string? LocaleId { get; set; }
}
