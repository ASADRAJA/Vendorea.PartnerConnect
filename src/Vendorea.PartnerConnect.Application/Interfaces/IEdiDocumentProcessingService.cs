using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service interface for processing EDI X12 documents.
/// Handles parsing, validation, transformation, and response generation.
/// </summary>
public interface IEdiDocumentProcessingService
{
    /// <summary>
    /// Processes a single EDI document from raw content.
    /// </summary>
    /// <param name="connectionId">The dealer-partner connection ID.</param>
    /// <param name="ediContent">Raw EDI X12 content.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result with document details.</returns>
    Task<EdiProcessingResult> ProcessDocumentAsync(
        int connectionId,
        string ediContent,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs EDI documents from SFTP for a connection.
    /// Downloads, processes, and archives documents.
    /// </summary>
    /// <param name="connectionId">The dealer-partner connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sync result with counts of processed documents.</returns>
    Task<EdiSyncResult> SyncEdiDocumentsAsync(
        int connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an EDI document by ID with parsed content.
    /// </summary>
    /// <param name="documentId">The EDI document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The EDI document or null if not found.</returns>
    Task<EdiDocument?> GetDocumentAsync(
        int documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets EDI documents for a connection with optional filtering.
    /// </summary>
    /// <param name="connectionId">The dealer-partner connection ID.</param>
    /// <param name="transactionSetCode">Optional filter by transaction set code (850, 856, etc.).</param>
    /// <param name="direction">Optional filter by direction (Inbound/Outbound).</param>
    /// <param name="skip">Number of records to skip.</param>
    /// <param name="take">Number of records to take.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of EDI documents.</returns>
    Task<IReadOnlyList<EdiDocument>> GetDocumentsAsync(
        int connectionId,
        string? transactionSetCode = null,
        EdiDirection? direction = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of processing a single EDI document.
/// </summary>
public class EdiProcessingResult
{
    /// <summary>
    /// Whether processing was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The created EDI document ID (if successful).
    /// </summary>
    public int? EdiDocumentId { get; set; }

    /// <summary>
    /// The parent partner document ID.
    /// </summary>
    public int? PartnerDocumentId { get; set; }

    /// <summary>
    /// The detected transaction set code (850, 856, 810, etc.).
    /// </summary>
    public string? TransactionSetCode { get; set; }

    /// <summary>
    /// The canonical model type (PurchaseOrder, ShipmentNotice, etc.).
    /// </summary>
    public string? CanonicalType { get; set; }

    /// <summary>
    /// Business reference from the document (PO number, invoice number, etc.).
    /// </summary>
    public string? BusinessReference { get; set; }

    /// <summary>
    /// Number of line items in the document.
    /// </summary>
    public int LineItemCount { get; set; }

    /// <summary>
    /// Total amount if applicable.
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Detailed errors from parsing.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Whether a 997 acknowledgment was generated.
    /// </summary>
    public bool Acknowledgment997Generated { get; set; }

    /// <summary>
    /// Whether an 855 acknowledgment was generated (for 850 only).
    /// </summary>
    public bool Acknowledgment855Generated { get; set; }

    /// <summary>
    /// Processing duration in milliseconds.
    /// </summary>
    public long ProcessingDurationMs { get; set; }
}

/// <summary>
/// Result of syncing EDI documents from SFTP.
/// </summary>
public class EdiSyncResult
{
    /// <summary>
    /// Whether the sync was successful overall.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of files found on SFTP.
    /// </summary>
    public int FilesFound { get; set; }

    /// <summary>
    /// Number of files successfully processed.
    /// </summary>
    public int FilesProcessed { get; set; }

    /// <summary>
    /// Number of files that failed processing.
    /// </summary>
    public int FilesFailed { get; set; }

    /// <summary>
    /// Number of files skipped (e.g., duplicates).
    /// </summary>
    public int FilesSkipped { get; set; }

    /// <summary>
    /// Number of outbound documents sent.
    /// </summary>
    public int OutboundDocumentsSent { get; set; }

    /// <summary>
    /// Individual processing results for each file.
    /// </summary>
    public List<EdiProcessingResult> Results { get; set; } = new();

    /// <summary>
    /// Error message if sync failed entirely.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Sync duration in milliseconds.
    /// </summary>
    public long SyncDurationMs { get; set; }

    /// <summary>
    /// When the sync started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the sync completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
