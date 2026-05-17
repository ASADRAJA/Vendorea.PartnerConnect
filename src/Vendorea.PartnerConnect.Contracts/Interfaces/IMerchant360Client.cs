using System.ComponentModel;

namespace Vendorea.PartnerConnect.Contracts.Interfaces;

/// <summary>
/// Client interface for communicating with the Merchant360 API.
/// Used to push processed data (prices, content) to merchants.
/// </summary>
public interface IMerchant360Client
{
    /// <summary>
    /// Tests connectivity to the Merchant360 API.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active merchants from Merchant360.
    /// Used by PC admin UI to select which merchant receives a price upload.
    /// </summary>
    Task<IReadOnlyList<Merchant360Merchant>> GetMerchantsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets trading partners from Merchant360.
    /// Used by PC to map its supplier/partner records to M360 trading partner IDs.
    /// </summary>
    Task<IReadOnlyList<Merchant360TradingPartner>> GetTradingPartnersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a batch of prices to Merchant360 for a specific merchant.
    /// Price data is per-merchant.
    /// </summary>
    Task<PriceBatchResponse> PushPriceBatchAsync(
        int merchantId,
        PriceBatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a batch of product content to Merchant360.
    /// Content is shared across all merchants.
    /// </summary>
    Task<ContentBatchResponse> PushContentBatchAsync(
        ContentBatchRequest request,
        CancellationToken cancellationToken = default);

    #region Phase 2 - Inventory (Disabled)

    /// <summary>
    /// Updates inventory levels in Merchant360 for a specific merchant.
    /// </summary>
    /// <remarks>Phase 2 - Not implemented in current release.</remarks>
    [Obsolete("Inventory push is Phase 2. Do not use in Phase 1.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    Task<InventoryUpdateResult> UpdateInventoryAsync(
        int merchantId,
        int tradingPartnerId,
        IEnumerable<InventoryUpdateItem> items,
        CancellationToken cancellationToken = default);

    #endregion

    #region Subscription Management (Stub for Phase 1)

    // Subscription workflow is documented but not expanded in Phase 1.
    // Future: M360 creates pending subscription → PC admin approves/denies → PC starts pushing data

    Task<SubscriptionListResult> GetSubscriptionsAsync(string? status, int? tenantId, int? tradingPartnerId, CancellationToken cancellationToken = default);
    Task<MerchantSubscriptionDto?> GetSubscriptionAsync(int id, CancellationToken cancellationToken = default);
    Task<MerchantSubscriptionDto?> CreateSubscriptionAsync(CreateSubscriptionDto request, CancellationToken cancellationToken = default);
    Task<bool> ApproveSubscriptionAsync(int id, ApproveSubscriptionDto request, CancellationToken cancellationToken = default);
    Task<bool> DenySubscriptionAsync(int id, DenySubscriptionDto request, CancellationToken cancellationToken = default);
    Task<bool> SuspendSubscriptionAsync(int id, SuspendSubscriptionDto request, CancellationToken cancellationToken = default);
    Task<bool> ReactivateSubscriptionAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies Merchant360 about a subscription status change.
    /// Called when PC admin approves, denies, suspends, reactivates, or unsubscribes a subscription.
    /// This callback is required to succeed - throws exception on failure.
    /// </summary>
    Task<bool> NotifySubscriptionStatusChangedAsync(
        SubscriptionStatusChangedDto statusChange,
        CancellationToken cancellationToken = default);

    #endregion
}

#region Merchant & Trading Partner DTOs

public record Merchant360Merchant(
    int Id,
    string Name,
    string? Code,
    bool IsActive);

public record Merchant360TradingPartner(
    int Id,
    string Code,
    string Name,
    bool IsActive);

#endregion

#region Price Batch DTOs (Per-Merchant)

/// <summary>
/// Request to push a batch of prices to M360 for a specific merchant.
/// </summary>
public class PriceBatchRequest
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public int SourceUploadId { get; set; }
    public DateTime UploadedAt { get; set; }
    public List<PriceBatchItem> Items { get; set; } = new();
}

/// <summary>
/// Individual price item in a batch.
/// </summary>
public class PriceBatchItem
{
    public string StockNumber { get; set; } = string.Empty;
    public string? ProductDescription { get; set; }
    public decimal NetCost { get; set; }
    public decimal? RetailListPrice { get; set; }
    public string? Uom { get; set; }
    public int? UomFactor { get; set; }
    public string? CategoryCode { get; set; }
    public string? SubcategoryCode { get; set; }
    public string? BrandCode { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public string? UpcCode { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Response from M360 after pushing a price batch.
/// </summary>
public class PriceBatchResponse
{
    public bool Success { get; set; }
    public int MerchantId { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public int RecordsReceived { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsSkipped { get; set; }
    public int? SyncLogId { get; set; }
    public List<string>? Errors { get; set; }
}

#endregion

#region Content Batch DTOs (Shared across merchants)

/// <summary>
/// Request to push a batch of product content to M360.
/// Content is shared across all merchants.
/// </summary>
public class ContentBatchRequest
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public string ContentVersion { get; set; } = string.Empty;
    public string Locale { get; set; } = "EN_US";
    public int SourceUploadId { get; set; }
    public List<ContentBatchProduct> Products { get; set; } = new();
}

/// <summary>
/// Individual product content in a batch.
/// </summary>
public class ContentBatchProduct
{
    public string StockNumber { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public string? BrandName { get; set; }
    public string? ManufacturerName { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public string? UpcCode { get; set; }
    public string? CategoryPath { get; set; }
    public string? ImageUrl225 { get; set; }
    public string? ImageUrl75 { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public List<ContentSpecification> Specifications { get; set; } = new();
    public List<ContentFeature> Features { get; set; } = new();
    public List<ContentRelatedProduct> RelatedProducts { get; set; } = new();
}

public class ContentSpecification
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Group { get; set; }
}

public class ContentFeature
{
    public string? Headline { get; set; }
    public string? Description { get; set; }
}

public class ContentRelatedProduct
{
    public string StockNumber { get; set; } = string.Empty;
    public string? RelationshipType { get; set; }
}

/// <summary>
/// Response from M360 after pushing a content batch.
/// </summary>
public class ContentBatchResponse
{
    public bool Success { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public string? ContentVersion { get; set; }
    public string? Locale { get; set; }
    public int RecordsReceived { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsSkipped { get; set; }
    public int SpecificationsProcessed { get; set; }
    public int FeaturesProcessed { get; set; }
    public int RelationshipsProcessed { get; set; }
    public int? SyncLogId { get; set; }
    public List<string>? Errors { get; set; }
}

#endregion

#region Inventory DTOs (Phase 2 - Disabled)

[Obsolete("Inventory is Phase 2. Do not use in Phase 1.")]
public record InventoryUpdateItem(
    string StockNumber,
    int QuantityAvailable,
    int? QuantityOnOrder,
    string? WarehouseCode);

[Obsolete("Inventory is Phase 2. Do not use in Phase 1.")]
public record InventoryUpdateResult(
    bool Success,
    int UpdatedCount,
    int SkippedCount,
    int ErrorCount,
    IReadOnlyList<string>? Errors);

#endregion

#region Subscription DTOs (Stub for Phase 1)

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

/// <summary>
/// DTO sent to M360 when a subscription status changes.
/// M360 can look up the subscription by TenantId + TradingPartnerId (unique).
/// </summary>
public class SubscriptionStatusChangedDto
{
    /// <summary>
    /// The M360 tenant ID (merchant).
    /// </summary>
    public int TenantId { get; set; }

    /// <summary>
    /// The trading partner ID in PartnerConnect.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// The trading partner code (e.g., "SPR").
    /// </summary>
    public string TradingPartnerCode { get; set; } = string.Empty;

    /// <summary>
    /// The merchant's account number with the trading partner.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// The previous status before the change.
    /// </summary>
    public string PreviousStatus { get; set; } = string.Empty;

    /// <summary>
    /// The new status after the change.
    /// </summary>
    public string NewStatus { get; set; } = string.Empty;

    /// <summary>
    /// When the status change occurred.
    /// </summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>
    /// Who made the change (e.g., "admin", user email).
    /// </summary>
    public string ChangedBy { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the change (e.g., denial reason).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Additional notes about the change.
    /// </summary>
    public string? Notes { get; set; }
}

#endregion
