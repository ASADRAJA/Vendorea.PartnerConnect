namespace Vendorea.PartnerConnect.Storage.Models;

/// <summary>
/// Metadata associated with a stored document.
/// </summary>
public record StorageMetadata
{
    /// <summary>
    /// Unique identifier for this stored document.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Original filename.
    /// </summary>
    public string? OriginalFileName { get; init; }

    /// <summary>
    /// Content type (MIME type).
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// SHA-256 hash of the content.
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// Dealer ID that owns this document.
    /// </summary>
    public int DealerId { get; init; }

    /// <summary>
    /// Trading partner code.
    /// </summary>
    public string? TradingPartnerCode { get; init; }

    /// <summary>
    /// Document type (e.g., "PriceList", "InventoryFeed").
    /// </summary>
    public string? DocumentType { get; init; }

    /// <summary>
    /// Reference to the PartnerDocument entity ID.
    /// </summary>
    public int? PartnerDocumentId { get; init; }

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// When the document was stored.
    /// </summary>
    public DateTime StoredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the document expires (for cleanup).
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Custom tags for organization.
    /// </summary>
    public IDictionary<string, string>? Tags { get; init; }
}

/// <summary>
/// Represents a stored document with its metadata and path.
/// </summary>
public record StoredDocument
{
    /// <summary>
    /// The storage path where the document is stored.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// The metadata associated with this document.
    /// </summary>
    public StorageMetadata Metadata { get; init; } = new();

    /// <summary>
    /// Whether the document exists.
    /// </summary>
    public bool Exists { get; init; }
}
