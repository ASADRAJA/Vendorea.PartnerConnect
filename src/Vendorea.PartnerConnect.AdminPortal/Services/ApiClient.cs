using System.Net.Http.Json;
using System.Text.Json;
using Vendorea.PartnerConnect.AdminPortal.Models;

namespace Vendorea.PartnerConnect.AdminPortal.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Dashboard endpoints
    public async Task<HealthResponse?> GetHealthAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<HealthResponse>("/api/admin/dashboard/health");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health status");
            return null;
        }
    }

    public async Task<DashboardStats?> GetDashboardStatsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DashboardStats>("/api/admin/dashboard/stats");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard stats");
            return null;
        }
    }

    // Trading Partners endpoints
    public async Task<bool> UpdatePartnerConnectionRequirementsAsync(int id, List<string> requirements)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/v1/partners/{id}/connection-requirements", new { Requirements = requirements });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update connection requirements for partner {Id}", id);
            return false;
        }
    }

    public async Task<PartnerTransportDto?> GetPartnerTransportAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PartnerTransportDto>($"/api/v1/partners/{id}/transport");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transport config for partner {Id}", id);
            return null;
        }
    }

    public async Task<List<TradingPartnerDto>> GetTradingPartnersAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<TradingPartnerDto>>("/api/v1/partners") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trading partners");
            return new();
        }
    }

    // Partner distribution centers
    public async Task<List<DistributionCenterModel>> GetPartnerDistributionCentersAsync(int partnerId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<DistributionCenterModel>>(
                $"/api/v1/partners/{partnerId}/distribution-centers") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get distribution centers for partner {Id}", partnerId);
            return new();
        }
    }

    /// <summary>Creates or updates a DC. Returns (success, error message). Error surfaces conflicts/validation to the UI.</summary>
    public async Task<(bool Success, string? Error)> SavePartnerDistributionCenterAsync(int partnerId, DistributionCenterModel dc)
    {
        try
        {
            var response = dc.Id > 0
                ? await _httpClient.PutAsJsonAsync($"/api/v1/partners/{partnerId}/distribution-centers/{dc.Id}", dc)
                : await _httpClient.PostAsJsonAsync($"/api/v1/partners/{partnerId}/distribution-centers", dc);

            if (response.IsSuccessStatusCode)
                return (true, null);

            var body = await response.Content.ReadAsStringAsync();
            return (false, ExtractError(body) ?? $"Request failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save distribution center for partner {Id}", partnerId);
            return (false, ex.Message);
        }
    }

    public async Task<bool> DeletePartnerDistributionCenterAsync(int partnerId, int dcId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/partners/{partnerId}/distribution-centers/{dcId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete distribution center {DcId} for partner {Id}", dcId, partnerId);
            return false;
        }
    }

    public async Task<TradingPartnerDto?> GetTradingPartnerAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TradingPartnerDto>($"/api/v1/partners/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trading partner {Id}", id);
            return null;
        }
    }

    // Connections endpoints (using admin endpoint to get all connections regardless of dealer)
    public async Task<List<ConnectionDto>> GetConnectionsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ConnectionDto>>("/api/admin/dashboard/connections/all") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connections");
            return new();
        }
    }

    public async Task<bool> ActivateConnectionAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/partners/connections/{id}/activate", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate connection {Id}", id);
            return false;
        }
    }

    public async Task<bool> DeactivateConnectionAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/partners/connections/{id}/deactivate", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate connection {Id}", id);
            return false;
        }
    }

    // Documents endpoints
    public async Task<DocumentPagedResult> GetDocumentsAsync(
        int skip = 0,
        int take = 50,
        string? status = null,
        string? documentType = null,
        int? dealerId = null,
        int? tradingPartnerId = null)
    {
        try
        {
            var url = $"/api/admin/documents?skip={skip}&take={take}";
            if (!string.IsNullOrEmpty(status))
                url += $"&status={status}";
            if (!string.IsNullOrEmpty(documentType))
                url += $"&documentType={documentType}";
            if (dealerId.HasValue)
                url += $"&dealerId={dealerId}";
            if (tradingPartnerId.HasValue)
                url += $"&tradingPartnerId={tradingPartnerId}";

            return await _httpClient.GetFromJsonAsync<DocumentPagedResult>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get documents");
            return new();
        }
    }

    public async Task<List<DocumentDto>> GetPendingDocumentsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<DocumentDto>>("/api/admin/documents/pending") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending documents");
            return new();
        }
    }

    public async Task<DocumentDto?> GetDocumentAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DocumentDto>($"/api/admin/documents/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document {Id}", id);
            return null;
        }
    }

    public async Task<bool> RetryDocumentAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/documents/{id}/retry", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry document {Id}", id);
            return false;
        }
    }

    // Audit endpoints
    public async Task<AuditSearchResult> SearchAuditLogsAsync(int skip = 0, int take = 50, int? dealerId = null, string? action = null)
    {
        try
        {
            var url = $"/api/admin/audit/search?skip={skip}&take={take}";
            if (dealerId.HasValue)
                url += $"&dealerId={dealerId}";
            if (!string.IsNullOrEmpty(action))
                url += $"&action={action}";

            return await _httpClient.GetFromJsonAsync<AuditSearchResult>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search audit logs");
            return new();
        }
    }

    public async Task<AuditStats?> GetAuditStatsAsync(int hours = 24)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<AuditStats>($"/api/admin/audit/stats?hours={hours}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit stats");
            return null;
        }
    }

    // Billing endpoints
    public async Task<List<BillingPlanDto>> GetBillingPlansAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<BillingPlanDto>>("/api/billing/plans") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get billing plans");
            return new();
        }
    }

    // Metering endpoints
    public async Task<UsageSummary?> GetDealerUsageSummaryAsync(int dealerId, int days = 30)
    {
        try
        {
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-days);
            return await _httpClient.GetFromJsonAsync<UsageSummary>(
                $"/api/admin/metering/dealer/{dealerId}/summary?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dealer usage summary");
            return null;
        }
    }

    // Webhook endpoints
    public async Task<List<WebhookSubscriptionDto>> GetWebhookSubscriptionsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<WebhookSubscriptionDto>>("/api/v1/webhooks/subscriptions") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get webhook subscriptions");
            return new();
        }
    }

    // Price Feed endpoints
    public async Task<PriceFeedUploadResult?> UploadPriceFeedAsync(int dealerId, string tradingPartnerCode, Stream fileStream, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync(
                $"/api/v1/pricefeeds/upload?dealerId={dealerId}&tradingPartnerCode={tradingPartnerCode}",
                content);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PriceFeedUploadResult>();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Price feed upload failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            return new PriceFeedUploadResult { Success = false, ErrorMessage = errorContent };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload price feed");
            return new PriceFeedUploadResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<List<PriceFeedUploadDto>> GetPriceFeedHistoryAsync(int dealerId, string? tradingPartnerCode = null, int limit = 20)
    {
        try
        {
            var url = $"/api/v1/pricefeeds/history?dealerId={dealerId}&limit={limit}";
            if (!string.IsNullOrEmpty(tradingPartnerCode))
                url += $"&tradingPartnerCode={tradingPartnerCode}";

            return await _httpClient.GetFromJsonAsync<List<PriceFeedUploadDto>>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get price feed history");
            return new();
        }
    }

    public async Task<List<PriceFeedUploadDto>> GetAllPriceFeedUploadsAsync(int? dealerId = null, string? tradingPartnerCode = null, int limit = 100)
    {
        try
        {
            var url = $"/api/admin/pricefeeds?limit={limit}";
            if (dealerId.HasValue)
                url += $"&dealerId={dealerId}";
            if (!string.IsNullOrEmpty(tradingPartnerCode))
                url += $"&tradingPartnerCode={tradingPartnerCode}";

            return await _httpClient.GetFromJsonAsync<List<PriceFeedUploadDto>>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all price feed uploads");
            return new();
        }
    }

    public async Task<PriceFeedUploadDetailDto?> GetPriceFeedDetailsAsync(int uploadId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PriceFeedUploadDetailDto>($"/api/v1/pricefeeds/{uploadId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get price feed details for upload {UploadId}", uploadId);
            return null;
        }
    }

    /// <summary>Queues an async push. Returns (success, error); the outcome is tracked via upload status.</summary>
    public async Task<(bool Success, string? Error)> PushToMerchant360Async(int uploadId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/pricefeeds/{uploadId}/push-to-merchant360", null);
            if (response.IsSuccessStatusCode)
                return (true, null);
            return (false, ExtractError(await response.Content.ReadAsStringAsync()) ?? $"Request failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue push to Merchant360 for upload {UploadId}", uploadId);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> CancelPriceFeedUploadAsync(int uploadId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/pricefeeds/{uploadId}/cancel", null);
            if (response.IsSuccessStatusCode)
                return (true, null);
            return (false, ExtractError(await response.Content.ReadAsStringAsync()) ?? $"Request failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel price feed upload {UploadId}", uploadId);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> DeletePriceFeedUploadAsync(int uploadId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/pricefeeds/{uploadId}");
            if (response.IsSuccessStatusCode)
                return (true, null);
            return (false, ExtractError(await response.Content.ReadAsStringAsync()) ?? $"Request failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete price feed upload {UploadId}", uploadId);
            return (false, ex.Message);
        }
    }

    public async Task<List<MerchantDto>> GetMerchantsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<MerchantDto>>("/api/admin/dashboard/merchants") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get merchants");
            return new();
        }
    }

    public async Task<List<MerchantWithSubscriptionsDto>> GetMerchantsWithActiveSubscriptionsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<MerchantWithSubscriptionsDto>>("/api/admin/tenants/active-by-merchant") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get merchants with subscriptions");
            return new();
        }
    }

    // SPR Enhanced Content endpoints
    // Content is supplier-specific but shared across all merchants
    public async Task<ContentImportResultDto?> ImportContentAsync(
        int tradingPartnerId,
        string contentVersion,
        string locale,
        Stream fileStream,
        string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            content.Add(fileContent, "file", fileName);

            var url = $"/api/v1/admin/spr/content/imports?tradingPartnerId={tradingPartnerId}&contentVersion={Uri.EscapeDataString(contentVersion)}&locale={Uri.EscapeDataString(locale)}";
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ContentImportResultDto>();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Content import failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            return new ContentImportResultDto { Status = "Failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import content");
            return new ContentImportResultDto { Status = "Failed" };
        }
    }

    public async Task<List<ContentImportSummaryDto>> GetContentImportHistoryAsync(int limit = 20)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ContentImportSummaryDto>>(
                $"/api/v1/admin/spr/content/imports?limit={limit}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get content import history");
            return new();
        }
    }

    public async Task<ContentImportStatusDto?> GetContentImportStatusAsync(int uploadId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ContentImportStatusDto>(
                $"/api/v1/admin/spr/content/imports/{uploadId}/status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get content import status for upload {UploadId}", uploadId);
            return null;
        }
    }

    public async Task<ContentStatisticsDto?> GetContentStatisticsAsync(string locale = "EN_US")
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ContentStatisticsDto>(
                $"/api/v1/admin/spr/content/imports/stats?locale={Uri.EscapeDataString(locale)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get content statistics");
            return null;
        }
    }

    public async Task<bool> CancelContentImportAsync(int uploadId)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/v1/admin/spr/content/imports/{uploadId}/cancel", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel content import {UploadId}", uploadId);
            return false;
        }
    }

    public async Task<bool> DeleteContentImportAsync(int uploadId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"/api/v1/admin/spr/content/imports/{uploadId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete content import {UploadId}", uploadId);
            return false;
        }
    }

    public async Task<ContentPushResultDto?> PushContentToM360Async(int uploadId)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/v1/admin/spr/content/imports/{uploadId}/push", null);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ContentPushResultDto>();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Content push to M360 failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            return new ContentPushResultDto { Success = false, ErrorMessage = errorContent };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push content to M360 for upload {UploadId}", uploadId);
            return new ContentPushResultDto { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<M360PushProgressDto?> StartM360PushAsync(int uploadId)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/v1/admin/spr/content/imports/{uploadId}/push-start", null);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<M360PushProgressDto>();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("M360 push start failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            return new M360PushProgressDto { IsComplete = true, Success = false, ErrorMessage = errorContent };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start M360 push for upload {UploadId}", uploadId);
            return new M360PushProgressDto { IsComplete = true, Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<M360PushProgressDto?> GetM360PushProgressAsync(int uploadId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<M360PushProgressDto>(
                $"/api/v1/admin/spr/content/imports/{uploadId}/push-status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get M360 push progress for upload {UploadId}", uploadId);
            return null;
        }
    }

    // FTP Content Ingestion endpoints
    public async Task<FtpIngestionConfigDto?> GetFtpIngestionConfigAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FtpIngestionConfigDto>("/api/admin/ftp-ingestion/config");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get FTP ingestion config");
            return null;
        }
    }

    public async Task<bool> SaveFtpIngestionConfigAsync(FtpIngestionConfigDto config)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("/api/admin/ftp-ingestion/config", config);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save FTP ingestion config");
            return false;
        }
    }

    public async Task<FtpConnectionTestResult> TestFtpConnectionAsync(FtpIngestionConfigDto config)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/ftp-ingestion/test-connection", config);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<FtpConnectionTestResult>()
                    ?? new FtpConnectionTestResult { Success = false, ErrorMessage = "Invalid response" };
            }
            var error = await response.Content.ReadAsStringAsync();
            return new FtpConnectionTestResult { Success = false, ErrorMessage = error };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test FTP connection");
            return new FtpConnectionTestResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<FtpIngestionStatusDto?> GetFtpIngestionStatusAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<FtpIngestionStatusDto>("/api/admin/ftp-ingestion/status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get FTP ingestion status");
            return null;
        }
    }

    public async Task<FtpIngestionRunDto?> RunFtpIngestionAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/admin/ftp-ingestion/run", null);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<FtpIngestionRunDto>();
            }
            var error = await response.Content.ReadAsStringAsync();
            return new FtpIngestionRunDto { Success = false, Errors = new List<string> { error } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run FTP ingestion");
            return new FtpIngestionRunDto { Success = false, Errors = new List<string> { ex.Message } };
        }
    }

    public async Task<List<FtpIngestionRunDto>> GetFtpIngestionHistoryAsync(int limit = 20)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<FtpIngestionRunDto>>(
                $"/api/admin/ftp-ingestion/history?limit={limit}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get FTP ingestion history");
            return new();
        }
    }

    // Organization endpoints
    public async Task<OrganizationListResult> GetOrganizationsAsync(string? status = null)
    {
        try
        {
            var url = "/api/admin/organizations";
            if (!string.IsNullOrEmpty(status))
                url += $"?status={status}";

            return await _httpClient.GetFromJsonAsync<OrganizationListResult>(url) ?? new OrganizationListResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get organizations");
            return new OrganizationListResult();
        }
    }

    public async Task<OrganizationDto?> GetOrganizationAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<OrganizationDto>($"/api/admin/organizations/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get organization {Id}", id);
            return null;
        }
    }

    public async Task<OrganizationDto?> CreateOrganizationAsync(CreateOrganizationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/organizations", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<OrganizationDto>();
            }
            _logger.LogWarning("Failed to create organization: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create organization");
            return null;
        }
    }

    public async Task<bool> UpdateOrganizationAsync(int id, UpdateOrganizationRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/admin/organizations/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update organization {Id}", id);
            return false;
        }
    }

    public async Task<bool> ActivateOrganizationAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/organizations/{id}/activate", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate organization {Id}", id);
            return false;
        }
    }

    public async Task<bool> SuspendOrganizationAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/organizations/{id}/suspend", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suspend organization {Id}", id);
            return false;
        }
    }

    public async Task<bool> ApproveOrganizationAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/organizations/{id}/approve", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve organization {Id}", id);
            return false;
        }
    }

    public async Task<bool> RejectOrganizationAsync(int id, string? reason)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/admin/organizations/{id}/reject", new { Reason = reason });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject organization {Id}", id);
            return false;
        }
    }

    /// <summary>
    /// Operator-led onboarding: creates an organization and stands it up in one step (activate +
    /// subscribe + invite first admin). Returns (result, error) — error surfaces validation/conflicts.
    /// </summary>
    public async Task<(OnboardOrganizationResult? Result, string? Error)> OnboardOrganizationAsync(OnboardOrganizationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/admin/organizations/onboard", request);
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<OnboardOrganizationResult>(), null);

            var body = await response.Content.ReadAsStringAsync();
            return (null, ExtractError(body) ?? $"Request failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to onboard organization");
            return (null, ex.Message);
        }
    }

    // --- Self-service org registration queue ---

    public async Task<OrgRegistrationListResult> GetOrgRegistrationsAsync(string? status = "Pending")
    {
        try
        {
            var url = "/api/v1/admin/org-registrations";
            if (!string.IsNullOrEmpty(status))
                url += $"?status={status}";
            return await _httpClient.GetFromJsonAsync<OrgRegistrationListResult>(url) ?? new OrgRegistrationListResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get org registrations");
            return new OrgRegistrationListResult();
        }
    }

    public async Task<(bool Success, string? Message)> ApproveOrgRegistrationAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/admin/org-registrations/{id}/approve", null);
            var body = await response.Content.ReadAsStringAsync();
            return (response.IsSuccessStatusCode, ExtractError(body));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve org registration {Id}", id);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Message)> DenyOrgRegistrationAsync(int id, string? reason)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/v1/admin/org-registrations/{id}/deny", new { Reason = reason });
            var body = await response.Content.ReadAsStringAsync();
            return (response.IsSuccessStatusCode, ExtractError(body));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deny org registration {Id}", id);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Message)> ResendOrgRegistrationInviteAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/admin/org-registrations/{id}/resend-invite", null);
            var body = await response.Content.ReadAsStringAsync();
            return (response.IsSuccessStatusCode, ExtractError(body));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend org registration invite {Id}", id);
            return (false, ex.Message);
        }
    }

    // --- Tenant-partner connections ---

    public async Task<TenantConnectionListResult> GetConnectionsAsync(int? organizationId = null, string? status = null)
    {
        try
        {
            var query = new List<string>();
            if (organizationId.HasValue) query.Add($"organizationId={organizationId}");
            if (!string.IsNullOrEmpty(status)) query.Add($"status={status}");
            var url = "/api/admin/connections" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
            return await _httpClient.GetFromJsonAsync<TenantConnectionListResult>(url) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connections");
            return new();
        }
    }

    public async Task<List<ConnectionPartnerOption>> GetConnectionPartnerOptionsAsync(int organizationId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ConnectionPartnerOption>>(
                $"/api/admin/connections/options?organizationId={organizationId}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection partner options for org {OrgId}", organizationId);
            return new();
        }
    }

    public async Task<List<string>> GetPartnerConfirmationFieldsAsync(int tradingPartnerId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<string>>(
                $"/api/admin/connections/partner-fields?tradingPartnerId={tradingPartnerId}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get partner confirmation fields for {PartnerId}", tradingPartnerId);
            return new();
        }
    }

    public async Task<bool> CreateConnectionAsync(CreateTenantConnectionRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/connections", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create connection");
            return false;
        }
    }

    public async Task<bool> ApproveConnectionAsync(int id, string? reason)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/admin/connections/{id}/approve", new { Reason = reason });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve connection {Id}", id);
            return false;
        }
    }

    public async Task<bool> DenyConnectionAsync(int id, string? reason)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/admin/connections/{id}/deny", new { Reason = reason });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deny connection {Id}", id);
            return false;
        }
    }

    // Tenant endpoints
    public async Task<TenantListResult> GetTenantsAsync(int? organizationId = null, string? status = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (organizationId.HasValue)
                queryParams.Add($"organizationId={organizationId}");
            if (!string.IsNullOrEmpty(status))
                queryParams.Add($"status={status}");

            var url = "/api/admin/tenants";
            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);

            return await _httpClient.GetFromJsonAsync<TenantListResult>(url) ?? new TenantListResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tenants");
            return new TenantListResult();
        }
    }

    public async Task<TenantDto?> GetTenantAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TenantDto>($"/api/admin/tenants/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tenant {Id}", id);
            return null;
        }
    }

    public async Task<TenantDto?> CreateTenantAsync(CreateTenantRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/tenants", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TenantDto>();
            }
            _logger.LogWarning("Failed to create tenant: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tenant");
            return null;
        }
    }

    public async Task<bool> UpdateTenantAsync(int id, UpdateTenantRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/admin/tenants/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tenant {Id}", id);
            return false;
        }
    }

    public async Task<bool> ActivateTenantAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/tenants/{id}/activate", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate tenant {Id}", id);
            return false;
        }
    }

    public async Task<bool> SuspendTenantAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/tenants/{id}/suspend", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suspend tenant {Id}", id);
            return false;
        }
    }

    // Tenant Partner Account endpoints
    public async Task<List<TenantPartnerAccountDto>> GetTenantPartnerAccountsAsync(int tenantId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<TenantPartnerAccountDto>>(
                $"/api/admin/tenants/{tenantId}/accounts") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tenant partner accounts for tenant {TenantId}", tenantId);
            return new();
        }
    }

    public async Task<TenantPartnerAccountDto?> CreateTenantPartnerAccountAsync(CreateTenantPartnerAccountRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/admin/tenants/{request.TenantId}/accounts", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TenantPartnerAccountDto>();
            }
            _logger.LogWarning("Failed to create tenant partner account: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tenant partner account");
            return null;
        }
    }

    public async Task<bool> DeactivateTenantPartnerAccountAsync(int accountId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/accounts/{accountId}/deactivate", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate tenant partner account {AccountId}", accountId);
            return false;
        }
    }

    // Order endpoints
    public async Task<OrderListResult> GetOrdersAsync(
        int? organizationId = null,
        int? tenantId = null,
        string? status = null,
        int skip = 0,
        int take = 50)
    {
        try
        {
            var queryParams = new List<string> { $"skip={skip}", $"take={take}" };
            if (organizationId.HasValue)
                queryParams.Add($"organizationId={organizationId}");
            if (tenantId.HasValue)
                queryParams.Add($"tenantId={tenantId}");
            if (!string.IsNullOrEmpty(status))
                queryParams.Add($"status={status}");

            var url = "/api/admin/orders?" + string.Join("&", queryParams);

            return await _httpClient.GetFromJsonAsync<OrderListResult>(url) ?? new OrderListResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get orders");
            return new OrderListResult();
        }
    }

    public async Task<OrderDetailDto?> GetOrderAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<OrderDetailDto>($"/api/admin/orders/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order {Id}", id);
            return null;
        }
    }

    public async Task<bool> CancelOrderAsync(int id, string? reason = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/admin/orders/{id}/cancel",
                new { reason });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order {Id}", id);
            return false;
        }
    }

    public async Task<bool> AcknowledgeOrderAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/orders/{id}/acknowledge", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge order {Id}", id);
            return false;
        }
    }

    /// <summary>Transmits an order to SPR (generate EZPO4 → XSD validate → SFTP send). Temporary manual dispatch.</summary>
    public async Task<(bool Success, string? Message)> TransmitOrderAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/orders/{id}/transmit", null);
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return (true, "Transmitted to SPR — order moved to Processing.");
            return (false, ExtractError(body) ?? $"Transmit failed (HTTP {(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transmit order {Id}", id);
            return (false, ex.Message);
        }
    }

    // Scheduled / Cron Jobs endpoints
    public async Task<List<ScheduledJobDto>> GetScheduledJobsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ScheduledJobDto>>("/api/admin/scheduled-jobs") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get scheduled jobs");
            return new();
        }
    }

    public async Task<List<ScheduledJobRunDto>> GetScheduledJobRunsAsync(int id, int take = 20)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ScheduledJobRunDto>>($"/api/admin/scheduled-jobs/{id}/runs?take={take}") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get runs for scheduled job {Id}", id);
            return new();
        }
    }

    public async Task<(bool Success, string? Error)> UpdateScheduledJobAsync(int id, UpdateScheduledJobRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/admin/scheduled-jobs/{id}", request);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update scheduled job {Id}", id);
            return (false, ex.Message);
        }
    }

    public async Task<CronPreviewResponse?> PreviewCronAsync(CronPreviewRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/scheduled-jobs/preview-cron", request);
            return await response.Content.ReadFromJsonAsync<CronPreviewResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview cron");
            return null;
        }
    }

    public async Task<RunJobResult?> RunScheduledJobAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/admin/scheduled-jobs/{id}/run", null);
            return await response.Content.ReadFromJsonAsync<RunJobResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run scheduled job {Id}", id);
            return new RunJobResult { Success = false, Error = ex.Message };
        }
    }

    // SPR inbound simulation endpoints
    public async Task<SprInjectResult?> InjectSprInboundAsync(int connectionId, string documentType, string xml)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/spr/inbound", new
            {
                connectionId,
                documentType,
                xml
            });

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SprInjectResult>();
            }

            var error = await response.Content.ReadAsStringAsync();
            return new SprInjectResult { Success = false, ErrorMessage = $"{(int)response.StatusCode}: {error}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject SPR inbound document");
            return new SprInjectResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SprCallbacksResult?> GetSprCallbacksAsync(string correlationId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<SprCallbacksResult>(
                $"/api/admin/spr/inbound/callbacks?correlationId={Uri.EscapeDataString(correlationId)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SPR callbacks for {CorrelationId}", correlationId);
            return null;
        }
    }

    // ---- Admin Portal users (login + management) ----

    /// <summary>Validates a username/password against the API. Returns the user on success, else null.</summary>
    public async Task<PortalUserDto?> AuthenticatePortalUserAsync(string username, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/admin/portal-users/authenticate", new { Username = username, Password = password });
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<PortalUserDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate portal user {Username}", username);
            return null;
        }
    }

    public async Task<List<PortalUserDto>> GetPortalUsersAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<PortalUserDto>>("/api/admin/portal-users")
                   ?? new List<PortalUserDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get portal users");
            return new List<PortalUserDto>();
        }
    }

    public async Task<(bool Success, string? Error)> CreatePortalUserAsync(CreatePortalUserRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/portal-users", request);
            return await ReadResultAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create portal user {Username}", request.Username);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> UpdatePortalUserAsync(Guid id, UpdatePortalUserRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/admin/portal-users/{id}", request);
            return await ReadResultAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update portal user {Id}", id);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> ResetPortalUserPasswordAsync(Guid id, string newPassword)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/admin/portal-users/{id}/password", new { NewPassword = newPassword });
            return await ReadResultAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset password for portal user {Id}", id);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> DeletePortalUserAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/admin/portal-users/{id}");
            return await ReadResultAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete portal user {Id}", id);
            return (false, ex.Message);
        }
    }

    private static async Task<(bool Success, string? Error)> ReadResultAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return (true, null);
        var body = await response.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
    }

    /// <summary>Pulls the "error"/"message" field out of a JSON error body; falls back to the raw text.</summary>
    private static string? ExtractError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { "error", "message" })
                {
                    if (doc.RootElement.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                        return prop.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // not JSON — return the raw body below
        }
        return body;
    }
}
