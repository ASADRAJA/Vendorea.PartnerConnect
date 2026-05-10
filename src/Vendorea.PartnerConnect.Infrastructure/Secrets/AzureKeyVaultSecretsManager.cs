using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vendorea.PartnerConnect.Infrastructure.Secrets;

/// <summary>
/// Configuration for Azure Key Vault secrets manager.
/// </summary>
public class AzureKeyVaultOptions
{
    /// <summary>
    /// The Key Vault URI (e.g., https://myvault.vault.azure.net/).
    /// </summary>
    public string VaultUri { get; set; } = string.Empty;

    /// <summary>
    /// Optional tenant ID for authentication.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Optional client ID for service principal authentication.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Optional client secret for service principal authentication.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Whether to use managed identity (default true in production).
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Cache duration for secrets in minutes.
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 5;
}

/// <summary>
/// Azure Key Vault implementation of secrets manager.
/// </summary>
public class AzureKeyVaultSecretsManager : ISecretsManager
{
    private readonly SecretClient _client;
    private readonly AzureKeyVaultOptions _options;
    private readonly ILogger<AzureKeyVaultSecretsManager> _logger;
    private readonly Dictionary<string, CachedSecret> _cache = new();
    private readonly object _cacheLock = new();

    public AzureKeyVaultSecretsManager(
        IOptions<AzureKeyVaultOptions> options,
        ILogger<AzureKeyVaultSecretsManager> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_options.VaultUri))
        {
            throw new ArgumentException("Key Vault URI is required", nameof(options));
        }

        var credential = CreateCredential();
        _client = new SecretClient(new Uri(_options.VaultUri), credential);
    }

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        // Check cache first
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(secretName, out var cached) && !cached.IsExpired)
            {
                return cached.Value;
            }
        }

        try
        {
            var response = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            var secret = response.Value;

            // Check if secret is enabled
            if (secret.Properties.Enabled == false)
            {
                _logger.LogWarning("Secret {SecretName} is disabled", secretName);
                return null;
            }

            // Check if secret is expired
            if (secret.Properties.ExpiresOn.HasValue && secret.Properties.ExpiresOn.Value < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Secret {SecretName} has expired", secretName);
                return null;
            }

            // Cache the secret
            lock (_cacheLock)
            {
                _cache[secretName] = new CachedSecret
                {
                    Value = secret.Value,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_options.CacheDurationMinutes)
                };
            }

            return secret.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Secret {SecretName} not found", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving secret {SecretName}", secretName);
            throw new SecretsException($"Failed to retrieve secret '{secretName}'", ex);
        }
    }

    public async Task<string?> GetSecretAsync(string secretName, string version, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetSecretAsync(secretName, version, cancellationToken);
            return response.Value.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Secret {SecretName} version {Version} not found", secretName, version);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving secret {SecretName} version {Version}", secretName, version);
            throw new SecretsException($"Failed to retrieve secret '{secretName}' version '{version}'", ex);
        }
    }

    public async Task SetSecretAsync(string secretName, string value, CancellationToken cancellationToken = default)
    {
        await SetSecretAsync(secretName, value, new SecretMetadata { Name = secretName }, cancellationToken);
    }

    public async Task SetSecretAsync(string secretName, string value, SecretMetadata metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = new KeyVaultSecret(secretName, value);

            if (metadata.ExpiresAt.HasValue)
            {
                secret.Properties.ExpiresOn = metadata.ExpiresAt.Value;
            }

            secret.Properties.Enabled = metadata.IsEnabled;

            if (!string.IsNullOrEmpty(metadata.ContentType))
            {
                secret.Properties.ContentType = metadata.ContentType;
            }

            foreach (var tag in metadata.Tags)
            {
                secret.Properties.Tags[tag.Key] = tag.Value;
            }

            await _client.SetSecretAsync(secret, cancellationToken);

            // Invalidate cache
            lock (_cacheLock)
            {
                _cache.Remove(secretName);
            }

            _logger.LogInformation("Secret {SecretName} saved to Key Vault", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting secret {SecretName}", secretName);
            throw new SecretsException($"Failed to set secret '{secretName}'", ex);
        }
    }

    public async Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            var operation = await _client.StartDeleteSecretAsync(secretName, cancellationToken);
            await operation.WaitForCompletionAsync(cancellationToken);

            // Invalidate cache
            lock (_cacheLock)
            {
                _cache.Remove(secretName);
            }

            _logger.LogInformation("Secret {SecretName} deleted from Key Vault", secretName);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Secret {SecretName} not found for deletion", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting secret {SecretName}", secretName);
            throw new SecretsException($"Failed to delete secret '{secretName}'", ex);
        }
    }

    public async Task<bool> SecretExistsAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> ListSecretsAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var secrets = new List<string>();

        await foreach (var secretProperties in _client.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            if (string.IsNullOrEmpty(prefix) ||
                secretProperties.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                secrets.Add(secretProperties.Name);
            }
        }

        return secrets.OrderBy(s => s).ToList();
    }

    public async Task<SecretMetadata?> GetSecretMetadataAsync(string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            var properties = response.Value.Properties;

            return new SecretMetadata
            {
                Name = secretName,
                CreatedAt = properties.CreatedOn?.UtcDateTime,
                UpdatedAt = properties.UpdatedOn?.UtcDateTime,
                ExpiresAt = properties.ExpiresOn?.UtcDateTime,
                IsEnabled = properties.Enabled ?? true,
                ContentType = properties.ContentType,
                Tags = properties.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private Azure.Core.TokenCredential CreateCredential()
    {
        // If client credentials are provided, use them
        if (!string.IsNullOrEmpty(_options.ClientId) &&
            !string.IsNullOrEmpty(_options.ClientSecret) &&
            !string.IsNullOrEmpty(_options.TenantId))
        {
            return new ClientSecretCredential(
                _options.TenantId,
                _options.ClientId,
                _options.ClientSecret);
        }

        // Use managed identity or default Azure credentials
        if (_options.UseManagedIdentity)
        {
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeManagedIdentityCredential = false,
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeAzureCliCredential = false,
                ExcludeInteractiveBrowserCredential = true
            });
        }

        return new DefaultAzureCredential();
    }

    private class CachedSecret
    {
        public string Value { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }
}
