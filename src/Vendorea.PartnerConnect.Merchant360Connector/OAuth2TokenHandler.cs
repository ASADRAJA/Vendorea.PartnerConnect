using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vendorea.PartnerConnect.Merchant360Connector;

/// <summary>
/// Configuration options for Merchant360 OAuth2 authentication.
/// </summary>
public class Merchant360Options
{
    public string BaseUrl { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>
/// Delegating handler that acquires and attaches OAuth2 bearer tokens to outgoing requests.
/// Implements token caching and automatic refresh.
/// </summary>
public class OAuth2TokenHandler : DelegatingHandler
{
    private readonly Merchant360Options _options;
    private readonly ILogger<OAuth2TokenHandler> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public OAuth2TokenHandler(
        IOptions<Merchant360Options> options,
        ILogger<OAuth2TokenHandler> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        // Check if we have a valid cached token (with 30 second buffer)
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow.AddSeconds(30) < _tokenExpiry)
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow.AddSeconds(30) < _tokenExpiry)
            {
                return _cachedToken;
            }

            return await AcquireTokenAsync(cancellationToken);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<string?> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Acquiring OAuth2 token from {TokenEndpoint}", _options.TokenEndpoint);

        try
        {
            using var httpClient = new HttpClient();

            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = "merchant360.prices.write merchant360.content.write merchant360.merchants.read merchant360.trading-partners.read"
            });

            var response = await httpClient.PostAsync(_options.TokenEndpoint, tokenRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to acquire OAuth2 token: {StatusCode} - {Error}",
                    response.StatusCode, errorContent);
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("Invalid token response from OAuth2 endpoint");
                return null;
            }

            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            _logger.LogDebug("Successfully acquired OAuth2 token, expires in {ExpiresIn} seconds",
                tokenResponse.ExpiresIn);

            return _cachedToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while acquiring OAuth2 token");
            return null;
        }
    }

    private class TokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("tokenType")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expiresIn")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }
}
