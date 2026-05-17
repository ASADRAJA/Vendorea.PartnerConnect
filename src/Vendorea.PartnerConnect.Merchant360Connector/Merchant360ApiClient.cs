using System.ComponentModel;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Contracts.Interfaces;

namespace Vendorea.PartnerConnect.Merchant360Connector;

/// <summary>
/// HTTP client for communicating with the Merchant360 API.
/// Used to push processed partner data (prices, content) to merchants.
/// </summary>
public class Merchant360ApiClient : IMerchant360Client
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<Merchant360ApiClient> _logger;

    private const int MaxBatchSize = 10000;

    public Merchant360ApiClient(
        HttpClient httpClient,
        ILogger<Merchant360ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Merchant360 API");
            return false;
        }
    }

    /// <summary>
    /// Gets active merchants from Merchant360.
    /// Used by PC admin UI to select which merchant receives a price upload.
    /// </summary>
    public async Task<IReadOnlyList<Merchant360Merchant>> GetMerchantsAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v1/partner-connect/merchants?activeOnly={activeOnly.ToString().ToLower()}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var merchants = await response.Content.ReadFromJsonAsync<List<Merchant360Merchant>>(cancellationToken);
                return merchants ?? new List<Merchant360Merchant>();
            }

            _logger.LogWarning("Failed to get merchants: {StatusCode}", response.StatusCode);
            return new List<Merchant360Merchant>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching merchants from Merchant360");
            return new List<Merchant360Merchant>();
        }
    }

    /// <summary>
    /// Gets trading partners from Merchant360.
    /// Used by PC to map its supplier/partner records to M360 trading partner IDs.
    /// </summary>
    public async Task<IReadOnlyList<Merchant360TradingPartner>> GetTradingPartnersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/partner-connect/trading-partners", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var partners = await response.Content.ReadFromJsonAsync<List<Merchant360TradingPartner>>(cancellationToken);
                return partners ?? new List<Merchant360TradingPartner>();
            }

            _logger.LogWarning("Failed to get trading partners: {StatusCode}", response.StatusCode);
            return new List<Merchant360TradingPartner>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching trading partners from Merchant360");
            return new List<Merchant360TradingPartner>();
        }
    }

    /// <summary>
    /// Pushes a batch of prices to Merchant360 for a specific merchant.
    /// Price data is per-merchant. Max batch size is 10,000 items.
    /// </summary>
    public async Task<PriceBatchResponse> PushPriceBatchAsync(
        int merchantId,
        PriceBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Pushing {Count} prices for merchant {MerchantId} from partner {PartnerCode} (SourceUploadId: {SourceUploadId})",
            request.Items.Count, merchantId, request.TradingPartnerCode, request.SourceUploadId);

        // Validate batch size
        if (request.Items.Count > MaxBatchSize)
        {
            _logger.LogError("Batch size {Count} exceeds maximum {Max}", request.Items.Count, MaxBatchSize);
            return new PriceBatchResponse
            {
                Success = false,
                MerchantId = merchantId,
                TradingPartnerId = request.TradingPartnerId,
                TradingPartnerCode = request.TradingPartnerCode,
                RecordsReceived = request.Items.Count,
                Errors = new List<string> { $"Batch size {request.Items.Count} exceeds maximum {MaxBatchSize}" }
            };
        }

        // Check for duplicate stock numbers
        var duplicates = request.Items
            .GroupBy(i => i.StockNumber)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            _logger.LogError("Duplicate stock numbers found: {Duplicates}", string.Join(", ", duplicates.Take(10)));
            return new PriceBatchResponse
            {
                Success = false,
                MerchantId = merchantId,
                TradingPartnerId = request.TradingPartnerId,
                TradingPartnerCode = request.TradingPartnerCode,
                RecordsReceived = request.Items.Count,
                Errors = new List<string> { $"Duplicate stock numbers found: {string.Join(", ", duplicates.Take(10))}" }
            };
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/partner-connect/merchants/{merchantId}/prices/batch",
                request,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PriceBatchResponse>(cancellationToken);
                if (result != null)
                {
                    _logger.LogInformation(
                        "Price batch success for merchant {MerchantId}: Received={Received}, Created={Created}, Updated={Updated}, Skipped={Skipped}, SyncLogId={SyncLogId}",
                        merchantId, result.RecordsReceived, result.RecordsCreated, result.RecordsUpdated, result.RecordsSkipped, result.SyncLogId);
                    return result;
                }
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Price batch failed for merchant {MerchantId}: {StatusCode} - {Error}",
                merchantId, response.StatusCode, errorContent);

            return new PriceBatchResponse
            {
                Success = false,
                MerchantId = merchantId,
                TradingPartnerId = request.TradingPartnerId,
                TradingPartnerCode = request.TradingPartnerCode,
                RecordsReceived = request.Items.Count,
                Errors = new List<string> { $"API returned {response.StatusCode}: {errorContent}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while pushing prices for merchant {MerchantId}", merchantId);
            return new PriceBatchResponse
            {
                Success = false,
                MerchantId = merchantId,
                TradingPartnerId = request.TradingPartnerId,
                TradingPartnerCode = request.TradingPartnerCode,
                RecordsReceived = request.Items.Count,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Pushes a batch of product content to Merchant360.
    /// Content is shared across all merchants. Max batch size is 10,000 items.
    /// </summary>
    public async Task<ContentBatchResponse> PushContentBatchAsync(
        ContentBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Pushing {Count} products for partner {PartnerCode}, version {Version}, locale {Locale} (SourceUploadId: {SourceUploadId})",
            request.Products.Count, request.TradingPartnerCode, request.ContentVersion, request.Locale, request.SourceUploadId);

        // Validate batch size
        if (request.Products.Count > MaxBatchSize)
        {
            _logger.LogError("Batch size {Count} exceeds maximum {Max}", request.Products.Count, MaxBatchSize);
            return new ContentBatchResponse
            {
                Success = false,
                TradingPartnerId = request.TradingPartnerId,
                TradingPartnerCode = request.TradingPartnerCode,
                ContentVersion = request.ContentVersion,
                Locale = request.Locale,
                RecordsReceived = request.Products.Count,
                Errors = new List<string> { $"Batch size {request.Products.Count} exceeds maximum {MaxBatchSize}" }
            };
        }

        // Check for duplicate stock numbers
        var duplicates = request.Products
            .GroupBy(p => p.StockNumber)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            _logger.LogError("Duplicate stock numbers found: {Duplicates}", string.Join(", ", duplicates.Take(10)));
            return new ContentBatchResponse
            {
                Success = false,
                TradingPartnerId = request.TradingPartnerId,
                TradingPartnerCode = request.TradingPartnerCode,
                ContentVersion = request.ContentVersion,
                Locale = request.Locale,
                RecordsReceived = request.Products.Count,
                Errors = new List<string> { $"Duplicate stock numbers found: {string.Join(", ", duplicates.Take(10))}" }
            };
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/partner-connect/content/products/batch",
                request,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ContentBatchResponse>(cancellationToken);
                if (result != null)
                {
                    _logger.LogInformation(
                        "Content batch success for partner {PartnerCode}: Received={Received}, Created={Created}, Updated={Updated}, Specs={Specs}, Features={Features}, Relationships={Relationships}, SyncLogId={SyncLogId}",
                        request.TradingPartnerCode, result.RecordsReceived, result.RecordsCreated, result.RecordsUpdated,
                        result.SpecificationsProcessed, result.FeaturesProcessed, result.RelationshipsProcessed, result.SyncLogId);
                    return result;
                }
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Content batch failed for partner {PartnerCode}: {StatusCode} - {Error}",
                request.TradingPartnerCode, response.StatusCode, errorContent);

            return new ContentBatchResponse
            {
                Success = false,
                TradingPartnerId = request.TradingPartnerId,
                TradingPartnerCode = request.TradingPartnerCode,
                ContentVersion = request.ContentVersion,
                Locale = request.Locale,
                RecordsReceived = request.Products.Count,
                Errors = new List<string> { $"API returned {response.StatusCode}: {errorContent}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while pushing content for partner {PartnerCode}", request.TradingPartnerCode);
            return new ContentBatchResponse
            {
                Success = false,
                TradingPartnerId = request.TradingPartnerId,
                TradingPartnerCode = request.TradingPartnerCode,
                ContentVersion = request.ContentVersion,
                Locale = request.Locale,
                RecordsReceived = request.Products.Count,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    #region Phase 2 - Inventory (Disabled)

    /// <summary>
    /// Updates inventory levels in Merchant360 for a specific merchant.
    /// </summary>
    /// <remarks>Phase 2 - Not implemented in current release.</remarks>
    [Obsolete("Inventory push is Phase 2. Do not use in Phase 1.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<InventoryUpdateResult> UpdateInventoryAsync(
        int merchantId,
        int tradingPartnerId,
        IEnumerable<InventoryUpdateItem> items,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Inventory push is disabled in Phase 1. MerchantId={MerchantId}, TradingPartnerId={TradingPartnerId}",
            merchantId, tradingPartnerId);

        return Task.FromResult(new InventoryUpdateResult(
            Success: false,
            UpdatedCount: 0,
            SkippedCount: 0,
            ErrorCount: items.Count(),
            Errors: new[] { "Inventory push is disabled in Phase 1" }));
    }

    #endregion

    #region Subscription Management (Stub for Phase 1)

    public async Task<SubscriptionListResult> GetSubscriptionsAsync(
        string? status,
        int? tenantId,
        int? tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(status))
                queryParams.Add($"status={status}");
            if (tenantId.HasValue)
                queryParams.Add($"tenantId={tenantId}");
            if (tradingPartnerId.HasValue)
                queryParams.Add($"tradingPartnerId={tradingPartnerId}");

            var url = "/api/v1/partner-connect/subscriptions";
            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SubscriptionListResult>(cancellationToken)
                    ?? new SubscriptionListResult();
            }

            _logger.LogWarning("Failed to get subscriptions: {StatusCode}", response.StatusCode);
            return new SubscriptionListResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching subscriptions from Merchant360");
            return new SubscriptionListResult();
        }
    }

    public async Task<MerchantSubscriptionDto?> GetSubscriptionAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/partner-connect/subscriptions/{id}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<MerchantSubscriptionDto>(cancellationToken);
            }

            _logger.LogWarning("Failed to get subscription {Id}: {StatusCode}", id, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching subscription {Id} from Merchant360", id);
            return null;
        }
    }

    public async Task<MerchantSubscriptionDto?> CreateSubscriptionAsync(
        CreateSubscriptionDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/partner-connect/subscriptions",
                request,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<MerchantSubscriptionDto>(cancellationToken);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to create subscription: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while creating subscription in Merchant360");
            return null;
        }
    }

    public async Task<bool> ApproveSubscriptionAsync(
        int id,
        ApproveSubscriptionDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/partner-connect/subscriptions/{id}/approve",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to approve subscription {Id}: {StatusCode} - {Error}",
                    id, response.StatusCode, errorContent);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while approving subscription {Id}", id);
            return false;
        }
    }

    public async Task<bool> DenySubscriptionAsync(
        int id,
        DenySubscriptionDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/partner-connect/subscriptions/{id}/deny",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to deny subscription {Id}: {StatusCode} - {Error}",
                    id, response.StatusCode, errorContent);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while denying subscription {Id}", id);
            return false;
        }
    }

    public async Task<bool> SuspendSubscriptionAsync(
        int id,
        SuspendSubscriptionDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/partner-connect/subscriptions/{id}/suspend",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to suspend subscription {Id}: {StatusCode} - {Error}",
                    id, response.StatusCode, errorContent);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while suspending subscription {Id}", id);
            return false;
        }
    }

    public async Task<bool> ReactivateSubscriptionAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/v1/partner-connect/subscriptions/{id}/reactivate",
                null,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to reactivate subscription {Id}: {StatusCode} - {Error}",
                    id, response.StatusCode, errorContent);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while reactivating subscription {Id}", id);
            return false;
        }
    }

    /// <summary>
    /// Notifies Merchant360 about a subscription status change.
    /// Called when PC admin approves, denies, suspends, reactivates, or unsubscribes a subscription.
    /// </summary>
    public async Task<bool> NotifySubscriptionStatusChangedAsync(
        SubscriptionStatusChangedDto statusChange,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Notifying M360 about subscription status change: TenantId={TenantId}, TradingPartnerId={TradingPartnerId}, {PreviousStatus} -> {NewStatus}",
            statusChange.TenantId, statusChange.TradingPartnerId, statusChange.PreviousStatus, statusChange.NewStatus);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/partner-connect/subscription-status-changed",
                statusChange,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully notified M360 about subscription status change for TenantId={TenantId}, TradingPartnerId={TradingPartnerId}",
                    statusChange.TenantId, statusChange.TradingPartnerId);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to notify M360 about subscription status change for TenantId={TenantId}, TradingPartnerId={TradingPartnerId}: {StatusCode} - {Error}",
                statusChange.TenantId, statusChange.TradingPartnerId, response.StatusCode, errorContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception while notifying M360 about subscription status change for TenantId={TenantId}, TradingPartnerId={TradingPartnerId}",
                statusChange.TenantId, statusChange.TradingPartnerId);
            return false;
        }
    }

    #endregion
}
