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
    public int DealerId { get; set; }
    public string? DealerName { get; set; }
    public int TradingPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public bool IsActive { get; set; }
    public string? Status { get; set; }
    public DateTime? LastSuccessfulSync { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Document Models
public class DocumentDto
{
    public int Id { get; set; }
    public int DealerPartnerConnectionId { get; set; }
    public int DealerId { get; set; }
    public string? DealerName { get; set; }
    public int TradingPartnerId { get; set; }
    public string? PartnerName { get; set; }
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
    public DocumentFilterOptions? FilterOptions { get; set; }
}

public class DocumentFilterOptions
{
    public List<string> DocumentTypes { get; set; } = new();
    public List<FilterOption> Dealers { get; set; } = new();
    public List<FilterOption> Partners { get; set; } = new();
}

public class FilterOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
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
    public string? DealerName { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public string? TradingPartnerName { get; set; }
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
    public DateTime? PushedToM360At { get; set; }
}

public class ContentPushResultDto
{
    public bool Success { get; set; }
    public int UploadId { get; set; }
    public int RecordsPushed { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsSkipped { get; set; }
    public int BatchCount { get; set; }
    public DateTime? PushedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class M360PushProgressDto
{
    public int UploadId { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string PhaseDescription { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public bool Success { get; set; }

    // Category progress
    public int TotalCategories { get; set; }
    public int CategoriesPushed { get; set; }

    // Product progress
    public int TotalProducts { get; set; }
    public int ProductsPushed { get; set; }
    public int CurrentBatch { get; set; }
    public int TotalBatches { get; set; }

    // Results
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsSkipped { get; set; }

    public double PercentComplete { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();
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

public class UnsubscribeRequest
{
    public string? Notes { get; set; }
}

// Subscription-based filtering for price feeds and content
public class MerchantWithSubscriptionsDto
{
    public int MerchantId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public string MerchantCode { get; set; } = string.Empty;
    public List<SubscribedPartnerDto> Partners { get; set; } = new();
}

public class SubscribedPartnerDto
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; } = string.Empty;
    public string TradingPartnerName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}

// FTP Content Ingestion Models
public class FtpIngestionConfigDto
{
    public string FtpHost { get; set; } = "ftp.etilize.com";
    public int FtpPort { get; set; } = 21;
    public string FtpUsername { get; set; } = string.Empty;
    public string FtpPassword { get; set; } = string.Empty;
    public string LocalDownloadPath { get; set; } = @"C:\inquire";
    public string Locale { get; set; } = "EN_US";
    public string DatabaseType { get; set; } = "mssql";
    public bool Enabled { get; set; }
    public bool EnableScheduledRun { get; set; } = true;
    public int ScheduledRunHourUtc { get; set; } = 2;
    public int CheckIntervalMinutes { get; set; } = 60;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public int BulkInsertBatchSize { get; set; } = 10000;
    public bool CleanupAfterImport { get; set; } = true;
}

public class FtpIngestionStatusDto
{
    public bool IsRunning { get; set; }
    public DateTime? LastRunAt { get; set; }
    public bool? LastRunSuccess { get; set; }
    public DateTime? NextScheduledRun { get; set; }
    public string? CurrentPhase { get; set; }
}

public class FtpIngestionRunDto
{
    public int Id { get; set; }
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Duration { get; set; } = string.Empty;
    public int ProductsTransformed { get; set; }
    public int CategoriesTransformed { get; set; }
    public int FeaturesTransformed { get; set; }
    public int RelationshipsTransformed { get; set; }
    public int SpecificationsTransformed { get; set; }
    public int FilesDownloaded { get; set; }
    public long BytesDownloaded { get; set; }
    public int TablesImported { get; set; }
    public long RowsImported { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class FtpConnectionTestResult
{
    public bool Success { get; set; }
    public int FilesFound { get; set; }
    public string? ErrorMessage { get; set; }
}

// Organization Models (Multi-Tenant)
public class OrganizationDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? BillingPlanId { get; set; }
    public string? BillingPlanName { get; set; }
    public string PaymentTerms { get; set; } = "CreditCard";
    public bool IsMultiTenant { get; set; }
    public bool ExternalPortalEnabled { get; set; }
    public string? PortalBaseUrl { get; set; }
    public bool HasPortalApiKey { get; set; }
    public int TenantCount { get; set; }
    public List<int> TradingPartnerIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class OrganizationListResult
{
    public int Total { get; set; }
    public int ActiveCount { get; set; }
    public int SuspendedCount { get; set; }
    public int PendingCount { get; set; }
    public List<OrganizationDto> Items { get; set; } = new();
}

public class CreateOrganizationRequest
{
    // Code is system-generated server-side; kept for compatibility, not sent by the form.
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public Guid? BillingPlanId { get; set; }
    public string PaymentTerms { get; set; } = "CreditCard";
    public bool IsMultiTenant { get; set; }
    public bool ExternalPortalEnabled { get; set; }
    public string? PortalBaseUrl { get; set; }
    public string? PortalApiKey { get; set; }
    public List<int> TradingPartnerIds { get; set; } = new();
}

public class UpdateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public Guid? BillingPlanId { get; set; }
    public string PaymentTerms { get; set; } = "CreditCard";
    public bool ExternalPortalEnabled { get; set; }
    public string? PortalBaseUrl { get; set; }
    public string? PortalApiKey { get; set; }
    public List<int> TradingPartnerIds { get; set; } = new();
}

// Tenant-partner connection models
public class TenantConnectionDto
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public int TradingPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string ExternalTenantId { get; set; } = string.Empty;
    public int? TenantId { get; set; }
    public string? TenantName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? ContactFirstName { get; set; }
    public string? ContactLastName { get; set; }
    public string? SpecialIdentifyingCode { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, string> ConfirmationFields { get; set; } = new();
    public string ApprovalStatus { get; set; } = string.Empty;
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class TenantConnectionListResult
{
    public int Total { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int DeniedCount { get; set; }
    public List<TenantConnectionDto> Items { get; set; } = new();
}

public class ConnectionPartnerOption
{
    public int TradingPartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class CreateTenantConnectionRequest
{
    public int OrganizationId { get; set; }
    public int TradingPartnerId { get; set; }
    public string ExternalTenantId { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? ContactFirstName { get; set; }
    public string? ContactLastName { get; set; }
    public string? SpecialIdentifyingCode { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, string> ConfirmationFields { get; set; } = new();
}

// Tenant Models (Multi-Tenant)
public class TenantDto
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int PartnerAccountCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TenantListResult
{
    public int Total { get; set; }
    public int ActiveCount { get; set; }
    public int SuspendedCount { get; set; }
    public List<TenantDto> Items { get; set; } = new();
}

public class CreateTenantRequest
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
}

// Tenant Partner Account Models
public class TenantPartnerAccountDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string? TenantName { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public string? TradingPartnerName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateTenantPartnerAccountRequest
{
    public int TenantId { get; set; }
    public int TradingPartnerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
}

public class TenantSyncResultDto
{
    public bool Success { get; set; }
    public int OrganizationId { get; set; }
    public string OrganizationCode { get; set; } = string.Empty;
    public int TotalMerchants { get; set; }
    public int TenantsCreated { get; set; }
    public int TenantsUpdated { get; set; }
    public int TenantsDeactivated { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public DateTime SyncedAt { get; set; }
    public int DurationMs { get; set; }
}

// Order Models
public class OrderDto
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public int TenantId { get; set; }
    public string? TenantName { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int LineCount { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

public class OrderDetailDto : OrderDto
{
    public DateTime? RequestedShipDate { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public string? ShippingMethod { get; set; }
    public string? Notes { get; set; }
    public AddressDto? ShipTo { get; set; }
    public AddressDto? BillTo { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();
    public List<OrderStatusHistoryDto> StatusHistory { get; set; } = new();
}

public class OrderLineDto
{
    public int Id { get; set; }
    public int LineNumber { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? VendorSku { get; set; }
    public string? Description { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityShipped { get; set; }
    public int QuantityCancelled { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class OrderStatusHistoryDto
{
    public int Id { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string? ChangedByUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class AddressDto
{
    public string? Name { get; set; }
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class OrderListResult
{
    public int Total { get; set; }
    public int DraftCount { get; set; }
    public int SubmittedCount { get; set; }
    public int ProcessingCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public List<OrderDto> Items { get; set; } = new();
}
