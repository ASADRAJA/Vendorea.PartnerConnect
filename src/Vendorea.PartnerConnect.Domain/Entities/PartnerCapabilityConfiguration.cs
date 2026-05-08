namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Defines the capabilities and integration features available for a trading partner.
/// Different partners may support different document types, formats, and protocols.
/// </summary>
public class PartnerCapabilityConfiguration
{
    public int Id { get; set; }
    public int TradingPartnerId { get; set; }
    public PartnerCapability Capability { get; set; }
    public bool IsEnabled { get; set; }
    public string? ConfigurationJson { get; set; }
    public string? AdapterType { get; set; }
    public string? EndpointUrl { get; set; }
    public string? ProtocolType { get; set; }
    public string? FileFormat { get; set; }
    public int? PollingIntervalMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public TradingPartner? TradingPartner { get; set; }
}

public enum PartnerCapability
{
    PriceFeed,
    InventoryFeed,
    ProductContent,
    OrderSubmission,
    OrderStatusUpdates,
    InvoiceReceive,
    ShipmentTracking,
    ReturnProcessing,
    CatalogSync
}
