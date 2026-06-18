namespace Vendorea.PartnerConnect.Contracts.Integration;

/// <summary>
/// M360-facing request for a live stock/price check against a trading partner (SPR). The caller is
/// an authenticated organization; <see cref="ExternalTenantId"/> identifies the dealer/tenant whose
/// partner connection (and account-specific pricing) should be used.
/// </summary>
public class StockCheckRequest
{
    /// <summary>The org-side id of the dealer/tenant making the check (the tenant's ExternalId).</summary>
    public string ExternalTenantId { get; set; } = string.Empty;

    /// <summary>Partner item number (for SPR: Mfr Id + Stock Number).</summary>
    public string ItemNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional list of distribution-center numbers to check (1–8). When provided, a lightweight
    /// per-DC check is used; when omitted, availability is returned for all stocking DCs.
    /// </summary>
    public List<int>? DcNumbers { get; set; }

    /// <summary>True (default): only return DCs with quantity available to sell.</summary>
    public bool AvailableOnly { get; set; } = true;
}

/// <summary>M360-facing response for a stock/price check.</summary>
public class StockCheckResponse
{
    /// <summary>True if the partner returned a successful result.</summary>
    public bool Success { get; set; }

    /// <summary>Partner status/error message (e.g. "OK").</summary>
    public string? Message { get; set; }

    // Item attributes
    public string? ItemNumber { get; set; }
    public string? Upc { get; set; }
    public string? Description { get; set; }
    public string? ItemStatus { get; set; }
    public string? UnitOfMeasure { get; set; }
    public int? OrderMinimum { get; set; }
    public decimal? RetailPrice { get; set; }
    public string? HazmatMessage { get; set; }

    /// <summary>True when dealer-specific pricing was included (dealer is connected to the partner).</summary>
    public bool PricingIncluded { get; set; }
    public decimal? DealerPrice { get; set; }
    public bool? Discountable { get; set; }
    public string? PriceDescription { get; set; }

    public List<DcAvailability> DistributionCenters { get; set; } = new();
}

/// <summary>Per-distribution-center availability in a stock-check response.</summary>
public class DcAvailability
{
    public string DcNumber { get; set; } = string.Empty;
    public string? DcName { get; set; }
    public int Available { get; set; }
    public string? UnitOfMeasure { get; set; }
    public int? OnOrder { get; set; }
    /// <summary>Expected manufacturer delivery (days, or partner codes like DUE/LATE).</summary>
    public string? Expected { get; set; }
    public bool Sprinter { get; set; }
    public string? CutOff { get; set; }
    public string? LeadTime { get; set; }
    public string? DcType { get; set; }
}
