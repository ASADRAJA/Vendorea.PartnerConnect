namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize attribute names by locale.
/// AttributeId prefixes: 32=Detailed, 34=Basic, 33=PreferredSearch, 2=NonPreferredSearch
/// </summary>
public class SprRawAttributeName
{
    public long Id { get; set; }
    public string? AttributeId { get; set; }
    public string? Name { get; set; }
    public string? LocaleId { get; set; }
}
