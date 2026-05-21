using Vendorea.PartnerConnect.WorkerProcesses.Configuration;

namespace Vendorea.PartnerConnect.WorkerProcesses.Services;

/// <summary>
/// Service for bulk importing SPR CSV files into the raw schema tables.
/// </summary>
public interface ISprCsvBulkImportService
{
    /// <summary>
    /// Imports all downloaded files into the raw schema.
    /// Performs truncate + insert for full replacement.
    /// </summary>
    Task<BulkImportResult> ImportAllAsync(
        IReadOnlyList<DownloadedFileInfo> downloadedFiles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a single CSV file into its target table.
    /// </summary>
    Task<TableImportResult> ImportCsvFileAsync(
        string csvFilePath,
        string targetTable,
        char delimiter = ',',
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Truncates all raw schema tables in preparation for a full import.
    /// </summary>
    Task TruncateAllTablesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a bulk import operation.
/// </summary>
public class BulkImportResult
{
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;
    public List<TableImportResult> TableResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public int TotalTablesProcessed => TableResults.Count;
    public int TablesSucceeded => TableResults.Count(r => r.Success);
    public int TablesFailed => TableResults.Count(r => !r.Success);
    public long TotalRowsInserted => TableResults.Sum(r => r.RowsInserted);
}

/// <summary>
/// Result of importing a single table.
/// </summary>
public class TableImportResult
{
    public string TableName { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long RowsInserted { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}
