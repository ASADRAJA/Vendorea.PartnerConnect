namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize product keywords for full-text search.
/// </summary>
public class SprRawProductKeyword
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? Keywords { get; set; }
    public string? LocaleId { get; set; }
}
