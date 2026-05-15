using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Contracts.Interfaces;

namespace Vendorea.PartnerConnect.Merchant360Connector;

/// <summary>
/// HTTP client for communicating with the Merchant360 API.
/// Used to push processed partner data (prices, inventory) to merchants.
/// </summary>
public class Merchant360ApiClient : IMerchant360Client
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<Merchant360ApiClient> _logger;

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

    public async Task<IReadOnlyList<Merchant360Merchant>> GetMerchantsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/partner-connect/merchants", cancellationToken);

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

    public async Task<PriceUpdateResult> UpdatePricesAsync(
        int merchantId,
        TradingPartnerInfo tradingPartner,
        IEnumerable<PriceUpdateItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemsList = items.ToList();
        _logger.LogInformation("Updating {Count} prices for merchant {MerchantId} from partner {PartnerCode}",
            itemsList.Count, merchantId, tradingPartner.Code);

        try
        {
            var payload = new
            {
                TradingPartner = new
                {
                    tradingPartner.PartnerConnectId,
                    tradingPartner.Code,
                    tradingPartner.Name,
                    tradingPartner.Description,
                    tradingPartner.LogoUrl
                },
                Items = itemsList
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/partner-connect/merchants/{merchantId}/prices/batch",
                payload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated prices for merchant {MerchantId}", merchantId);
                return new PriceUpdateResult(
                    Success: true,
                    UpdatedCount: itemsList.Count,
                    SkippedCount: 0,
                    ErrorCount: 0,
                    Errors: null);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Price update failed for merchant {MerchantId}: {StatusCode} - {Error}",
                merchantId, response.StatusCode, errorContent);

            return new PriceUpdateResult(
                Success: false,
                UpdatedCount: 0,
                SkippedCount: 0,
                ErrorCount: itemsList.Count,
                Errors: new[] { $"API returned {response.StatusCode}: {errorContent}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while updating prices for merchant {MerchantId}", merchantId);
            return new PriceUpdateResult(
                Success: false,
                UpdatedCount: 0,
                SkippedCount: 0,
                ErrorCount: itemsList.Count,
                Errors: new[] { ex.Message });
        }
    }

    public async Task<InventoryUpdateResult> UpdateInventoryAsync(
        int merchantId,
        TradingPartnerInfo tradingPartner,
        IEnumerable<InventoryUpdateItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemsList = items.ToList();
        _logger.LogInformation("Updating {Count} inventory items for merchant {MerchantId} from partner {PartnerCode}",
            itemsList.Count, merchantId, tradingPartner.Code);

        try
        {
            var payload = new
            {
                TradingPartner = new
                {
                    tradingPartner.PartnerConnectId,
                    tradingPartner.Code,
                    tradingPartner.Name,
                    tradingPartner.Description,
                    tradingPartner.LogoUrl
                },
                Items = itemsList
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/partner-connect/merchants/{merchantId}/inventory/batch",
                payload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated inventory for merchant {MerchantId}", merchantId);
                return new InventoryUpdateResult(
                    Success: true,
                    UpdatedCount: itemsList.Count,
                    SkippedCount: 0,
                    ErrorCount: 0,
                    Errors: null);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Inventory update failed for merchant {MerchantId}: {StatusCode} - {Error}",
                merchantId, response.StatusCode, errorContent);

            return new InventoryUpdateResult(
                Success: false,
                UpdatedCount: 0,
                SkippedCount: 0,
                ErrorCount: itemsList.Count,
                Errors: new[] { $"API returned {response.StatusCode}: {errorContent}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while updating inventory for merchant {MerchantId}", merchantId);
            return new InventoryUpdateResult(
                Success: false,
                UpdatedCount: 0,
                SkippedCount: 0,
                ErrorCount: itemsList.Count,
                Errors: new[] { ex.Message });
        }
    }

    // Subscription management methods
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
}
