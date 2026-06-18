namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

/// <summary>
/// Connection + auth for SPR's interactive web services (Stock Check / Dealer Stock Check /
/// Quick Check Plus / freight). Auth is carried in the request body as a GroupCode/UserID/Password
/// triple (not HTTP/SOAP-header auth). Built from the partner's transport config + decrypted creds.
/// </summary>
public class SprWebServiceConfig
{
    /// <summary>Base URL, e.g. "http://sprws.sprich.com/sprws/". Service name + ".php" is appended.</summary>
    public string BaseUrl { get; set; } = "http://sprws.sprich.com/sprws/";

    public string GroupCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Dealer's SPR customer/account number. Required for dealer pricing; optional otherwise.</summary>
    public string? CustNumber { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>Query parameters for the stock-check family of services.</summary>
public class SprStockCheckQuery
{
    /// <summary>SPR item number (Mfr Id + Stock Number).</summary>
    public string ItemNumber { get; set; } = string.Empty;

    /// <summary>Specific DC numbers to check (Quick Check Plus: 1–8). Empty = all DCs (Stock/Dealer).</summary>
    public IReadOnlyList<int> DcNumbers { get; set; } = Array.Empty<int>();

    /// <summary>Y: only return DCs with quantity available to sell (default). N: all DCs.</summary>
    public bool AvailableOnly { get; set; } = true;

    /// <summary>Convert minimum order qty to full packs.</summary>
    public bool MinInFullPacks { get; set; }

    /// <summary>A = sort alphabetically (default), N = numerically.</summary>
    public char SortBy { get; set; } = 'A';
}

/// <summary>Query parameters for the freight-rate services.</summary>
public class SprFreightQuery
{
    public int ShipFromDc { get; set; }
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    /// <summary>Carrier code (Table 5); empty = all carriers.</summary>
    public string? Carrier { get; set; }
    /// <summary>Service-level code (Table 4).</summary>
    public string? ServiceLevel { get; set; }
    public bool Residential { get; set; }
}
