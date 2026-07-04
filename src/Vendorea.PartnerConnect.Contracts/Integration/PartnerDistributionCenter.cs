namespace Vendorea.PartnerConnect.Contracts.Integration;

/// <summary>
/// Org-facing view of a trading partner's distribution center (reference/address data, e.g. SPR's
/// published DC list). Returned by <c>GET /api/v1/org/partners/{partnerCode}/distribution-centers</c>.
/// Internal columns (surrogate id, owning partner id, audit timestamps) are intentionally omitted.
/// </summary>
public class PartnerDistributionCenterDto
{
    /// <summary>The partner's DC number (e.g. SPR DC 1, 2, 39).</summary>
    public int DcNumber { get; set; }

    /// <summary>Headline label as published (e.g. "Atlanta, GA").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Parenthetical area/alias published with the DC (e.g. "Lithia Springs").</summary>
    public string? Area { get; set; }

    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>Region grouping (currently unpopulated — the partner has not provided a mapping).</summary>
    public string? Region { get; set; }

    public string? ContactName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Phone { get; set; }
    public string? TollFreePhone { get; set; }
    public string? Fax { get; set; }

    /// <summary>Any published detail that doesn't fit a structured column (e.g. a second fax line).</summary>
    public string? AdditionalContactInfo { get; set; }
}
