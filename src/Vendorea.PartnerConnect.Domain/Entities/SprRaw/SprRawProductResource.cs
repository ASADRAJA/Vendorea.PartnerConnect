namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize product resources (Rebates, MSDS sheets, etc.).
/// </summary>
public class SprRawProductResource
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? SkuName { get; set; }
    public string? Sku { get; set; }
    public string? Type { get; set; }
    public string? Url { get; set; }
    public string? Text { get; set; }
    public string? LocaleId { get; set; }
    public string? Status { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}
