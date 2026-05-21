namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// Tracks the history of FTP content ingestion runs.
/// </summary>
public class FtpIngestionRun
{
    public int Id { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public bool Success { get; set; }

    /// <summary>
    /// How the run was triggered (Manual, Scheduled).
    /// </summary>
    public string TriggeredBy { get; set; } = "Manual";

    // Download stats
    public int FilesDownloaded { get; set; }
    public long BytesDownloaded { get; set; }

    // Import stats
    public int TablesImported { get; set; }
    public long RowsImported { get; set; }

    // Transform stats
    public int ProductsTransformed { get; set; }
    public int CategoriesTransformed { get; set; }
    public int FeaturesTransformed { get; set; }
    public int RelationshipsTransformed { get; set; }
    public int SpecificationsTransformed { get; set; }

    /// <summary>
    /// List of errors encountered during the run.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Duration of the run.
    /// </summary>
    public TimeSpan Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : TimeSpan.Zero;
}
