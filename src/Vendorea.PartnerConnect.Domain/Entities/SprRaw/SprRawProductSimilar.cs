namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize similar/cross-sell products.
/// Products with similar equivalency within a category.
/// </summary>
public class SprRawProductSimilar
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? SimilarProductId { get; set; }
    public string? LocaleId { get; set; }
}
