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
