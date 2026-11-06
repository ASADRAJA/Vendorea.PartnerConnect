using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Infrastructure.Amazon;

/// <summary>
/// Login with Amazon token exchange and refresh.
/// </summary>
/// <remarks>
/// Nothing here logs a code, a refresh token or an access token — not at Debug, not in an error
/// path. Amazon's error responses are safe to log; the request bodies that produce them are not,
/// and a single well-meant "log the payload on failure" would put a seller's refresh token in
/// Serilog's sink permanently.
/// </remarks>
public class AmazonLwaClient : IAmazonLwaClient
{
    private readonly HttpClient _http;
    private readonly AmazonLwaOptions _options;
    private readonly ILogger<AmazonLwaClient> _logger;

    public AmazonLwaClient(
        HttpClient http,
        IOptions<AmazonLwaOptions> options,
        ILogger<AmazonLwaClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured && !string.IsNullOrWhiteSpace(_options.ApplicationId);

    public Task<LwaTokenResult> ExchangeAuthorizationCodeAsync(
        string authorizationCode, CancellationToken cancellationToken = default)
        => PostAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            // Amazon requires this to match the registered URI exactly, even though the browser
            // has already been redirected by the time we get here.
            ["redirect_uri"] = _options.RedirectUri,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        }, "authorization code exchange", cancellationToken);

    public Task<LwaTokenResult> RefreshAccessTokenAsync(
        string refreshToken, CancellationToken cancellationToken = default)
        => PostAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        }, "access token refresh", cancellationToken);

    public string BuildConsentUrl(string state)
    {
        var url = $"{_options.SellerCentralBaseUrl.TrimEnd('/')}/apps/authorize/consent" +
                  $"?application_id={Uri.EscapeDataString(_options.ApplicationId)}" +
                  $"&state={Uri.EscapeDataString(state)}";

        // A draft application's consent page requires version=beta; a published one rejects it.
        // Getting this wrong is the single most common reason a seller sees an Amazon error page
        // instead of a consent screen, so it is driven by configuration rather than guessed.
        return _options.DraftApplication ? url + "&version=beta" : url;
    }

    private async Task<LwaTokenResult> PostAsync(
        Dictionary<string, string> form, string operation, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return LwaTokenResult.Failed(
                "Amazon LWA is not configured (Amazon:Lwa ClientId/ClientSecret/RedirectUri).");
        }

        try
        {
            using var response = await _http.PostAsync(
                _options.TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response, cancellationToken);
                _logger.LogWarning(
                    "Amazon LWA {Operation} failed: {StatusCode} {Error}",
                    operation, (int)response.StatusCode, error);
                return LwaTokenResult.Failed(error);
            }

            var token = await response.Content.ReadFromJsonAsync<LwaTokenResponse>(cancellationToken);
            if (token is null || string.IsNullOrEmpty(token.AccessToken))
            {
                return LwaTokenResult.Failed("Amazon returned no access token.");
            }

            return LwaTokenResult.Ok(
                token.AccessToken,
                token.RefreshToken,
                DateTime.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // The message, not the exception with its request context: HttpRequestException can
            // carry the request URI, and a form-encoded body has been seen in some handlers' traces.
            _logger.LogWarning("Amazon LWA {Operation} could not reach Amazon: {Message}",
                operation, ex.Message);
            return LwaTokenResult.Failed("Amazon could not be reached.");
        }
    }

    /// <summary>Amazon's own error code, which is safe to surface and useful to an operator.</summary>
    private static async Task<string> ReadErrorAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<LwaErrorResponse>(cancellationToken);
            if (!string.IsNullOrWhiteSpace(error?.Error))
            {
                return string.IsNullOrWhiteSpace(error!.Description)
                    ? error.Error!
                    : $"{error.Error}: {error.Description}";
            }
        }
        catch
        {
            // Non-JSON body; fall through.
        }

        return $"HTTP {(int)response.StatusCode}";
    }

    private sealed class LwaTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private sealed class LwaErrorResponse
    {
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("error_description")] public string? Description { get; set; }
    }
}
