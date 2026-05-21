namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize manufacturer information.
/// </summary>
public class SprRawManufacturer
{
    public long Id { get; set; }
    public string? ManufacturerId { get; set; }
    public string? Name { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? Zip { get; set; }
    public string? Url { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? LastUpdated { get; set; }
}
