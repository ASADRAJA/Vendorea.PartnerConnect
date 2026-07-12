using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Vendorea.PartnerConnect.CustomerPortal.Models;

namespace Vendorea.PartnerConnect.CustomerPortal.Services;

/// <summary>
/// Typed HttpClient for the org-facing API (<c>/api/v1/org</c>). Unlike the Admin portal (which
/// bakes a single admin key into the client), the customer portal is per-user/org: the caller's
/// org portal API key is stored in the <c>org_api_key</c> cookie claim at login and attached as the
/// <c>X-Api-Key</c> header on every request here.
/// </summary>
public class ApiClient
{
    /// <summary>Claim name that carries the caller's org portal API key. Dev-grade — see LoginModel.</summary>
    public const string ApiKeyClaim = "org_api_key";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Validates a candidate org API key by calling <c>GET /org/me</c> with it explicitly. Used at
    /// login (before a cookie/claim exists). Returns the org context on 200, else null.
    /// </summary>
    public async Task<OrgContextDto?> ValidateKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/org/me");
            request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<OrgContextDto>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate org API key");
            return null;
        }
    }

    /// <summary>Current org + user context. Returns null on any non-success/transport error.</summary>
    public async Task<OrgContextDto?> GetOrgContextAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, "/api/v1/org/me");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET /org/me returned {StatusCode}", (int)response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<OrgContextDto>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get org context");
            return null;
        }
    }

    /// <summary>The org's connections. Returns an empty list on any non-success/transport error.</summary>
    public async Task<List<OrgConnectionDto>> GetConnectionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, "/api/v1/org/connections");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET /org/connections returned {StatusCode}", (int)response.StatusCode);
                return new();
            }
            return await response.Content.ReadFromJsonAsync<List<OrgConnectionDto>>(cancellationToken: cancellationToken) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connections");
            return new();
        }
    }

    /// <summary>Connection detail. Returns null on any non-success/transport error.</summary>
    public async Task<OrgConnectionDetailDto?> GetConnectionAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, $"/api/v1/org/connections/{id}");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET /org/connections/{Id} returned {StatusCode}", id, (int)response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<OrgConnectionDetailDto>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection {Id}", id);
            return null;
        }
    }

    /// <summary>Updates a connection's editable configuration.</summary>
    public async Task<ConnectionActionResult> UpdateConnectionAsync(int id, UpdateOrgConnectionRequest body, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Put, $"/api/v1/org/connections/{id}");
            request.Content = JsonContent.Create(body);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await ToActionResultAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update connection {Id}", id);
            return ConnectionActionResult.Fail("Couldn't reach the server. Please try again.");
        }
    }

    /// <summary>Suspends an approved connection.</summary>
    public async Task<ConnectionActionResult> SuspendConnectionAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Post, $"/api/v1/org/connections/{id}/suspend");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await ToActionResultAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suspend connection {Id}", id);
            return ConnectionActionResult.Fail("Couldn't reach the server. Please try again.");
        }
    }

    /// <summary>Disconnects (cancels/unsubscribes) a connection.</summary>
    public async Task<ConnectionActionResult> DisconnectConnectionAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Delete, $"/api/v1/org/connections/{id}");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await ToActionResultAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect connection {Id}", id);
            return ConnectionActionResult.Fail("Couldn't reach the server. Please try again.");
        }
    }

    /// <summary>A partner's distribution centers. Returns an empty list on any non-success/transport error.</summary>
    public async Task<List<OrgDistributionCenterDto>> GetPartnerDistributionCentersAsync(string partnerCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, $"/api/v1/org/partners/{Uri.EscapeDataString(partnerCode)}/distribution-centers");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET /org/partners/{Code}/distribution-centers returned {StatusCode}", partnerCode, (int)response.StatusCode);
                return new();
            }
            return await response.Content.ReadFromJsonAsync<List<OrgDistributionCenterDto>>(cancellationToken: cancellationToken) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get distribution centers for {Code}", partnerCode);
            return new();
        }
    }

    // ========================================================================================
    // Catalog (increment 3): prices, inventory, content — all tenant-scoped and read-only.
    // ========================================================================================

    /// <summary>The partners the tenant is connected to (drives the catalog partner selector).</summary>
    public async Task<List<OrgCatalogPartnerDto>> GetTenantPartnersAsync(int tenantId, CancellationToken cancellationToken = default)
        => await GetListAsync<OrgCatalogPartnerDto>($"/api/v1/org/tenants/{tenantId}/partners", cancellationToken);

    /// <summary>Current prices for a tenant + partner (paged, searchable).</summary>
    public async Task<PagedResult<PriceRowDto>> GetPricesAsync(
        int tenantId, string? partnerCode, string? search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("partnerCode", partnerCode), ("search", search), ("skip", skip.ToString()), ("take", take.ToString()));
        return await GetPagedAsync<PriceRowDto>($"/api/v1/org/tenants/{tenantId}/prices{query}", cancellationToken);
    }

    /// <summary>Price history for a single SKU (empty Points when no history is tracked).</summary>
    public async Task<PriceHistoryDto?> GetPriceHistoryAsync(
        int tenantId, string sku, string? partnerCode, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("partnerCode", partnerCode));
        return await GetJsonAsync<PriceHistoryDto>(
            $"/api/v1/org/tenants/{tenantId}/prices/{Uri.EscapeDataString(sku)}/history{query}", cancellationToken);
    }

    /// <summary>Partner-level inventory (stock by DC) for a tenant's partner (paged, searchable).</summary>
    public async Task<PagedResult<InventoryRowDto>> GetInventoryAsync(
        int tenantId, string? partnerCode, string? search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("partnerCode", partnerCode), ("search", search), ("skip", skip.ToString()), ("take", take.ToString()));
        return await GetPagedAsync<InventoryRowDto>($"/api/v1/org/tenants/{tenantId}/inventory{query}", cancellationToken);
    }

    /// <summary>Content coverage + subscription summary for a tenant + partner.</summary>
    public async Task<ContentSummaryDto?> GetContentSummaryAsync(
        int tenantId, string? partnerCode, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("partnerCode", partnerCode));
        return await GetJsonAsync<ContentSummaryDto>($"/api/v1/org/tenants/{tenantId}/content/summary{query}", cancellationToken);
    }

    /// <summary>Per-SKU content availability for a tenant + partner (paged, searchable).</summary>
    public async Task<PagedResult<ContentRowDto>> GetContentAsync(
        int tenantId, string? partnerCode, string? search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("partnerCode", partnerCode), ("search", search), ("skip", skip.ToString()), ("take", take.ToString()));
        return await GetPagedAsync<ContentRowDto>($"/api/v1/org/tenants/{tenantId}/content{query}", cancellationToken);
    }

    /// <summary>Live SPR stock/price lookup. Distinguishes no-connection / not-configured from data.</summary>
    public async Task<StockCheckResult> StockCheckAsync(StockCheckRequest body, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Post, "/api/v1/org/stock-check");
            request.Content = JsonContent.Create(body);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<StockCheckResponse>(cancellationToken: cancellationToken);
                return StockCheckResult.Success(payload);
            }

            string? message = null;
            try
            {
                using var doc = await System.Text.Json.JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == System.Text.Json.JsonValueKind.String)
                    message = err.GetString();
            }
            catch { /* non-JSON body */ }

            return StockCheckResult.Fail(message ?? $"Stock check failed ({(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stock check failed for tenant {ExternalTenantId}", body.ExternalTenantId);
            return StockCheckResult.Fail("Couldn't reach the server. Please try again.");
        }
    }

    /// <summary>GETs a paged result; returns an empty page on any non-success/transport error.</summary>
    private async Task<PagedResult<T>> GetPagedAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Uri} returned {StatusCode}", requestUri, (int)response.StatusCode);
                return new PagedResult<T>();
            }
            return await response.Content.ReadFromJsonAsync<PagedResult<T>>(cancellationToken: cancellationToken) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GET {Uri}", requestUri);
            return new PagedResult<T>();
        }
    }

    /// <summary>GETs a JSON object; returns null on any non-success/transport error.</summary>
    private async Task<T?> GetJsonAsync<T>(string requestUri, CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Uri} returned {StatusCode}", requestUri, (int)response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GET {Uri}", requestUri);
            return null;
        }
    }

    /// <summary>GETs a JSON array; returns an empty list on any non-success/transport error.</summary>
    private async Task<List<T>> GetListAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Uri} returned {StatusCode}", requestUri, (int)response.StatusCode);
                return new();
            }
            return await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken: cancellationToken) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed GET {Uri}", requestUri);
            return new();
        }
    }

    /// <summary>Builds a <c>?a=b&amp;c=d</c> query string from non-empty pairs (values URL-encoded).</summary>
    private static string BuildQuery(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}");
        var query = string.Join("&", parts);
        return string.IsNullOrEmpty(query) ? string.Empty : $"?{query}";
    }

    /// <summary>Maps a mutation response to a result, extracting the API's <c>{ error }</c> message on failure.</summary>
    private static async Task<ConnectionActionResult> ToActionResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadFromJsonAsync<OrgConnectionDetailDto>(cancellationToken: cancellationToken);
            return ConnectionActionResult.Ok(detail);
        }

        string? message = null;
        try
        {
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == System.Text.Json.JsonValueKind.String)
                message = err.GetString();
        }
        catch { /* non-JSON body */ }

        return ConnectionActionResult.Fail(message ?? $"Request failed ({(int)response.StatusCode}).");
    }

    /// <summary>Builds a request with the current user's org API key attached as X-Api-Key.</summary>
    private HttpRequestMessage BuildRequest(HttpMethod method, string requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        var apiKey = _httpContextAccessor.HttpContext?.User?.FindFirst(ApiKeyClaim)?.Value;
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }
}
