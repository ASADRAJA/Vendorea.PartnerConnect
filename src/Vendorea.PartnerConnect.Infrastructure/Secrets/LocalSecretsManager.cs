using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vendorea.PartnerConnect.Infrastructure.Secrets;

/// <summary>
/// Configuration for local secrets manager.
/// </summary>
public class LocalSecretsOptions
{
    /// <summary>
    /// The directory to store secrets. Defaults to user's app data.
    /// </summary>
    public string? SecretsDirectory { get; set; }

    /// <summary>
    /// The name of the secrets file.
    /// </summary>
    public string SecretsFileName { get; set; } = "secrets.json";

    /// <summary>
    /// Whether to encrypt secrets at rest.
    /// </summary>
    public bool EncryptAtRest { get; set; } = true;

    /// <summary>
    /// Optional encryption key. If not provided, a machine-specific key is used.
    /// </summary>
    public string? EncryptionKey { get; set; }
}

/// <summary>
/// Local file-based secrets manager for development and testing.
/// NOT recommended for production use.
/// </summary>
public class LocalSecretsManager : ISecretsManager
{
    private readonly LocalSecretsOptions _options;
    private readonly ILogger<LocalSecretsManager> _logger;
    private readonly ConcurrentDictionary<string, SecretEntry> _cache;
    private readonly string _secretsFilePath;
    private readonly object _fileLock = new();

    public LocalSecretsManager(
        IOptions<LocalSecretsOptions> options,
        ILogger<LocalSecretsManager> logger)
    {
        _options = options.Value;
        _logger = logger;
        _cache = new ConcurrentDictionary<string, SecretEntry>();

        var baseDir = _options.SecretsDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vendorea.PartnerConnect");

        Directory.CreateDirectory(baseDir);
        _secretsFilePath = Path.Combine(baseDir, _options.SecretsFileName);

        LoadSecrets();
    }

    public Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(secretName, out var entry))
        {
            if (entry.Metadata.ExpiresAt.HasValue && entry.Metadata.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Secret {SecretName} has expired", secretName);
                return Task.FromResult<string?>(null);
            }

            if (!entry.Metadata.IsEnabled)
            {
                _logger.LogWarning("Secret {SecretName} is disabled", secretName);
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(entry.Value);
        }

        return Task.FromResult<string?>(null);
    }

    public Task<string?> GetSecretAsync(string secretName, string version, CancellationToken cancellationToken = default)
    {
        // Local implementation doesn't support versioning
        return GetSecretAsync(secretName, cancellationToken);
    }

    public Task SetSecretAsync(string secretName, string value, CancellationToken cancellationToken = default)
    {
        return SetSecretAsync(secretName, value, new SecretMetadata { Name = secretName }, cancellationToken);
    }

    public Task SetSecretAsync(string secretName, string value, SecretMetadata metadata, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var entry = new SecretEntry
        {
            Value = value,
            Metadata = new SecretMetadata
            {
                Name = secretName,
                CreatedAt = metadata.CreatedAt ?? now,
                UpdatedAt = now,
                ExpiresAt = metadata.ExpiresAt,
                IsEnabled = metadata.IsEnabled,
                ContentType = metadata.ContentType,
                Tags = metadata.Tags ?? new Dictionary<string, string>()
            }
        };

        _cache[secretName] = entry;
        SaveSecrets();

        _logger.LogInformation("Secret {SecretName} saved", secretName);
        return Task.CompletedTask;
    }

    public Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (_cache.TryRemove(secretName, out _))
        {
            SaveSecrets();
            _logger.LogInformation("Secret {SecretName} deleted", secretName);
        }

        return Task.CompletedTask;
    }

    public Task<bool> SecretExistsAsync(string secretName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cache.ContainsKey(secretName));
    }

    public Task<IReadOnlyList<string>> ListSecretsAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var names = _cache.Keys.AsEnumerable();

        if (!string.IsNullOrEmpty(prefix))
        {
            names = names.Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<string>>(names.OrderBy(n => n).ToList());
    }

    public Task<SecretMetadata?> GetSecretMetadataAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(secretName, out var entry))
        {
            return Task.FromResult<SecretMetadata?>(entry.Metadata);
        }

        return Task.FromResult<SecretMetadata?>(null);
    }

    private void LoadSecrets()
    {
        lock (_fileLock)
        {
            if (!File.Exists(_secretsFilePath))
            {
                return;
            }

            try
            {
                var content = File.ReadAllText(_secretsFilePath);

                if (_options.EncryptAtRest)
                {
                    content = Decrypt(content);
                }

                var entries = JsonSerializer.Deserialize<Dictionary<string, SecretEntry>>(content);
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        _cache[entry.Key] = entry.Value;
                    }
                }

                _logger.LogInformation("Loaded {Count} secrets from local storage", _cache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load secrets from local storage");
            }
        }
    }

    private void SaveSecrets()
    {
        lock (_fileLock)
        {
            try
            {
                var content = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });

                if (_options.EncryptAtRest)
                {
                    content = Encrypt(content);
                }

                File.WriteAllText(_secretsFilePath, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save secrets to local storage");
            }
        }
    }

    private string Encrypt(string plainText)
    {
        var key = GetEncryptionKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to encrypted data
        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    private string Decrypt(string encryptedText)
    {
        var key = GetEncryptionKey();
        var encryptedData = Convert.FromBase64String(encryptedText);

        using var aes = Aes.Create();
        aes.Key = key;

        // Extract IV from encrypted data
        var iv = new byte[aes.BlockSize / 8];
        var cipherBytes = new byte[encryptedData.Length - iv.Length];

        Buffer.BlockCopy(encryptedData, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(encryptedData, iv.Length, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    private byte[] GetEncryptionKey()
    {
        if (!string.IsNullOrEmpty(_options.EncryptionKey))
        {
            // Use provided key, hash to ensure correct length
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(_options.EncryptionKey));
        }

        // Generate machine-specific key
        var machineId = Environment.MachineName + Environment.UserName + "Vendorea.PartnerConnect";
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId));
    }

    private class SecretEntry
    {
        public string Value { get; set; } = string.Empty;
        public SecretMetadata Metadata { get; set; } = new();
    }
}
