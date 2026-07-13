using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Vendorea.PartnerConnect.CustomerPortal.Models;

namespace Vendorea.PartnerConnect.CustomerPortal.Services;

/// <summary>
/// Typed HttpClient for the org-facing API (<c>/api/v1/org</c>). The customer portal is per-user:
/// the user signs in with email + password (<see cref="LoginAsync"/>), receives a per-user JWT, and
/// that token is stored in the <c>org_user_token</c> cookie claim at login and attached as an
/// <c>Authorization: Bearer</c> header on every request here. (The org API key remains for
/// machine/integration callers — it is not used by the human portal flow.)
/// </summary>
public class ApiClient
{
    /// <summary>Cookie claim name carrying the caller's per-user bearer token.</summary>
    public const string TokenClaim = "org_user_token";

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
    /// Signs a user in with email + password against <c>POST /api/v1/org/auth/login</c> (anonymous —
    /// no token needed yet). Returns the minted token + user/org summary on success, or null on
    /// invalid credentials / transport error.
    /// </summary>
    public async Task<OrgLoginResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/org/auth/login")
            {
                Content = JsonContent.Create(new { email, password })
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<OrgLoginResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Org portal login failed");
            return null;
        }
    }

    /// <summary>
    /// Fetches activation context for a link (anonymous — <c>GET /api/v1/org/auth/activation</c>). No
    /// bearer token is attached; the activation page is public. Returns the invitee's email/org on a
    /// valid token, or a failure with the API's generic error message.
    /// </summary>
    public async Task<ActivationResult> GetActivationInfoAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"/api/v1/org/auth/activation?token={Uri.EscapeDataString(token)}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var info = await response.Content.ReadFromJsonAsync<ActivationInfoDto>(cancellationToken: cancellationToken);
                return ActivationResult.Ok(info);
            }
            return ActivationResult.Fail(await ReadErrorAsync(response, "This activation link is invalid or has expired.", cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch activation info");
            return ActivationResult.Fail("Couldn't reach the server. Please try again.");
        }
    }

    /// <summary>
    /// Redeems an activation token by setting the user's password (anonymous —
    /// <c>POST /api/v1/org/auth/activate</c>). Returns success, or the API's error message on failure.
    /// </summary>
    public async Task<ActivationResult> ActivateAsync(string token, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/org/auth/activate")
            {
                Content = JsonContent.Create(new { token, password })
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return ActivationResult.Ok();
            return ActivationResult.Fail(await ReadErrorAsync(response, "Couldn't set your password. The link may have expired.", cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate account");
            return ActivationResult.Fail("Couldn't reach the server. Please try again.");
        }
    }

    /// <summary>
    /// Lists selectable billing plans (anonymous — <c>GET /api/v1/public/plans</c>) to populate the
    /// public Register form. Returns an empty list on any transport error.
    /// </summary>
    public async Task<List<PublicPlanDto>> GetPublicPlansAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/public/plans");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new List<PublicPlanDto>();
            return await response.Content.ReadFromJsonAsync<List<PublicPlanDto>>(cancellationToken: cancellationToken)
                   ?? new List<PublicPlanDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch public plans");
            return new List<PublicPlanDto>();
        }
    }

    /// <summary>
    /// Submits a self-service org registration (anonymous — <c>POST /api/v1/public/org-registrations</c>).
    /// No auth cookie/bearer token is attached. Returns the API's "pending review" message on success
    /// (202), or its error message on failure.
    /// </summary>
    public async Task<RegistrationResult> RegisterOrganizationAsync(
        string organizationName, string planCode, string adminDisplayName, string adminEmail,
        string? contactPhone, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/org-registrations")
            {
                Content = JsonContent.Create(new
                {
                    organizationName,
                    planCode,
                    adminDisplayName,
                    adminEmail,
                    contactPhone
                })
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var message = await ReadMessageAsync(response,
                    "Thanks — your registration is under review. You'll get an email when it's approved.",
                    cancellationToken);
                return RegistrationResult.Ok(message);
            }
            return RegistrationResult.Fail(await ReadErrorAsync(response, "We couldn't submit your registration. Please try again.", cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit org registration");
            return RegistrationResult.Fail("Couldn't reach the server. Please try again.");
        }
    }

    /// <summary>Extracts the API's <c>{ message }</c> from a success response, or a fallback.</summary>
    private static async Task<string> ReadMessageAsync(HttpResponseMessage response, string fallback, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == System.Text.Json.JsonValueKind.String)
                return msg.GetString() ?? fallback;
        }
        catch { /* non-JSON body */ }
        return fallback;
    }

    /// <summary>Extracts the API's <c>{ error }</c> message from a failure response, or a fallback.</summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == System.Text.Json.JsonValueKind.String)
                return err.GetString() ?? fallback;
        }
        catch { /* non-JSON body */ }
        return fallback;
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

    // ========================================================================================
    // Orders (increment 4) + Activity — tenant-scoped, read-only, server-paged.
    // ========================================================================================

    /// <summary>The tenant's orders (paged), filterable by partner, status, and order-date range.</summary>
    public async Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(
        int tenantId, string? partnerCode, string? status, DateTime? from, DateTime? to,
        int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("partnerCode", partnerCode),
            ("status", status),
            ("from", from?.ToString("o")),
            ("to", to?.ToString("o")),
            ("skip", skip.ToString()),
            ("take", take.ToString()));
        return await GetPagedAsync<OrderSummaryDto>($"/api/v1/org/tenants/{tenantId}/orders{query}", cancellationToken);
    }

    /// <summary>Full detail for one order incl. its document chain. Null on non-success/transport error.</summary>
    public async Task<OrderDetailDto?> GetOrderAsync(int tenantId, int orderId, CancellationToken cancellationToken = default)
        => await GetJsonAsync<OrderDetailDto>($"/api/v1/org/tenants/{tenantId}/orders/{orderId}", cancellationToken);

    /// <summary>The tenant's activity feed (paged), filterable by type, level, and date range.</summary>
    public async Task<PagedResult<ActivityEventDto>> GetActivityAsync(
        int tenantId, string? type, string? level, DateTime? from, DateTime? to,
        int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("type", type),
            ("level", level),
            ("from", from?.ToString("o")),
            ("to", to?.ToString("o")),
            ("skip", skip.ToString()),
            ("take", take.ToString()));
        return await GetPagedAsync<ActivityEventDto>($"/api/v1/org/tenants/{tenantId}/activity{query}", cancellationToken);
    }

    // ========================================================================================
    // Organization admin (increment 5): tenants (read-only), settings, dashboard summary.
    // ========================================================================================

    /// <summary>The org's tenants (read-only; operator-provisioned). Empty list on error.</summary>
    public async Task<List<OrgTenantRowDto>> GetTenantsAsync(CancellationToken cancellationToken = default)
        => await GetListAsync<OrgTenantRowDto>("/api/v1/org/tenants", cancellationToken);

    /// <summary>The org's editable profile. Null on any non-success/transport error.</summary>
    public async Task<OrgSettingsDto?> GetSettingsAsync(CancellationToken cancellationToken = default)
        => await GetJsonAsync<OrgSettingsDto>("/api/v1/org/settings", cancellationToken);

    /// <summary>Updates the org's editable profile fields.</summary>
    public async Task<SettingsSaveResult> UpdateSettingsAsync(UpdateOrgSettingsRequest body, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Put, "/api/v1/org/settings");
            request.Content = JsonContent.Create(body);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var settings = await response.Content.ReadFromJsonAsync<OrgSettingsDto>(cancellationToken: cancellationToken);
                return SettingsSaveResult.Ok(settings);
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

            return SettingsSaveResult.Fail(message ?? $"Couldn't save settings ({(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update org settings");
            return SettingsSaveResult.Fail("Couldn't reach the server. Please try again.");
        }
    }

    /// <summary>One-call dashboard summary for a tenant. Null on any non-success/transport error.</summary>
    public async Task<TenantSummaryDto?> GetTenantSummaryAsync(int tenantId, CancellationToken cancellationToken = default)
        => await GetJsonAsync<TenantSummaryDto>($"/api/v1/org/tenants/{tenantId}/summary", cancellationToken);

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

    /// <summary>Builds a request with the current user's bearer token attached as Authorization.</summary>
    private HttpRequestMessage BuildRequest(HttpMethod method, string requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        var token = _httpContextAccessor.HttpContext?.User?.FindFirst(TokenClaim)?.Value;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }
}
