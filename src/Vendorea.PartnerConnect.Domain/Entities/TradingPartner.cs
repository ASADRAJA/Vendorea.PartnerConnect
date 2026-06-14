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

    /// <summary>
    /// Partner's "requirements for tenant confirmation": an ordered JSON list of field names
    /// (collected as free text on the connection form) that PC staff use to verify a tenant
    /// connection with the partner before approval. Authored by partner registration (future).
    /// </summary>
    public string? TenantConfirmationFieldsJson { get; set; }

    /// <summary>
    /// Partner-level shared transport configuration (PC's single connection with this partner):
    /// SFTP host/paths, EDI/SOAP settings, etc. as JSON. Shared across all of the partner's
    /// tenant connections. (Convergence: this replaces per-dealer transport config.)
    /// </summary>
    public string? TransportConfigJson { get; set; }

    /// <summary>
    /// Partner-level shared transport credentials (e.g., SFTP password/key) as JSON,
    /// encrypted at rest via ICredentialProtector.
    /// </summary>
    public string? TransportCredentialsJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

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
