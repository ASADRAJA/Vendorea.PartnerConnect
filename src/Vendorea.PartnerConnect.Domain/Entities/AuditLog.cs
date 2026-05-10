namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents an audit log entry for tracking changes.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The type of action performed (Create, Update, Delete, Read).
    /// </summary>
    public AuditAction Action { get; set; }

    /// <summary>
    /// The entity type that was affected.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the entity that was affected.
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// The user or service that performed the action.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// User-friendly name of who performed the action.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// The IP address of the user.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// The user agent string.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// The original values before the change (JSON).
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// The new values after the change (JSON).
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// The properties that were changed.
    /// </summary>
    public string? ChangedProperties { get; set; }

    /// <summary>
    /// When the action occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Correlation ID for tracing related actions.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// The dealer ID associated with this action (if applicable).
    /// </summary>
    public int? DealerId { get; set; }

    /// <summary>
    /// Additional context or notes.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// The request path that triggered the action.
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// The HTTP method used.
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Duration of the operation in milliseconds.
    /// </summary>
    public int? DurationMs { get; set; }

    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Types of audit actions.
/// </summary>
public enum AuditAction
{
    /// <summary>
    /// Entity was created.
    /// </summary>
    Create,

    /// <summary>
    /// Entity was updated.
    /// </summary>
    Update,

    /// <summary>
    /// Entity was deleted.
    /// </summary>
    Delete,

    /// <summary>
    /// Entity was read/accessed.
    /// </summary>
    Read,

    /// <summary>
    /// An operation was executed.
    /// </summary>
    Execute,

    /// <summary>
    /// User logged in.
    /// </summary>
    Login,

    /// <summary>
    /// User logged out.
    /// </summary>
    Logout,

    /// <summary>
    /// Access was denied.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Configuration was changed.
    /// </summary>
    ConfigChange
}
