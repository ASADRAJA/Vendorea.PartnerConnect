namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Tracks idempotency keys for document processing.
/// Prevents duplicate processing of the same document.
/// </summary>
public class DocumentIdempotencyKey
{
    public int Id { get; set; }

    /// <summary>
    /// The idempotency key value (hash or unique identifier).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Type of key (ContentHash, ControlNumber, ExternalId).
    /// </summary>
    public IdempotencyKeyType KeyType { get; set; }

    /// <summary>
    /// Trading partner this key is scoped to.
    /// </summary>
    public int TradingPartnerId { get; set; }

    /// <summary>
    /// Connection this key is scoped to (if applicable).
    /// </summary>
    public int? DealerPartnerConnectionId { get; set; }

    /// <summary>
    /// Document type this key applies to.
    /// </summary>
    public DocumentType DocumentType { get; set; }

    /// <summary>
    /// The document that was processed for this key.
    /// </summary>
    public int PartnerDocumentId { get; set; }

    /// <summary>
    /// When this key was first seen.
    /// </summary>
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this key was last seen (for duplicate tracking).
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this key has been seen.
    /// </summary>
    public int SeenCount { get; set; } = 1;

    /// <summary>
    /// When this key expires and can be reused.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public TradingPartner? TradingPartner { get; set; }
    public DealerPartnerConnection? Connection { get; set; }
    public PartnerDocument? PartnerDocument { get; set; }
}

/// <summary>
/// Type of idempotency key.
/// </summary>
public enum IdempotencyKeyType
{
    /// <summary>Key based on content hash.</summary>
    ContentHash = 0,

    /// <summary>Key based on EDI control number.</summary>
    ControlNumber = 10,

    /// <summary>Key based on external document ID.</summary>
    ExternalId = 20,

    /// <summary>Key based on business reference (PO number, invoice number).</summary>
    BusinessReference = 30,

    /// <summary>Composite key from multiple fields.</summary>
    Composite = 40
}
