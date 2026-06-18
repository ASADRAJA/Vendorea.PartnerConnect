namespace Vendorea.PartnerConnect.Contracts.Integration;

/// <summary>
/// M360-facing request for SPR freight rates for a shipment. Used by both the "all rates" and
/// "lowest rate" endpoints. The dealer (identified by <see cref="ExternalTenantId"/>) must have an
/// active SPR connection.
/// </summary>
public class FreightRateRequest
{
    public string ExternalTenantId { get; set; } = string.Empty;

    /// <summary>Ship-from SPR distribution center number.</summary>
    public int ShipFromDc { get; set; }

    /// <summary>Destination state/province code (e.g. "GA").</summary>
    public string DestinationState { get; set; } = string.Empty;

    /// <summary>Destination postal/ZIP code.</summary>
    public string DestinationZip { get; set; } = string.Empty;

    /// <summary>Total shipment weight in pounds.</summary>
    public decimal TotalWeight { get; set; }

    /// <summary>Optional carrier code (SPR Table 5: e.g. UPS, FDX, PCS). Empty = all carriers.</summary>
    public string? Carrier { get; set; }

    /// <summary>Service-level code (SPR Table 4: 00–09). Required for lowest-rate.</summary>
    public string? ServiceLevel { get; set; }

    /// <summary>Whether the destination is a residential address.</summary>
    public bool Residential { get; set; }
}

/// <summary>M360-facing freight-rate response. "Lowest rate" returns at most one option.</summary>
public class FreightRateResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<FreightRateOption> Rates { get; set; } = new();
}

public class FreightRateOption
{
    public string? ShipFromDc { get; set; }
    public string? Carrier { get; set; }
    public string? CarrierDescription { get; set; }
    /// <summary>SPR ship-via code (Table 6, e.g. UPSN, FXGD).</summary>
    public string? ShipVia { get; set; }
    public decimal? Rate { get; set; }
    public int? DeliveryDays { get; set; }
    public int? NumberOfCartons { get; set; }
    public string? ServiceLevel { get; set; }
    public bool Residential { get; set; }
}
