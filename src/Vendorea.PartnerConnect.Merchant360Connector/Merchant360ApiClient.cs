using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Contracts.Interfaces;

namespace Vendorea.PartnerConnect.Merchant360Connector;

/// <summary>
/// HTTP client for communicating with the Merchant360 API.
/// Used to push processed partner data (prices, inventory) to dealer tenants.
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

    public async Task<PriceUpdateResult> UpdatePricesAsync(
        int dealerId,
        IEnumerable<PriceUpdateItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemsList = items.ToList();
        _logger.LogInformation("Updating {Count} prices for dealer {DealerId}", itemsList.Count, dealerId);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/dealers/{dealerId}/prices/batch",
                new { Items = itemsList },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated prices for dealer {DealerId}", dealerId);
                return new PriceUpdateResult(
                    Success: true,
                    UpdatedCount: itemsList.Count,
                    SkippedCount: 0,
                    ErrorCount: 0,
                    Errors: null);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Price update failed for dealer {DealerId}: {StatusCode} - {Error}",
                dealerId, response.StatusCode, errorContent);

            return new PriceUpdateResult(
                Success: false,
                UpdatedCount: 0,
                SkippedCount: 0,
                ErrorCount: itemsList.Count,
                Errors: new[] { $"API returned {response.StatusCode}: {errorContent}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while updating prices for dealer {DealerId}", dealerId);
            return new PriceUpdateResult(
                Success: false,
                UpdatedCount: 0,
                SkippedCount: 0,
                ErrorCount: itemsList.Count,
                Errors: new[] { ex.Message });
        }
    }

    public async Task<InventoryUpdateResult> UpdateInventoryAsync(
        int dealerId,
        IEnumerable<InventoryUpdateItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemsList = items.ToList();
        _logger.LogInformation("Updating {Count} inventory items for dealer {DealerId}", itemsList.Count, dealerId);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/dealers/{dealerId}/inventory/batch",
                new { Items = itemsList },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated inventory for dealer {DealerId}", dealerId);
                return new InventoryUpdateResult(
                    Success: true,
                    UpdatedCount: itemsList.Count,
                    SkippedCount: 0,
                    ErrorCount: 0,
                    Errors: null);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Inventory update failed for dealer {DealerId}: {StatusCode} - {Error}",
                dealerId, response.StatusCode, errorContent);

            return new InventoryUpdateResult(
                Success: false,
                UpdatedCount: 0,
                SkippedCount: 0,
                ErrorCount: itemsList.Count,
                Errors: new[] { $"API returned {response.StatusCode}: {errorContent}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while updating inventory for dealer {DealerId}", dealerId);
            return new InventoryUpdateResult(
                Success: false,
                UpdatedCount: 0,
                SkippedCount: 0,
                ErrorCount: itemsList.Count,
                Errors: new[] { ex.Message });
        }
    }
}
