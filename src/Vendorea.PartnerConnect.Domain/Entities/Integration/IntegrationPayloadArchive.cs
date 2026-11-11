namespace Vendorea.PartnerConnect.Domain.Entities.Integration;

/// <summary>
/// A redacted copy of what a provider actually sent, kept for a bounded time.
/// </summary>
/// <remarks>
/// Separate from <see cref="RawDocumentArchive"/>, which could not be reused. That table is bound
/// one-to-one to <c>PartnerDocument</c> by a non-nullable key with a unique index — it archives
/// SPR EDI and XML documents, and holding an Amazon order in it would mean either inventing
/// PartnerDocument rows for payloads that are not documents or relaxing the invariant that keeps
/// the SPR pipeline honest. Neither is worth doing to avoid a table.
///
/// The purpose is diagnosis: when an order maps oddly, or a status arrives that we do not
/// recognise, the question is always "what did Amazon actually send?". That is answerable for a
/// while and then it is not, which is the point — this is a debugging aid with an expiry date,
/// never a second system of record.
///
/// <see cref="Content"/> is redacted **before** it is written. The archive is therefore never a
/// second copy of anyone's personal data: buyer identity and street address live on the
/// <c>SalesOrder</c> row, where retention governs them explicitly, and the fields that were
/// removed are listed in <see cref="RedactedPaths"/> so a diagnostician can tell "we took this
/// out" from "the provider never sent it".
/// </remarks>
public class IntegrationPayloadArchive
{
    public Guid Id { get; set; }

    public Guid IntegrationConnectionId { get; set; }

    /// <summary>Owning tenant, denormalised so every read can be tenant-scoped without a join.</summary>
    public int TenantId { get; set; }

    public IntegrationPayloadKind PayloadKind { get; set; }

    /// <summary>
    /// What the payload is about — an order id, a notification id. Indexed, because the question
    /// is nearly always about one specific thing rather than a time range.
    /// </summary>
    public string? ExternalReference { get; set; }

    /// <summary>Ties the capture to the request that produced it.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// SHA-256 of <see cref="Content"/>, lowercase hex.
    /// </summary>
    /// <remarks>
    /// Used to skip writing a payload identical to the one already stored for this reference.
    /// Polling deliberately re-reads an overlap window on every cycle, so without this the
    /// archive would accumulate near-identical copies of unchanged orders far faster than it
    /// accumulated anything useful.
    /// </remarks>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>The redacted payload, exactly as written — not re-serialised on read.</summary>
    public string Content { get; set; } = string.Empty;

    public int SizeBytes { get; set; }

    /// <summary>
    /// JSON paths whose values were removed, as a JSON array.
    /// </summary>
    /// <remarks>
    /// Recorded rather than inferred. "The address is missing" and "we redacted the address" look
    /// identical in the stored document and mean entirely different things when you are working
    /// out why an order would not ship.
    /// </remarks>
    public string? RedactedPaths { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this row becomes eligible for purging. Set on write from policy, never null.
    /// </summary>
    /// <remarks>
    /// Non-nullable on purpose. A nullable expiry is how an archive becomes permanent: one code
    /// path forgets to set it and those rows are never collected, with nothing to show that
    /// anything is wrong.
    /// </remarks>
    public DateTime ExpiresAt { get; set; }

    public IntegrationConnection? Connection { get; set; }
}

/// <summary>What kind of exchange a payload came from.</summary>
public enum IntegrationPayloadKind
{
    /// <summary>One order as returned by a search or a direct read.</summary>
    OrderSnapshot,

    /// <summary>A change notification received out of band.</summary>
    OrderNotification,

    /// <summary>A fulfilment confirmation we sent.</summary>
    FulfillmentRequest,

    /// <summary>The provider's response to a fulfilment confirmation.</summary>
    FulfillmentResponse,

    /// <summary>A provider error body worth keeping while a problem is open.</summary>
    ProviderError
}
