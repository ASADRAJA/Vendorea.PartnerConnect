namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize category names by locale.
/// </summary>
public class SprRawCategoryName
{
    public long Id { get; set; }
    public string? CategoryId { get; set; }
    public string? Name { get; set; }
    public string? LocaleId { get; set; }
}
