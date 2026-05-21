namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize locale definitions.
/// Locales represent language + market combinations (e.g., EN_US = English USA).
/// </summary>
public class SprRawLocale
{
    public long Id { get; set; }
    public string? LocaleId { get; set; }
    public string? IsActive { get; set; }
    public string? LanguageCode { get; set; }
    public string? CountryCode { get; set; }
    public string? Name { get; set; }
}
