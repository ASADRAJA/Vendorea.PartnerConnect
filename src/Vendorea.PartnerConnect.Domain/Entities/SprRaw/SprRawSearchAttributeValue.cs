namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize search attribute values.
/// Lookup table for search_attribute valueid references.
/// </summary>
public class SprRawSearchAttributeValue
{
    public long Id { get; set; }
    public string? ValueId { get; set; }
    public string? Value { get; set; }
    public string? AbsoluteValue { get; set; }
    public string? UnitId { get; set; }
    public string? IsAbsolute { get; set; }
}
