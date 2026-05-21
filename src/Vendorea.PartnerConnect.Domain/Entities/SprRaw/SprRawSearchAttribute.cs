namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize optimized search attributes.
/// Pre-indexed for faster parametric search queries.
/// </summary>
public class SprRawSearchAttribute
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? AttributeId { get; set; }
    public string? ValueId { get; set; }
    public string? AbsoluteValue { get; set; }
    public string? IsAbsolute { get; set; }
    public string? LocaleId { get; set; }
}
