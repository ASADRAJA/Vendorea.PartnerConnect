namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// A trading partner's distribution center (DC) reference data — e.g., SPR's DC address list
/// (SPRCP-00103). Stored as shared, partner-level master data and used to resolve the bare DC
/// numbers that appear in partner feeds (e.g. the SPR inventory <c>I1</c>/<c>Q1</c> records) into
/// meaningful location/contact information. City/State/PostalCode are kept as separate columns so
/// downstream logic (e.g. ZIP-based routing) can use them independently.
/// </summary>
public class PartnerDistributionCenter
{
    public int Id { get; set; }

    /// <summary>Owning trading partner (the DCs belong to a partner like SPR).</summary>
    public int TradingPartnerId { get; set; }

    /// <summary>The partner's DC number (e.g., SPR DC 1, 2, 39).</summary>
    public int DcNumber { get; set; }

    /// <summary>The DC's headline label as published (e.g. "Atlanta, GA"); "Corporate HQ" for HQ.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The parenthetical area/alias published with the DC (e.g. "Lithia Springs", "Chicago").</summary>
    public string? Area { get; set; }

    public string? ContactName { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }

    /// <summary>Physical address city (may differ from the marketing <see cref="Label"/> city).</summary>
    public string? City { get; set; }

    /// <summary>Two-letter state/province code.</summary>
    public string? State { get; set; }

    /// <summary>Postal/ZIP code — separate column to support ZIP-only logic.</summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Region grouping for the DC. Nullable and left unpopulated for now — SPR has not provided a
    /// region mapping. Reserved for future region-based logic.
    /// </summary>
    public string? Region { get; set; }

    public string? Phone { get; set; }
    public string? TollFreePhone { get; set; }
    public string? Fax { get; set; }

    /// <summary>
    /// Lossless catch-all for any published detail that doesn't fit a structured column
    /// (e.g. a second fax, extra labeled phone lines).
    /// </summary>
    public string? AdditionalContactInfo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public TradingPartner? TradingPartner { get; set; }
}
