namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

/// <summary>
/// Result of a stock-check family call (Stock Check / Dealer Stock Check / Quick Check Plus).
/// Dealer pricing fields are populated only when the service returns them (dealer / quick-check).
/// </summary>
public class SprStockCheckResult
{
    public bool Success { get; set; }
    /// <summary>SPR return status code ("0000" = success).</summary>
    public string? RtnStatus { get; set; }
    public string? RtnMessage { get; set; }
    public string? ErrorMessage { get; set; }

    // Item attributes
    public string? SprItemNumber { get; set; }
    public string? StripNumber { get; set; }
    public string? Upc { get; set; }
    public string? ItemStatus { get; set; }
    public string? Description { get; set; }
    public string? SellUom { get; set; }
    public int? OrderMinimum { get; set; }
    public decimal? RetailPrice { get; set; }
    public string? RetailUom { get; set; }
    public string? HazmatMessage { get; set; }
    public string? UpchargeMessage { get; set; }

    // Dealer pricing (Dealer Stock Check / Quick Check Plus)
    public decimal? DealerPrice { get; set; }
    public bool? Discountable { get; set; }
    public string? PriceDescription { get; set; }
    public string? TariffMessage { get; set; }

    public List<SprDcStock> Dcs { get; set; } = new();
}

/// <summary>Per-DC availability returned by the stock-check services.</summary>
public class SprDcStock
{
    public string DcNumber { get; set; } = string.Empty;
    public string? DcName { get; set; }
    public int Available { get; set; }
    public string? Uom { get; set; }
    public int? OnOrder { get; set; }
    /// <summary>Expected manufacturer delivery (days, or codes like DUE/LATE).</summary>
    public string? Expected { get; set; }
    public bool Sprinter { get; set; }
    public string? CutOff { get; set; }
    public string? LeadTime { get; set; }
    public string? DcType { get; set; }
}

/// <summary>Result of a connectivity heartbeat (Action = "?").</summary>
public class SprPingResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public TimeSpan ResponseTime { get; set; }
}
