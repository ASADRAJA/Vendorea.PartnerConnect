using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.PartnerAdapters.Common;
using Vendorea.PartnerConnect.Storage.Interfaces;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.PartnerAdapters.Generic;

/// <summary>
/// Configuration for the generic API adapter.
/// </summary>
public class GenericApiAdapterConfiguration
{
    /// <summary>
    /// Base URL for the API.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint for fetching documents (GET).
    /// </summary>
    public string FetchEndpoint { get; set; } = "/api/documents";

    /// <summary>
    /// Endpoint for sending documents (POST).
    /// </summary>
    public string SendEndpoint { get; set; } = "/api/documents";

    /// <summary>
    /// Authentication type: "None", "ApiKey", "Bearer", "Basic".
    /// </summary>
    public string AuthType { get; set; } = "None";

    /// <summary>
    /// API key header name (for ApiKey auth).
    /// </summary>
    public string ApiKeyHeader { get; set; } = "X-API-Key";

    /// <summary>
    /// API key value (for ApiKey auth).
    /// </summary>
    public string? ApiKey { get; set; }

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
    /// Content type for requests.
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Document type for categorization.
    /// </summary>
    public DocumentType DocumentType { get; set; } = DocumentType.PriceList;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Custom headers to include in requests.
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    /// <summary>
    /// JSONPath expression to extract documents from response (if applicable).
    /// </summary>
    public string? DocumentsJsonPath { get; set; }
}

/// <summary>
/// Generic API adapter for REST-based partner integrations.
/// </summary>
public class GenericApiAdapter : BasePartnerAdapter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDocumentStorage _documentStorage;
    private readonly IDuplicateDetectionService _duplicateDetection;
    private GenericApiAdapterConfiguration _config = new();

    public GenericApiAdapter(
        IHttpClientFactory httpClientFactory,
        IDocumentStorage documentStorage,
        IDuplicateDetectionService duplicateDetection,
        ILogger<GenericApiAdapter> logger) : base(logger)
    {
        _httpClientFactory = httpClientFactory;
        _documentStorage = documentStorage;
        _duplicateDetection = duplicateDetection;
    }

    public override string PartnerCode => "GENERIC_API";

    public override IReadOnlyList<PartnerCapability> SupportedCapabilities =>
        new List<PartnerCapability> { PartnerCapability.PriceFeed, PartnerCapability.InventoryFeed };

    /// <summary>
    /// Configures the adapter with specific settings.
    /// </summary>
    public void Configure(GenericApiAdapterConfiguration configuration)
    {
        _config = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<IReadOnlyList<PartnerDocument>> FetchDocumentsAsync(
        TradingPartner partner,
        CancellationToken cancellationToken = default)
    {
        var documents = new List<PartnerDocument>();

        try
        {
            LogInfo("Fetching documents from API for partner {ConnectionId}", partner.Id);

            using var client = CreateHttpClient(partner);
            var url = $"{_config.BaseUrl.TrimEnd('/')}{_config.FetchEndpoint}";

            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentBytes = Encoding.UTF8.GetBytes(content);

            // Check for duplicates using content hash
            var contentHash = _duplicateDetection.ComputeHash(contentBytes);
            if (await _duplicateDetection.IsDuplicateAsync(
                partner.Id,
                _config.DocumentType,
                contentHash,
                cancellationToken))
            {
                LogInfo("Skipping duplicate API response");
                return documents;
            }

            // Store raw response
            var fileName = $"api_response_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            var storagePath = $"raw/{partner.Id}/{_config.DocumentType}/{DateTime.UtcNow:yyyyMMdd}/{fileName}";
            var storageKey = await _documentStorage.StoreAsync(
                new MemoryStream(contentBytes),
                storagePath,
                new Storage.Models.StorageMetadata
                {
                    OriginalFileName = fileName,
                    ContentType = _config.ContentType,
                    DealerId = partner.Id,
                    TradingPartnerCode = partner.Code,
                    DocumentType = _config.DocumentType.ToString(),
                    SizeBytes = contentBytes.Length,
                    ContentHash = contentHash
                },
                cancellationToken);

            // Create document record
            var document = new PartnerDocument
            {
                TradingPartnerId = partner.Id,
                DocumentType = _config.DocumentType,
                Direction = DocumentDirection.Inbound,
                FileName = fileName,
                ContentHash = contentHash,
                FileSizeBytes = contentBytes.Length,
                StoragePath = storageKey,
                ReceivedAt = DateTime.UtcNow
            };

            // Register fingerprint
            await _duplicateDetection.RegisterFingerprintAsync(
                partner.Id,
                _config.DocumentType,
                contentHash,
                document.Id,
                fileName,
                contentBytes.Length,
                null,
                cancellationToken);

            documents.Add(document);
            LogInfo("Successfully fetched API response");

            return documents;
        }
        catch (Exception ex)
        {
            LogError(ex, "Error fetching documents from API for partner {ConnectionId}", partner.Id);
            throw;
        }
    }

    public async Task<bool> SendDocumentAsync(
        TradingPartner partner,
        PartnerDocument document,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateHttpClient(partner);
            var url = $"{_config.BaseUrl.TrimEnd('/')}{_config.SendEndpoint}";

            using var reader = new StreamReader(content);
            var body = await reader.ReadToEndAsync(cancellationToken);

            var httpContent = new StringContent(body, Encoding.UTF8, _config.ContentType);
            var response = await client.PostAsync(url, httpContent, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                LogInfo("Successfully sent document {DocumentId} to API", document.Id);
                return true;
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                LogWarning(
                    "Failed to send document {DocumentId}: {StatusCode} - {Error}",
                    document.Id,
                    response.StatusCode,
                    errorBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error sending document {DocumentId} to API", document.Id);
            return false;
        }
    }

    public override async Task<bool> TestConnectionAsync(
        TradingPartner partner,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateHttpClient(partner);
            var url = $"{_config.BaseUrl.TrimEnd('/')}/health";

            // Try a health endpoint first, fall back to base URL
            try
            {
                var response = await client.GetAsync(url, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // Fall back to base URL
                var response = await client.GetAsync(_config.BaseUrl, cancellationToken);
                return response.IsSuccessStatusCode;
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "API partner test failed for {ConnectionId}", partner.Id);
            return false;
        }
    }

    private HttpClient CreateHttpClient(TradingPartner partner)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);

        // Apply authentication
        ApplyAuthentication(client, partner);

        // Apply custom headers
        foreach (var header in _config.CustomHeaders)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        return client;
    }

    private void ApplyAuthentication(HttpClient client, TradingPartner partner)
    {
        // Try to get credentials from partner details first, fall back to config
        var credentials = ParseCredentials(partner.TransportCredentialsJson);

        switch (_config.AuthType.ToLowerInvariant())
        {
            case "apikey":
                var apiKey = GetCredentialValue(credentials, "apiKey") ?? _config.ApiKey;
                if (!string.IsNullOrEmpty(apiKey))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(_config.ApiKeyHeader, apiKey);
                }
                break;

            case "bearer":
                var token = GetCredentialValue(credentials, "bearerToken") ?? _config.BearerToken;
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                break;

            case "basic":
                var username = GetCredentialValue(credentials, "username") ?? _config.Username;
                var password = GetCredentialValue(credentials, "password") ?? _config.Password;
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    var credentialBytes = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentialBytes);
                }
                break;
        }
    }

    private static Dictionary<string, JsonElement>? ParseCredentials(string? credentialsJson)
    {
        if (string.IsNullOrEmpty(credentialsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(credentialsJson);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetCredentialValue(Dictionary<string, JsonElement>? credentials, string key)
    {
        if (credentials == null)
        {
            return null;
        }

        return credentials.TryGetValue(key, out var value) ? value.GetString() : null;
    }
}
