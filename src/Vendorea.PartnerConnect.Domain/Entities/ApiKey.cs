namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents an API key for external access.
/// </summary>
public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The dealer ID this key belongs to.
    /// </summary>
    public int DealerId { get; set; }

    /// <summary>
    /// Display name for the API key.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The hashed API key value.
    /// </summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Prefix of the key for identification (first 8 chars).
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Scopes/permissions granted to this key.
    /// </summary>
    public IList<string> Scopes { get; set; } = new List<string>();

    /// <summary>
    /// Whether the key is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the key expires (null = never).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When the key was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who created the key.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// When the key was last used.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// IP address that last used this key.
    /// </summary>
    public string? LastUsedIp { get; set; }

    /// <summary>
    /// Number of times this key has been used.
    /// </summary>
    public long UsageCount { get; set; }

    /// <summary>
    /// When the key was revoked.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Why the key was revoked.
    /// </summary>
    public string? RevocationReason { get; set; }

    /// <summary>
    /// Rate limit (requests per minute). Null = use default.
    /// </summary>
    public int? RateLimitPerMinute { get; set; }

    /// <summary>
    /// Allowed IP addresses (CIDR notation). Empty = all allowed.
    /// </summary>
    public IList<string> AllowedIps { get; set; } = new List<string>();

    /// <summary>
    /// Metadata as JSON.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Checks if the key is valid for use.
    /// </summary>
    public bool IsValid()
    {
        if (!IsActive) return false;
        if (RevokedAt.HasValue) return false;
        if (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow) return false;
        return true;
    }

    /// <summary>
    /// Checks if the key has a specific scope.
    /// </summary>
    public bool HasScope(string scope)
    {
        return Scopes.Any(s => s.Equals(scope, StringComparison.OrdinalIgnoreCase)
            || s.Equals("*", StringComparison.Ordinal));
    }

    /// <summary>
    /// Records a usage of this key.
    /// </summary>
    public void RecordUsage(string? ipAddress = null)
    {
        LastUsedAt = DateTime.UtcNow;
        LastUsedIp = ipAddress;
        UsageCount++;
    }
}

/// <summary>
/// Known API scopes.
/// </summary>
public static class ApiScopes
{
    // Document operations
    public const string DocumentsRead = "documents:read";
    public const string DocumentsWrite = "documents:write";

    // Connection operations
    public const string ConnectionsRead = "connections:read";
    public const string ConnectionsWrite = "connections:write";

    // Webhook operations
    public const string WebhooksRead = "webhooks:read";
    public const string WebhooksWrite = "webhooks:write";

    // Usage/billing operations
    public const string UsageRead = "usage:read";

    // Admin operations
    public const string Admin = "admin";

    // Full access
    public const string All = "*";

    public static readonly IReadOnlyList<string> AllScopes = new[]
    {
        DocumentsRead, DocumentsWrite,
        ConnectionsRead, ConnectionsWrite,
        WebhooksRead, WebhooksWrite,
        UsageRead, Admin, All
    };
}
