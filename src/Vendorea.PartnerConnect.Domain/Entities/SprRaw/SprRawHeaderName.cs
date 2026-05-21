namespace Vendorea.PartnerConnect.Domain.Entities.SprRaw;

/// <summary>
/// Raw SPR/Etilize header names by locale.
/// Headers group attributes (e.g., "General Information", "Battery Information").
/// </summary>
public class SprRawHeaderName
{
    public long Id { get; set; }
    public string? HeaderId { get; set; }
    public string? Name { get; set; }
    public string? LocaleId { get; set; }
}
