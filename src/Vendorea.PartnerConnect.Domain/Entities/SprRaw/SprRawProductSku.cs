namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize product SKUs from distributors (SPR, Horizon, TechData, etc.),
/// UPC codes, and UNSPSC codes.
/// </summary>
public class SprRawProductSku
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? Name { get; set; }
    public string? Sku { get; set; }
    public string? LocaleId { get; set; }
    public string? AddedDate { get; set; }
    public string? DiscontinuedDate { get; set; }
}
