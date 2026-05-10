using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service for managing API keys.
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Creates a new API key.
    /// </summary>
    Task<ApiKeyCreationResult> CreateAsync(
        int dealerId,
        string name,
        IEnumerable<string> scopes,
        DateTime? expiresAt = null,
        IEnumerable<string>? allowedIps = null,
        string? createdBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an API key and returns its details.
    /// </summary>
    Task<ApiKeyValidationResult> ValidateAsync(
        string apiKey,
        string? clientIp = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an API key by ID.
    /// </summary>
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all API keys for a dealer.
    /// </summary>
    Task<IReadOnlyList<ApiKey>> GetByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    Task<bool> RevokeAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Regenerates an API key (creates new key with same settings).
    /// </summary>
    Task<ApiKeyCreationResult?> RegenerateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates API key settings.
    /// </summary>
    Task<ApiKey?> UpdateAsync(
        Guid id,
        string? name = null,
        IEnumerable<string>? scopes = null,
        DateTime? expiresAt = null,
        IEnumerable<string>? allowedIps = null,
        int? rateLimitPerMinute = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an API key.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of API key service.
/// </summary>
public class ApiKeyService : IApiKeyService
{
    private readonly IApiKeyRepository _repository;
    private readonly ILogger<ApiKeyService> _logger;
    private const int KeyLength = 32; // 256 bits

    public ApiKeyService(
        IApiKeyRepository repository,
        ILogger<ApiKeyService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiKeyCreationResult> CreateAsync(
        int dealerId,
        string name,
        IEnumerable<string> scopes,
        DateTime? expiresAt = null,
        IEnumerable<string>? allowedIps = null,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        // Generate a secure random key
        var rawKey = GenerateSecureKey();
        var keyHash = HashKey(rawKey);
        var keyPrefix = rawKey[..8];

        var apiKey = new ApiKey
        {
            DealerId = dealerId,
            Name = name,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Scopes = scopes.ToList(),
            ExpiresAt = expiresAt,
            AllowedIps = allowedIps?.ToList() ?? new List<string>(),
            CreatedBy = createdBy,
            IsActive = true
        };

        await _repository.AddAsync(apiKey, cancellationToken);

        _logger.LogInformation(
            "Created API key {KeyId} ({KeyPrefix}...) for dealer {DealerId}",
            apiKey.Id, keyPrefix, dealerId);

        return new ApiKeyCreationResult
        {
            Id = apiKey.Id,
            Key = rawKey, // Only returned once
            KeyPrefix = keyPrefix,
            DealerId = dealerId,
            Name = name,
            Scopes = apiKey.Scopes.ToList(),
            ExpiresAt = expiresAt,
            CreatedAt = apiKey.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task<ApiKeyValidationResult> ValidateAsync(
        string apiKey,
        string? clientIp = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ApiKeyValidationResult.Invalid("API key is required");
        }

        var keyHash = HashKey(apiKey);
        var storedKey = await _repository.GetByKeyHashAsync(keyHash, cancellationToken);

        if (storedKey == null)
        {
            return ApiKeyValidationResult.Invalid("Invalid API key");
        }

        if (!storedKey.IsValid())
        {
            if (storedKey.RevokedAt.HasValue)
            {
                return ApiKeyValidationResult.Invalid("API key has been revoked");
            }
            if (!storedKey.IsActive)
            {
                return ApiKeyValidationResult.Invalid("API key is inactive");
            }
            if (storedKey.ExpiresAt.HasValue && storedKey.ExpiresAt.Value < DateTime.UtcNow)
            {
                return ApiKeyValidationResult.Invalid("API key has expired");
            }
        }

        // Validate IP if restrictions are set
        if (storedKey.AllowedIps.Count > 0 && !string.IsNullOrEmpty(clientIp))
        {
            if (!IsIpAllowed(clientIp, storedKey.AllowedIps))
            {
                _logger.LogWarning(
                    "API key {KeyId} used from unauthorized IP {ClientIp}",
                    storedKey.Id, clientIp);
                return ApiKeyValidationResult.Invalid("IP address not allowed");
            }
        }

        // Record usage
        storedKey.RecordUsage(clientIp);
        await _repository.UpdateAsync(storedKey, cancellationToken);

        return ApiKeyValidationResult.Valid(storedKey);
    }

    /// <inheritdoc />
    public Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ApiKey>> GetByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByDealerIdAsync(dealerId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default)
    {
        var apiKey = await _repository.GetByIdAsync(id, cancellationToken);
        if (apiKey == null) return false;

        apiKey.RevokedAt = DateTime.UtcNow;
        apiKey.RevocationReason = reason;
        apiKey.IsActive = false;

        await _repository.UpdateAsync(apiKey, cancellationToken);

        _logger.LogInformation(
            "Revoked API key {KeyId} ({KeyPrefix}...): {Reason}",
            id, apiKey.KeyPrefix, reason ?? "No reason provided");

        return true;
    }

    /// <inheritdoc />
    public async Task<ApiKeyCreationResult?> RegenerateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var oldKey = await _repository.GetByIdAsync(id, cancellationToken);
        if (oldKey == null) return null;

        // Revoke the old key
        await RevokeAsync(id, "Regenerated", cancellationToken);

        // Create a new key with the same settings
        return await CreateAsync(
            oldKey.DealerId,
            oldKey.Name,
            oldKey.Scopes,
            oldKey.ExpiresAt,
            oldKey.AllowedIps,
            oldKey.CreatedBy,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiKey?> UpdateAsync(
        Guid id,
        string? name = null,
        IEnumerable<string>? scopes = null,
        DateTime? expiresAt = null,
        IEnumerable<string>? allowedIps = null,
        int? rateLimitPerMinute = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await _repository.GetByIdAsync(id, cancellationToken);
        if (apiKey == null) return null;

        if (name != null) apiKey.Name = name;
        if (scopes != null) apiKey.Scopes = scopes.ToList();
        if (expiresAt.HasValue) apiKey.ExpiresAt = expiresAt;
        if (allowedIps != null) apiKey.AllowedIps = allowedIps.ToList();
        if (rateLimitPerMinute.HasValue) apiKey.RateLimitPerMinute = rateLimitPerMinute;

        await _repository.UpdateAsync(apiKey, cancellationToken);

        _logger.LogInformation("Updated API key {KeyId}", id);

        return apiKey;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            _logger.LogInformation("Deleted API key {KeyId}", id);
        }

        return deleted;
    }

    private static string GenerateSecureKey()
    {
        var bytes = new byte[KeyLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string HashKey(string key)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsIpAllowed(string clientIp, IList<string> allowedIps)
    {
        // Simple exact match for now - could add CIDR support later
        return allowedIps.Contains(clientIp, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Result of creating an API key.
/// </summary>
public record ApiKeyCreationResult
{
    public Guid Id { get; init; }
    public required string Key { get; init; }  // Only available once
    public required string KeyPrefix { get; init; }
    public int DealerId { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();
    public DateTime? ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Result of validating an API key.
/// </summary>
public record ApiKeyValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public ApiKey? ApiKey { get; init; }

    public static ApiKeyValidationResult Valid(ApiKey apiKey) =>
        new() { IsValid = true, ApiKey = apiKey };

    public static ApiKeyValidationResult Invalid(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}
