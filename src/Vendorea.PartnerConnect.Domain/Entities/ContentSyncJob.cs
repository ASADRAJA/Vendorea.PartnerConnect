namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Tracks product content synchronization jobs from trading partners.
/// Content includes product descriptions, images, specifications, etc.
/// </summary>
public class ContentSyncJob
{
    public int Id { get; set; }
    public int DealerId { get; set; }
    public int TradingPartnerId { get; set; }
    public ContentSyncType SyncType { get; set; }
    public ContentSyncStatus Status { get; set; }
    public int TotalProducts { get; set; }
    public int ProcessedProducts { get; set; }
    public int UpdatedProducts { get; set; }
    public int NewImagesDownloaded { get; set; }
    public int SkippedProducts { get; set; }
    public int ErrorProducts { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorDetails { get; set; }
    public string? TriggerSource { get; set; }
}

public enum ContentSyncType
{
    Full,
    Incremental,
    SelectedProducts,
    NewProductsOnly
}

public enum ContentSyncStatus
{
    Scheduled,
    Running,
    Completed,
    PartiallyCompleted,
    Failed,
    Cancelled
}
