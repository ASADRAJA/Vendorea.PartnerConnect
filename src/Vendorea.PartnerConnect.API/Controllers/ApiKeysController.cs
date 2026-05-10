using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers;

/// <summary>
/// Controller for managing API keys.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<ApiKeysController> _logger;

    public ApiKeysController(
        IApiKeyService apiKeyService,
        ILogger<ApiKeysController> logger)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new API key.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        // Validate scopes
        var invalidScopes = request.Scopes.Where(s => !ApiScopes.AllScopes.Contains(s)).ToList();
        if (invalidScopes.Count > 0)
        {
            return BadRequest($"Invalid scopes: {string.Join(", ", invalidScopes)}");
        }

        var result = await _apiKeyService.CreateAsync(
            request.DealerId,
            request.Name,
            request.Scopes,
            request.ExpiresAt,
            request.AllowedIps,
            User.Identity?.Name,
            cancellationToken);

        // Return the key - this is the ONLY time it will be shown
        return CreatedAtAction(nameof(Get), new { id = result.Id }, new
        {
            result.Id,
            result.Key, // Only shown once
            result.KeyPrefix,
            result.DealerId,
            result.Name,
            result.Scopes,
            result.ExpiresAt,
            result.CreatedAt,
            Warning = "Store this key securely - it will not be shown again"
        });
    }

    /// <summary>
    /// Gets an API key by ID (key value is not returned).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var apiKey = await _apiKeyService.GetByIdAsync(id, cancellationToken);

        if (apiKey == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(apiKey));
    }

    /// <summary>
    /// Gets all API keys for a dealer.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetByDealer(
        [FromQuery] int dealerId,
        CancellationToken cancellationToken)
    {
        var apiKeys = await _apiKeyService.GetByDealerIdAsync(dealerId, cancellationToken);
        return Ok(apiKeys.Select(MapToResponse));
    }

    /// <summary>
    /// Updates an API key.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        // Validate scopes if provided
        if (request.Scopes != null)
        {
            var invalidScopes = request.Scopes.Where(s => !ApiScopes.AllScopes.Contains(s)).ToList();
            if (invalidScopes.Count > 0)
            {
                return BadRequest($"Invalid scopes: {string.Join(", ", invalidScopes)}");
            }
        }

        var apiKey = await _apiKeyService.UpdateAsync(
            id,
            request.Name,
            request.Scopes,
            request.ExpiresAt,
            request.AllowedIps,
            request.RateLimitPerMinute,
            cancellationToken);

        if (apiKey == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(apiKey));
    }

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(
        Guid id,
        [FromBody] RevokeApiKeyRequest? request,
        CancellationToken cancellationToken)
    {
        var revoked = await _apiKeyService.RevokeAsync(id, request?.Reason, cancellationToken);

        if (!revoked)
        {
            return NotFound();
        }

        return Ok(new { message = "API key revoked successfully" });
    }

    /// <summary>
    /// Regenerates an API key (creates new key with same settings).
    /// </summary>
    [HttpPost("{id:guid}/regenerate")]
    public async Task<IActionResult> Regenerate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _apiKeyService.RegenerateAsync(id, cancellationToken);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            result.Id,
            result.Key, // Only shown once
            result.KeyPrefix,
            result.DealerId,
            result.Name,
            result.Scopes,
            result.ExpiresAt,
            result.CreatedAt,
            Warning = "Store this key securely - it will not be shown again"
        });
    }

    /// <summary>
    /// Deletes an API key.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _apiKeyService.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Gets available scopes.
    /// </summary>
    [HttpGet("scopes")]
    public IActionResult GetScopes()
    {
        return Ok(ApiScopes.AllScopes);
    }

    private static ApiKeyResponse MapToResponse(ApiKey apiKey)
    {
        return new ApiKeyResponse
        {
            Id = apiKey.Id,
            KeyPrefix = apiKey.KeyPrefix,
            DealerId = apiKey.DealerId,
            Name = apiKey.Name,
            Scopes = apiKey.Scopes.ToList(),
            IsActive = apiKey.IsActive,
            ExpiresAt = apiKey.ExpiresAt,
            CreatedAt = apiKey.CreatedAt,
            CreatedBy = apiKey.CreatedBy,
            LastUsedAt = apiKey.LastUsedAt,
            UsageCount = apiKey.UsageCount,
            RevokedAt = apiKey.RevokedAt,
            RevocationReason = apiKey.RevocationReason,
            RateLimitPerMinute = apiKey.RateLimitPerMinute,
            AllowedIps = apiKey.AllowedIps.ToList()
        };
    }
}

public record CreateApiKeyRequest
{
    public required int DealerId { get; init; }
    public required string Name { get; init; }
    public required List<string> Scopes { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public List<string>? AllowedIps { get; init; }
}

public record UpdateApiKeyRequest
{
    public string? Name { get; init; }
    public List<string>? Scopes { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public List<string>? AllowedIps { get; init; }
    public int? RateLimitPerMinute { get; init; }
}

public record RevokeApiKeyRequest
{
    public string? Reason { get; init; }
}

public record ApiKeyResponse
{
    public Guid Id { get; init; }
    public required string KeyPrefix { get; init; }
    public int DealerId { get; init; }
    public required string Name { get; init; }
    public required List<string> Scopes { get; init; }
    public bool IsActive { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public long UsageCount { get; init; }
    public DateTime? RevokedAt { get; init; }
    public string? RevocationReason { get; init; }
    public int? RateLimitPerMinute { get; init; }
    public required List<string> AllowedIps { get; init; }
}
