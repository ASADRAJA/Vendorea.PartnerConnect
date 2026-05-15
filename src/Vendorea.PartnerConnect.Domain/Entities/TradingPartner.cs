namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents an external trading partner (wholesaler, distributor, supplier)
/// that exchanges data with the Vendorea platform.
/// </summary>
public class TradingPartner
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TradingPartnerType PartnerType { get; set; }
    public TradingPartnerStatus Status { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? LogoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<DealerPartnerConnection> DealerConnections { get; set; } = new List<DealerPartnerConnection>();
    public ICollection<PartnerCapabilityConfiguration> Capabilities { get; set; } = new List<PartnerCapabilityConfiguration>();
}

public enum TradingPartnerType
{
    Wholesaler,
    Distributor,
    Supplier,
    Marketplace,
    DropshipVendor
}

public enum TradingPartnerStatus
{
    Pending,
    Active,
    Suspended,
    Inactive
}
