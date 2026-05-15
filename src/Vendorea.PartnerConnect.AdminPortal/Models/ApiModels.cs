namespace Vendorea.PartnerConnect.AdminPortal.Models;

// Dashboard Models
public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, ComponentHealth>? Components { get; set; }
}

public class ComponentHealth
{
    public string Status { get; set; } = string.Empty;
    public int ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DashboardStats
{
    public int TotalDealers { get; set; }
    public int ActiveConnections { get; set; }
    public int TotalDocuments { get; set; }
    public int PendingDocuments { get; set; }
    public int FailedDocuments { get; set; }
    public int QuarantinedDocuments { get; set; }
}

// Trading Partner Models
public class TradingPartnerDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
}

// Connection Models
public class ConnectionDto
{
    public int Id { get; set; }
    public int TradingPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSuccessfulSync { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Document Models
public class DocumentDto
{
    public int Id { get; set; }
    public int DealerPartnerConnectionId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ContentHash { get; set; }
    public string? StoragePath { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class DocumentPagedResult
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public List<DocumentDto> Results { get; set; } = new();
}

// Audit Log Models
public class AuditLogDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public int? DealerId { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }
    public DateTime Timestamp { get; set; }
    public bool HasChanges { get; set; }
}

public class AuditSearchResult
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public List<AuditLogDto> Results { get; set; } = new();
}

public class AuditStats
{
    public DateTime Since { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<string, int>? ByAction { get; set; }
    public Dictionary<string, int>? ByEntityType { get; set; }
    public double SuccessRate { get; set; }
}

// Billing Models
public class BillingPlanDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal? AnnualPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int IncludedDocuments { get; set; }
    public int IncludedApiCalls { get; set; }
    public int IncludedStorageGb { get; set; }
    public int MaxConnections { get; set; }
    public int MaxWebhooks { get; set; }
    public bool IsTrial { get; set; }
    public int? TrialDays { get; set; }
}

// Metering Models
public class UsageSummary
{
    public int DealerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Granularity { get; set; } = string.Empty;
    public List<MetricSummary> Summaries { get; set; } = new();
}

public class MetricSummary
{
    public string MetricType { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public double TotalValue { get; set; }
    public int RecordCount { get; set; }
    public string? Unit { get; set; }
}

// Webhook Models
public class WebhookSubscriptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<string> EventTypes { get; set; } = new();
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastDeliveryAt { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public int FailedDeliveries { get; set; }
}

// Price Feed Models
public class PriceFeedUploadResult
{
    public bool Success { get; set; }
    public int UploadId { get; set; }
    public int RecordCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsDuplicate { get; set; }
    public TimeSpan ParseDuration { get; set; }
    public string? CorrelationId { get; set; }
}

public class PriceFeedUploadDto
{
    public int Id { get; set; }
    public int DealerId { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? UploadedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? PushedToMerchant360At { get; set; }
    public long FileSizeBytes { get; set; }
}

public class PriceFeedUploadDetailDto
{
    public int Id { get; set; }
    public int DealerId { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? UploadedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? PushedToMerchant360At { get; set; }
    public string? CorrelationId { get; set; }
    public List<PriceRecordSummaryDto> SampleRecords { get; set; } = new();
}

public class PriceRecordSummaryDto
{
    public string StockNumber { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public decimal NetCost { get; set; }
    public decimal RetailListPrice { get; set; }
    public string? CategoryCode { get; set; }
}

public class PushToMerchant360Result
{
    public bool Success { get; set; }
    public int RecordsPushed { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? PushedAt { get; set; }
}

public class MerchantDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; }
}

// Enhanced Content Models (supplier-specific, shared across merchants)
public class ContentImportSummaryDto
{
    public int UploadId { get; set; }
    public int TradingPartnerId { get; set; }
    public string TradingPartnerName { get; set; } = string.Empty;
    public string ContentVersion { get; set; } = string.Empty;
    public string LocaleId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int ProcessedProducts { get; set; }
    public int ErrorProducts { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ContentImportResultDto
{
    public int UploadId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int ProcessedProducts { get; set; }
    public int ErrorProducts { get; set; }
    public string ContentVersion { get; set; } = string.Empty;
    public string LocaleId { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ContentImportProgressDto
{
    public string CurrentPhase { get; set; } = string.Empty;
    public double PercentComplete { get; set; }
    public int TotalProducts { get; set; }
    public int ProcessedProducts { get; set; }
    public int ProcessedFeatures { get; set; }
    public int ProcessedRelationships { get; set; }
}

public class ContentImportStatusDto
{
    public int UploadId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrentPhase { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int ProcessedProducts { get; set; }
    public int ErrorProducts { get; set; }
    public int TotalFeatures { get; set; }
    public int ProcessedFeatures { get; set; }
    public int TotalRelationships { get; set; }
    public int ProcessedRelationships { get; set; }
    public double PercentComplete { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorDetails { get; set; }
}

public class ContentStatisticsDto
{
    public int TotalProducts { get; set; }
    public int TotalBrands { get; set; }
    public int TotalCategories { get; set; }
    public int TotalFeatures { get; set; }
    public int TotalRelationships { get; set; }
    public DateTime? LastImportDate { get; set; }
    public string? LastContentVersion { get; set; }
}

// Merchant Subscription Models
public enum SubscriptionStatus
{
    Pending,
    Approved,
    Denied,
    Suspended
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
    public string Status { get; set; } = string.Empty;
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

public class SubscriptionListResult
{
    public int Total { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int DeniedCount { get; set; }
    public int SuspendedCount { get; set; }
    public List<MerchantSubscriptionDto> Items { get; set; } = new();
}

public class CreateSubscriptionRequest
{
    public int TenantId { get; set; }
    public int TradingPartnerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ApproveSubscriptionRequest
{
    public string? Notes { get; set; }
}

public class DenySubscriptionRequest
{
    public string DenialReason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class SuspendSubscriptionRequest
{
    public string? Notes { get; set; }
}
