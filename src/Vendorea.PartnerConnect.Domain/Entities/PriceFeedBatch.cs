namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Represents a batch of price updates received from a trading partner.
/// </summary>
public class PriceFeedBatch
{
    public int Id { get; set; }
    /// <summary>
    /// The document this batch came from. Null when the run produced none — a fetch that failed,
    /// or a feed that was empty — both of which are still worth recording as a batch.
    /// </summary>
    public int? PartnerDocumentId { get; set; }
    public int DealerId { get; set; }
    public int TradingPartnerId { get; set; }
    public FeedBatchStatus Status { get; set; }
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int MatchedItems { get; set; }
    public int UpdatedItems { get; set; }
    public int SkippedItems { get; set; }
    public int ErrorItems { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public string? ErrorSummary { get; set; }

    public PartnerDocument? PartnerDocument { get; set; }
    /// <summary>
    /// True when this batch records a poll that found no file, so there is nothing worth keeping.
    /// </summary>
    /// <remarks>
    /// The feed worker polls on a timer whether or not the partner has delivered anything. A poll
    /// that finds no file is not a batch — it completed, it processed nothing, and no document was
    /// ever created. Persisting one produced 21,801 identical empty rows on the test environment,
    /// one every five minutes, which is table growth and would have buried the real batches the
    /// first time SPR delivered a file.
    ///
    /// A **failed** poll is deliberately not covered by this: a failure is a real event and worth
    /// a row. Only "there was nothing there" is discarded.
    /// </remarks>
    public bool RecordsNoFile =>
        Status == FeedBatchStatus.Completed
        && TotalItems == 0
        && PartnerDocumentId is null;
}

public enum FeedBatchStatus
{
    Received,
    Validating,
    Processing,
    Completed,
    PartiallyCompleted,
    Failed
}
