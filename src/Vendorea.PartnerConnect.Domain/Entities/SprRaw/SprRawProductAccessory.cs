namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize product accessories and options.
/// Options are required for product function; accessories are optional enhancements.
/// Also includes "Also Bought" relationships when Note contains "Also Bought".
/// </summary>
public class SprRawProductAccessory
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? AccessoryProductId { get; set; }
    public string? IsActive { get; set; }
    public string? IsPreferred { get; set; }
    public string? IsOption { get; set; }
    public string? Note { get; set; }
    public string? RecommendationWeight { get; set; }
}
