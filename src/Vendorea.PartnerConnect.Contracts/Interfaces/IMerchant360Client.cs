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

    /// <summary>
    /// Pushes a batch of categories to Merchant360.
    /// Categories should be synced before products.
    /// </summary>
    Task<CategoryBatchResponse> PushCategoryBatchAsync(
        CategoryBatchRequest request,
        CancellationToken cancellationToken = default);

    #region Order Updates

    /// <summary>
    /// Pushes order status updates to Merchant360.
    /// Called when we receive POA (855), ASN (856), or Invoice (810) from supplier.
    /// </summary>
    Task<OrderStatusUpdateResult> PushOrderStatusUpdateAsync(
        int merchantId,
        OrderStatusUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes shipment tracking information to Merchant360.
    /// Called when we receive ASN (856) with tracking details.
    /// </summary>
    Task<ShipmentUpdateResult> PushShipmentUpdateAsync(
        int merchantId,
        ShipmentUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes invoice information to Merchant360.
    /// Called when we receive Invoice (810) from supplier.
    /// </summary>
    Task<InvoiceUpdateResult> PushInvoiceUpdateAsync(
        int merchantId,
        InvoiceUpdateRequest request,
        CancellationToken cancellationToken = default);

    #endregion

    #region Inventory Updates

    /// <summary>
    /// Updates inventory levels in Merchant360 for a specific merchant.
    /// </summary>
    Task<InventoryUpdateResult> UpdateInventoryAsync(
        int merchantId,
        int tradingPartnerId,
        IEnumerable<InventoryUpdateItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes a full inventory snapshot to Merchant360.
    /// Used for full-refresh inventory updates.
    /// </summary>
    Task<InventorySnapshotResult> PushInventorySnapshotAsync(
        int merchantId,
        InventorySnapshotRequest request,
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
    public string? ImageUrl3 { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }

    // New fields for enhanced content
    public string? Keywords { get; set; }
    public string? CountryOfOrigin { get; set; }
    public string? UnspscCode { get; set; }
    public string? ProductType { get; set; }
    public string? ProductLine { get; set; }
    public string? ProductSeries { get; set; }
    public decimal? RecycledPercent { get; set; }
    public decimal? RecycledPcwPercent { get; set; }
    public bool? AssemblyRequired { get; set; }
    public string? Description3 { get; set; }
    public string? ManufacturerWebsite { get; set; }
    public string? CategoryCode { get; set; }

    public List<ContentSpecification> Specifications { get; set; } = new();
    public List<ContentFeature> Features { get; set; } = new();
    public List<ContentRelatedProduct> RelatedProducts { get; set; } = new();
}

public class ContentSpecification
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Group { get; set; }
    public int DisplayOrder { get; set; }
}

public class ContentFeature
{
    public string? Headline { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

public class ContentRelatedProduct
{
    public string StockNumber { get; set; } = string.Empty;
    public string? RelationshipType { get; set; }
    public bool IsBidirectional { get; set; }
    public int DisplayOrder { get; set; }
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

#region Category Batch DTOs

/// <summary>
/// Request to push a batch of categories to M360.
/// Categories should be synced before products.
/// </summary>
public class CategoryBatchRequest
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public List<CategoryBatchItem> Categories { get; set; } = new();
}

public class CategoryBatchItem
{
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ParentCategoryCode { get; set; }
    public int Level { get; set; }
    public string? FullPath { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Response from M360 after pushing a category batch.
/// </summary>
public class CategoryBatchResponse
{
    public bool Success { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public int CategoriesReceived { get; set; }
    public int CategoriesCreated { get; set; }
    public int CategoriesUpdated { get; set; }
    public List<string>? Errors { get; set; }
}

#endregion

#region Order Status Update DTOs

/// <summary>
/// Request to push order status updates to M360.
/// </summary>
public class OrderStatusUpdateRequest
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string? SupplierOrderNumber { get; set; }
    public OrderStatusType StatusType { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public DateTime StatusDate { get; set; }
    public string? SourceDocumentType { get; set; }
    public int? SourceDocumentId { get; set; }
    public List<OrderLineStatusUpdate> LineUpdates { get; set; } = new();
}

public enum OrderStatusType
{
    Acknowledged = 0,
    Processing = 10,
    PartiallyShipped = 20,
    Shipped = 30,
    Delivered = 40,
    Invoiced = 50,
    Completed = 60,
    Cancelled = 70,
    Backordered = 80
}

public class OrderLineStatusUpdate
{
    public int LineNumber { get; set; }
    public string StockNumber { get; set; } = string.Empty;
    public int QuantityOrdered { get; set; }
    public int? QuantityAcknowledged { get; set; }
    public int? QuantityShipped { get; set; }
    public int? QuantityBackordered { get; set; }
    public int? QuantityCancelled { get; set; }
    public string? LineStatusCode { get; set; }
    public string? LineStatusMessage { get; set; }
    public DateTime? EstimatedShipDate { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
}

public class OrderStatusUpdateResult
{
    public bool Success { get; set; }
    public int MerchantId { get; set; }
    public string? PoNumber { get; set; }
    public string? NewStatus { get; set; }
    public int LinesUpdated { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion

#region Shipment Update DTOs

/// <summary>
/// Request to push shipment tracking information to M360.
/// </summary>
public class ShipmentUpdateRequest
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string? SupplierOrderNumber { get; set; }
    public string ShipmentId { get; set; } = string.Empty;
    public string? BillOfLadingNumber { get; set; }
    public string? CarrierCode { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public string? ShipMethod { get; set; }
    public DateTime? ShipDate { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
    public ShipmentAddress? ShipFrom { get; set; }
    public ShipmentAddress? ShipTo { get; set; }
    public decimal? TotalWeight { get; set; }
    public string? WeightUnit { get; set; }
    public int? PackageCount { get; set; }
    public List<ShipmentLineItem> Lines { get; set; } = new();
    public List<ShipmentCarton> Cartons { get; set; } = new();
}

public class ShipmentAddress
{
    public string? Name { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class ShipmentLineItem
{
    public int LineNumber { get; set; }
    public string StockNumber { get; set; } = string.Empty;
    public int QuantityShipped { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? SerialNumber { get; set; }
}

public class ShipmentCarton
{
    public string CartonId { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public decimal? Weight { get; set; }
    public List<ShipmentCartonItem> Items { get; set; } = new();
}

public class ShipmentCartonItem
{
    public string StockNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class ShipmentUpdateResult
{
    public bool Success { get; set; }
    public int MerchantId { get; set; }
    public string? PoNumber { get; set; }
    public string? ShipmentId { get; set; }
    public int LinesUpdated { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion

#region Invoice Update DTOs

/// <summary>
/// Request to push invoice information to M360.
/// </summary>
public class InvoiceUpdateRequest
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string? SupplierOrderNumber { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal SubTotal { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? ShippingAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Currency { get; set; }
    public List<InvoiceLineItem> Lines { get; set; } = new();
}

public class InvoiceLineItem
{
    public int LineNumber { get; set; }
    public string StockNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal ExtendedPrice { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
}

public class InvoiceUpdateResult
{
    public bool Success { get; set; }
    public int MerchantId { get; set; }
    public string? PoNumber { get; set; }
    public string? InvoiceNumber { get; set; }
    public int LinesProcessed { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion

#region Inventory DTOs

public record InventoryUpdateItem(
    string StockNumber,
    int QuantityAvailable,
    int? QuantityOnOrder,
    string? WarehouseCode,
    string? Status,
    DateTime? LastUpdated);

public class InventoryUpdateResult
{
    public bool Success { get; set; }
    public int MerchantId { get; set; }
    public int TradingPartnerId { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string>? Errors { get; set; }
}

/// <summary>
/// Request to push a full inventory snapshot to M360.
/// </summary>
public class InventorySnapshotRequest
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
    public DateTime InventoryDate { get; set; }
    public bool IsFullRefresh { get; set; } = true;
    public int SourceSnapshotId { get; set; }
    public List<InventorySnapshotItem> Items { get; set; } = new();
}

public class InventorySnapshotItem
{
    public string StockNumber { get; set; } = string.Empty;
    public string? Upc { get; set; }
    public int QuantityAvailable { get; set; }
    public int? QuantityOnOrder { get; set; }
    public int? QuantityOnHand { get; set; }
    public int? QuantityCommitted { get; set; }
    public string? Status { get; set; }
    public string? WarehouseCode { get; set; }
    public decimal? UnitCost { get; set; }
    public DateTime? NextAvailableDate { get; set; }
    public string? LeadTimeDays { get; set; }
}

public class InventorySnapshotResult
{
    public bool Success { get; set; }
    public int MerchantId { get; set; }
    public int TradingPartnerId { get; set; }
    public string? SnapshotId { get; set; }
    public int ItemsReceived { get; set; }
    public int ItemsUpdated { get; set; }
    public int ItemsCreated { get; set; }
    public int ItemsRemoved { get; set; }
    public int? SyncLogId { get; set; }
    public List<string>? Errors { get; set; }
}

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
