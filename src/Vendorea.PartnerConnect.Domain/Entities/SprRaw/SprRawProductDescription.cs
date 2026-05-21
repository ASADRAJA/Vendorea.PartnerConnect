namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize product descriptions.
/// Types: 0=Manual (SPR), 1=Full, 2=MainTitle, 3=SubTitle, 20-29=Features/Benefits
/// </summary>
public class SprRawProductDescription
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? Description { get; set; }
    public string? IsDefault { get; set; }
    public string? Type { get; set; }
    public string? LocaleId { get; set; }
}
