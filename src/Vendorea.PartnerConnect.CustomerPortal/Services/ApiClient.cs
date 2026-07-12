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
