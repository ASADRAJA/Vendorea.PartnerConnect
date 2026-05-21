namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize product feature bullets.
/// Marketing bullet points for products.
/// </summary>
public class SprRawProductFeature
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? LocaleId { get; set; }
    public string? SequenceNo { get; set; }
    public string? BulletText { get; set; }
}
