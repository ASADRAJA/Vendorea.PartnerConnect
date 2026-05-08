namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents the connection between a dealer (Merchant360 tenant) and a trading partner.
/// Supports multi-dealer scenarios where different dealers may have different
/// credentials and configurations for the same partner.
/// </summary>
public class DealerPartnerConnection
{
    public int Id { get; set; }
    public int DealerId { get; set; }
    public int TradingPartnerId { get; set; }
    public string? ExternalAccountId { get; set; }
    public ConnectionStatus Status { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime? DisconnectedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? CredentialsJson { get; set; }
    public string? ConfigurationJson { get; set; }

    public TradingPartner? TradingPartner { get; set; }
    public ICollection<PartnerDocument> Documents { get; set; } = new List<PartnerDocument>();
}

public enum ConnectionStatus
{
    Pending,
    Active,
    Suspended,
    Disconnected,
    Error
}
