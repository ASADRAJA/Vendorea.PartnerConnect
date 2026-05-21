namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize measurement units with conversion factors.
/// </summary>
public class SprRawUnit
{
    public long Id { get; set; }
    public string? UnitId { get; set; }
    public string? Name { get; set; }
    public string? BaseUnitId { get; set; }
    public string? Multiple { get; set; }
}
