namespace Vendorea.PartnerConnect.Contracts.Interfaces;

/// <summary>
/// Client interface for communicating with the Merchant360 API.
/// Used to push processed data (prices, inventory, products) to merchants.
/// </summary>
public interface IMerchant360Client
{
    /// <summary>
    /// Gets all merchants from Merchant360.
    /// </summary>
    Task<IReadOnlyList<Merchant360Merchant>> GetMerchantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates product prices in Merchant360 for a specific merchant.
    /// Includes trading partner metadata for M360 to upsert partner record.
    /// </summary>
    Task<PriceUpdateResult> UpdatePricesAsync(int merchantId, TradingPartnerInfo tradingPartner, IEnumerable<PriceUpdateItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates inventory levels in Merchant360 for a specific merchant.
    /// Includes trading partner metadata for M360 to upsert partner record.
    /// </summary>
    Task<InventoryUpdateResult> UpdateInventoryAsync(int merchantId, TradingPartnerInfo tradingPartner, IEnumerable<InventoryUpdateItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to the Merchant360 API.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    // Subscription management
    Task<SubscriptionListResult> GetSubscriptionsAsync(string? status, int? tenantId, int? tradingPartnerId, CancellationToken cancellationToken = default);
    Task<MerchantSubscriptionDto?> GetSubscriptionAsync(int id, CancellationToken cancellationToken = default);
    Task<MerchantSubscriptionDto?> CreateSubscriptionAsync(CreateSubscriptionDto request, CancellationToken cancellationToken = default);
    Task<bool> ApproveSubscriptionAsync(int id, ApproveSubscriptionDto request, CancellationToken cancellationToken = default);
    Task<bool> DenySubscriptionAsync(int id, DenySubscriptionDto request, CancellationToken cancellationToken = default);
    Task<bool> SuspendSubscriptionAsync(int id, SuspendSubscriptionDto request, CancellationToken cancellationToken = default);
    Task<bool> ReactivateSubscriptionAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Trading partner metadata included in push payloads for M360 to upsert.
/// </summary>
public record TradingPartnerInfo(
    int PartnerConnectId,
    string Code,
    string Name,
    string? Description,
    string? LogoUrl);

public record PriceUpdateItem(
    string Sku,
    decimal Cost,
    decimal? ListPrice,
    string? CurrencyCode);

public record PriceUpdateResult(
    bool Success,
    int UpdatedCount,
    int SkippedCount,
    int ErrorCount,
    IReadOnlyList<string>? Errors);

public record InventoryUpdateItem(
    string Sku,
    int QuantityAvailable,
    int? QuantityOnOrder,
    string? WarehouseCode);

public record InventoryUpdateResult(
    bool Success,
    int UpdatedCount,
    int SkippedCount,
    int ErrorCount,
    IReadOnlyList<string>? Errors);

public record Merchant360Merchant(
    int Id,
    string Name,
    string? Code,
    bool IsActive);

// Subscription DTOs
public enum SubscriptionStatus
{
    Pending,
    Approved,
    Denied,
    Suspended
}

public class SubscriptionListResult
{
    public int Total { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int DeniedCount { get; set; }
    public int SuspendedCount { get; set; }
    public List<MerchantSubscriptionDto> Items { get; set; } = new();
}

public class MerchantSubscriptionDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantCode { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public string? TradingPartnerName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApprovedByUserName { get; set; }
    public string? DenialReason { get; set; }
    public string? Notes { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public int? SuspendedByUserId { get; set; }
    public string? SuspendedByUserName { get; set; }
}

public class CreateSubscriptionDto
{
    public int TenantId { get; set; }
    public int TradingPartnerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ApproveSubscriptionDto
{
    public string? Notes { get; set; }
}

public class DenySubscriptionDto
{
    public string DenialReason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class SuspendSubscriptionDto
{
    public string? Notes { get; set; }
}
