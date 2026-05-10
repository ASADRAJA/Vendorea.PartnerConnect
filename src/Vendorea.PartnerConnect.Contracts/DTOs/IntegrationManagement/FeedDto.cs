using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;

/// <summary>
/// DTO for price feed batch information.
/// </summary>
public record PriceFeedBatchDto(
    int Id,
    int PartnerDocumentId,
    int DealerId,
    int TradingPartnerId,
    string? TradingPartnerCode,
    FeedBatchStatus Status,
    int TotalItems,
    int ProcessedItems,
    int MatchedItems,
    int UpdatedItems,
    int SkippedItems,
    int ErrorItems,
    DateTime ReceivedAt,
    DateTime? ProcessingStartedAt,
    DateTime? ProcessingCompletedAt,
    string? ErrorSummary);

/// <summary>
/// DTO for inventory feed batch information.
/// </summary>
public record InventoryFeedBatchDto(
    int Id,
    int PartnerDocumentId,
    int DealerId,
    int TradingPartnerId,
    string? TradingPartnerCode,
    FeedBatchStatus Status,
    int TotalItems,
    int ProcessedItems,
    int MatchedItems,
    int UpdatedItems,
    int SkippedItems,
    int ErrorItems,
    DateTime ReceivedAt,
    DateTime? ProcessingStartedAt,
    DateTime? ProcessingCompletedAt,
    string? ErrorSummary);

/// <summary>
/// DTO for content sync job information.
/// </summary>
public record ContentSyncJobDto(
    int Id,
    int DealerId,
    int TradingPartnerId,
    string? TradingPartnerCode,
    ContentSyncType SyncType,
    ContentSyncStatus Status,
    int TotalProducts,
    int ProcessedProducts,
    int UpdatedProducts,
    int NewImagesDownloaded,
    int SkippedProducts,
    int ErrorProducts,
    DateTime ScheduledAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorDetails,
    string? TriggerSource);

/// <summary>
/// Command to trigger a manual feed sync.
/// </summary>
public record TriggerFeedSyncCommand(
    FeedType FeedType);

/// <summary>
/// Command to schedule a content sync.
/// </summary>
public record ScheduleContentSyncCommand(
    ContentSyncType SyncType,
    DateTime? ScheduleAt = null);

/// <summary>
/// Response for a triggered sync operation.
/// </summary>
public record SyncTriggerResponse(
    bool Accepted,
    string? Message,
    int? BatchId);

/// <summary>
/// Feed type enumeration.
/// </summary>
public enum FeedType
{
    Price,
    Inventory
}

/// <summary>
/// DTO for feed statistics.
/// </summary>
public record FeedStatisticsDto(
    int TotalBatches,
    int TotalItemsProcessed,
    int TotalItemsUpdated,
    int TotalErrors,
    int CompletedBatches,
    int FailedBatches,
    DateTime? LastSyncAt,
    decimal SuccessRate);
