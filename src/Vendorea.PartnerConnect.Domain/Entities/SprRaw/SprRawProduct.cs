namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize product table - the central table in the inQuire database.
/// </summary>
public class SprRawProduct
{
    public long Id { get; set; }
    public string? ProductId { get; set; }
    public string? ManufacturerId { get; set; }
    public string? IsActive { get; set; }
    public string? MfgPartNo { get; set; }
    public string? CategoryId { get; set; }
    public string? IsAccessory { get; set; }
    public string? Equivalency { get; set; }
    public string? CreationDate { get; set; }
    public string? ModifiedDate { get; set; }
    public string? LastUpdated { get; set; }
}
