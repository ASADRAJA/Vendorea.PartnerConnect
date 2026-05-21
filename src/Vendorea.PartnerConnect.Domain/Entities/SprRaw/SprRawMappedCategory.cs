namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR mapped category - maps products to SPR's category structure (iteminfo.com).
/// </summary>
public class SprRawMappedCategory
{
    public string? ProductId { get; set; }
    public string? CategoryId { get; set; }
}
