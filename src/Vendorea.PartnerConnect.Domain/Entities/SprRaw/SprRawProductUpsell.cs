namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize upsell products.
/// Products with higher equivalency (better features) within the same category.
/// </summary>
public class SprRawProductUpsell
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? UpsellProductId { get; set; }
    public string? LocaleId { get; set; }
}
