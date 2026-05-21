namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize product locale assignments.
/// </summary>
public class SprRawProductLocale
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? LocaleId { get; set; }
    public string? IsActive { get; set; }
    public string? Status { get; set; }
}
