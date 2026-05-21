namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize unit names by locale.
/// </summary>
public class SprRawUnitName
{
    public long Id { get; set; }
    public string? UnitId { get; set; }
    public string? Name { get; set; }
    public string? LocaleId { get; set; }
}
