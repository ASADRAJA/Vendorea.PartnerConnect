namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize product image references.
/// Images are hosted at: https://content.etilize.com/{Type}/{ProductId}.jpg
/// </summary>
public class SprRawProductImage
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
}
