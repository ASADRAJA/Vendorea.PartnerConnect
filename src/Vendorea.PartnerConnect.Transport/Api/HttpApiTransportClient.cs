using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Transport.Interfaces;

namespace Vendorea.PartnerConnect.Transport.Api;

/// <summary>
/// Configuration for HTTP API transport.
/// </summary>
public class HttpApiConfiguration
{
    /// <summary>
    /// The base URL for the API.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Authentication type: None, ApiKey, Bearer, Basic, OAuth2.
    /// </summary>
    public string AuthType { get; set; } = "None";

    /// <summary>
    /// API key value (for ApiKey auth).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// API key header name (for ApiKey auth).
    /// </summary>
    public string ApiKeyHeader { get; set; } = "X-API-Key";

    /// <summary>
    /// Bearer token (for Bearer auth).
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Username (for Basic auth).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password (for Basic auth).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// OAuth2 client ID.
    /// </summary>
    public string? OAuth2ClientId { get; set; }

    /// <summary>
    /// OAuth2 client secret.
    /// </summary>
    public string? OAuth2ClientSecret { get; set; }

    /// <summary>
    /// OAuth2 token endpoint.
    /// </summary>
    public string? OAuth2TokenUrl { get; set; }

    /// <summary>
    /// OAuth2 scopes.
    /// </summary>
    public string? OAuth2Scopes { get; set; }

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Default content type for requests.
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Custom headers to include in all requests.
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    /// <summary>
    /// Whether to retry failed requests.
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial retry delay in milliseconds.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
}

/// <summary>
/// HTTP API transport client for REST-based partner integrations.
/// </summary>
public class HttpApiTransportClient : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpApiConfiguration _config;
    private readonly ILogger<HttpApiTransportClient> _logger;
    private string? _cachedOAuth2Token;
    private DateTime _tokenExpiresAt;

    public HttpApiTransportClient(
        IHttpClientFactory httpClientFactory,
        HttpApiConfiguration configuration,
        ILogger<HttpApiTransportClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _config = configuration;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        _httpClient.BaseAddress = new Uri(_config.BaseUrl.TrimEnd('/') + "/");

        ApplyAuthentication();
        ApplyCustomHeaders();
    }

    /// <summary>
    /// Sends a GET request.
    /// </summary>
    public async Task<HttpApiResponse<T>> GetAsync<T>(
        string endpoint,
        Dictionary<string, string>? queryParams = null,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(endpoint, queryParams);
        return await ExecuteWithRetryAsync<T>(
            () => _httpClient.GetAsync(url, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Sends a POST request with JSON body.
    /// </summary>
    public async Task<HttpApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest data,
        CancellationToken cancellationToken = default)
    {
        var content = CreateJsonContent(data);
        return await ExecuteWithRetryAsync<TResponse>(
            () => _httpClient.PostAsync(endpoint, content, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Sends a POST request with raw content.
    /// </summary>
    public async Task<HttpApiResponse<TResponse>> PostAsync<TResponse>(
        string endpoint,
        byte[] data,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        return await ExecuteWithRetryAsync<TResponse>(
            () => _httpClient.PostAsync(endpoint, content, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Sends a PUT request with JSON body.
    /// </summary>
    public async Task<HttpApiResponse<TResponse>> PutAsync<TRequest, TResponse>(
        string endpoint,
        TRequest data,
        CancellationToken cancellationToken = default)
    {
        var content = CreateJsonContent(data);
        return await ExecuteWithRetryAsync<TResponse>(
            () => _httpClient.PutAsync(endpoint, content, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Sends a DELETE request.
    /// </summary>
    public async Task<HttpApiResponse<T>> DeleteAsync<T>(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync<T>(
            () => _httpClient.DeleteAsync(endpoint, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Downloads a file from the API.
    /// </summary>
    public async Task<HttpApiDownloadResult> DownloadAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        var result = new HttpApiDownloadResult();

        try
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            result.StatusCode = (int)response.StatusCode;
            result.IsSuccessful = response.IsSuccessStatusCode;

            if (response.IsSuccessStatusCode)
            {
                result.Content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                result.ContentType = response.Content.Headers.ContentType?.MediaType;
                result.FileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
            }
            else
            {
                result.ErrorMessage = await response.Content.ReadAsStringAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading from {Endpoint}", endpoint);
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Uploads a file to the API.
    /// </summary>
    public async Task<HttpApiResponse<TResponse>> UploadAsync<TResponse>(
        string endpoint,
        Stream content,
        string fileName,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
    {
        using var formContent = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        formContent.Add(streamContent, "file", fileName);

        return await ExecuteWithRetryAsync<TResponse>(
            () => _httpClient.PostAsync(endpoint, formContent, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Tests the API connection.
    /// </summary>
    public async Task<bool> TestConnectionAsync(
        string? healthEndpoint = "/health",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(healthEndpoint, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            // Try base URL if health endpoint fails
            try
            {
                var response = await _httpClient.GetAsync("", cancellationToken);
                return response.IsSuccessStatusCode || (int)response.StatusCode < 500;
            }
            catch
            {
                return false;
            }
        }
    }

    private async Task<HttpApiResponse<T>> ExecuteWithRetryAsync<T>(
        Func<Task<HttpResponseMessage>> requestFunc,
        CancellationToken cancellationToken)
    {
        var result = new HttpApiResponse<T>();
        var attempts = 0;
        var delay = _config.RetryDelayMs;

        while (attempts < (_config.EnableRetry ? _config.MaxRetryAttempts : 1))
        {
            attempts++;

            try
            {
                // Refresh OAuth2 token if needed
                await EnsureValidTokenAsync(cancellationToken);

                var response = await requestFunc();
                result.StatusCode = (int)response.StatusCode;
                result.Headers = response.Headers.ToDictionary(
                    h => h.Key,
                    h => string.Join(", ", h.Value));

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    result.IsSuccessful = true;
                    result.RawContent = content;

                    if (!string.IsNullOrEmpty(content))
                    {
                        try
                        {
                            result.Data = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        }
                        catch
                        {
                            // Content isn't JSON or doesn't match expected type
                        }
                    }

                    return result;
                }
                else
                {
                    result.ErrorMessage = await response.Content.ReadAsStringAsync(cancellationToken);

                    // Don't retry on client errors (4xx) except 429 (rate limit)
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500 &&
                        response.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
                    {
                        return result;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                result.ErrorMessage = "Request timed out";
                throw;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _logger.LogWarning(ex, "Request attempt {Attempt} failed", attempts);
            }

            if (attempts < _config.MaxRetryAttempts && _config.EnableRetry)
            {
                await Task.Delay(delay, cancellationToken);
                delay *= 2; // Exponential backoff
            }
        }

        result.IsSuccessful = false;
        return result;
    }

    private void ApplyAuthentication()
    {
        switch (_config.AuthType.ToLowerInvariant())
        {
            case "apikey":
                if (!string.IsNullOrEmpty(_config.ApiKey))
                {
                    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                        _config.ApiKeyHeader,
                        _config.ApiKey);
                }
                break;

            case "bearer":
                if (!string.IsNullOrEmpty(_config.BearerToken))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _config.BearerToken);
                }
                break;

            case "basic":
                if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.Password))
                {
                    var credentials = Convert.ToBase64String(
                        Encoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}"));
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Basic", credentials);
                }
                break;

            case "oauth2":
                // OAuth2 token is fetched dynamically
                break;
        }
    }

    private async Task EnsureValidTokenAsync(CancellationToken cancellationToken)
    {
        if (_config.AuthType.Equals("oauth2", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(_cachedOAuth2Token) || DateTime.UtcNow >= _tokenExpiresAt)
            {
                await RefreshOAuth2TokenAsync(cancellationToken);
            }
        }
    }

    private async Task RefreshOAuth2TokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_config.OAuth2TokenUrl))
        {
            throw new InvalidOperationException("OAuth2 token URL is required");
        }

        using var tokenClient = new HttpClient();
        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _config.OAuth2ClientId ?? "",
            ["client_secret"] = _config.OAuth2ClientSecret ?? ""
        };

        if (!string.IsNullOrEmpty(_config.OAuth2Scopes))
        {
            tokenRequest["scope"] = _config.OAuth2Scopes;
        }

        var response = await tokenClient.PostAsync(
            _config.OAuth2TokenUrl,
            new FormUrlEncodedContent(tokenRequest),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);

        _cachedOAuth2Token = tokenResponse.GetProperty("access_token").GetString();
        var expiresIn = tokenResponse.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60); // Refresh 1 minute early

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _cachedOAuth2Token);

        _logger.LogDebug("OAuth2 token refreshed, expires at {ExpiresAt}", _tokenExpiresAt);
    }

    private void ApplyCustomHeaders()
    {
        foreach (var header in _config.CustomHeaders)
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private static string BuildUrl(string endpoint, Dictionary<string, string>? queryParams)
    {
        if (queryParams == null || !queryParams.Any())
        {
            return endpoint;
        }

        var queryString = string.Join("&", queryParams.Select(
            kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return endpoint.Contains('?')
            ? $"{endpoint}&{queryString}"
            : $"{endpoint}?{queryString}";
    }

    private StringContent CreateJsonContent<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return new StringContent(json, Encoding.UTF8, _config.ContentType);
    }

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Response from an HTTP API request.
/// </summary>
public class HttpApiResponse<T>
{
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public T? Data { get; set; }
    public string? RawContent { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
}

/// <summary>
/// Result of a file download from the API.
/// </summary>
public class HttpApiDownloadResult
{
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public byte[]? Content { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
    public string? ErrorMessage { get; set; }
}
