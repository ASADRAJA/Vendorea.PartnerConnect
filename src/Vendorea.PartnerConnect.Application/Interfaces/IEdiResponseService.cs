using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service interface for generating and sending EDI response documents (997, 855).
/// </summary>
public interface IEdiResponseService
{
    /// <summary>
    /// Generates a 997 Functional Acknowledgment for an EDI document.
    /// </summary>
    /// <param name="ediDocumentId">The EDI document ID to acknowledge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated 997 EDI content and response document ID.</returns>
    Task<EdiResponseResult> Generate997Async(
        int ediDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an 855 Purchase Order Acknowledgment for an 850 document.
    /// </summary>
    /// <param name="ediDocumentId">The 850 EDI document ID to acknowledge.</param>
    /// <param name="options">Options for the acknowledgment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated 855 EDI content and response document ID.</returns>
    Task<EdiResponseResult> Generate855Async(
        int ediDocumentId,
        Edi855Options? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a response document to the trading partner via SFTP.
    /// </summary>
    /// <param name="responseDocumentId">The response EDI document ID to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Send result with success status.</returns>
    Task<EdiSendResult> SendResponseAsync(
        int responseDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending response documents that need to be sent.
    /// </summary>
    /// <param name="connectionId">Optional filter by connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of pending response documents.</returns>
    Task<IReadOnlyList<EdiDocument>> GetPendingResponsesAsync(
        int? connectionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends all pending responses for a connection.
    /// </summary>
    /// <param name="connectionId">The dealer-partner connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch send result.</returns>
    Task<EdiBatchSendResult> SendPendingResponsesAsync(
        int connectionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of generating an EDI response document.
/// </summary>
public class EdiResponseResult
{
    /// <summary>
    /// Whether generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The generated response document ID.
    /// </summary>
    public int? ResponseDocumentId { get; set; }

    /// <summary>
    /// The generated EDI content.
    /// </summary>
    public string? EdiContent { get; set; }

    /// <summary>
    /// The transaction set code of the response (997 or 855).
    /// </summary>
    public string? TransactionSetCode { get; set; }

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Options for generating 855 Purchase Order Acknowledgment.
/// </summary>
public class Edi855Options
{
    /// <summary>
    /// Default acknowledgment status for all line items.
    /// </summary>
    public LineAcknowledgmentStatus DefaultLineStatus { get; set; } = LineAcknowledgmentStatus.Accepted;

    /// <summary>
    /// Line-specific acknowledgment overrides.
    /// Key is the line number from the original PO.
    /// </summary>
    public Dictionary<int, LineAcknowledgmentInfo>? LineOverrides { get; set; }

    /// <summary>
    /// Vendor order number to include in the acknowledgment.
    /// </summary>
    public string? VendorOrderNumber { get; set; }

    /// <summary>
    /// Estimated ship date.
    /// </summary>
    public DateTime? EstimatedShipDate { get; set; }

    /// <summary>
    /// Estimated delivery date.
    /// </summary>
    public DateTime? EstimatedDeliveryDate { get; set; }

    /// <summary>
    /// Notes or comments to include.
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Line item acknowledgment status.
/// </summary>
public enum LineAcknowledgmentStatus
{
    /// <summary>
    /// Line item is accepted as ordered.
    /// </summary>
    Accepted,

    /// <summary>
    /// Line item is accepted with changes.
    /// </summary>
    AcceptedWithChanges,

    /// <summary>
    /// Line item is backordered.
    /// </summary>
    Backordered,

    /// <summary>
    /// Line item is rejected.
    /// </summary>
    Rejected
}

/// <summary>
/// Line-specific acknowledgment information.
/// </summary>
public class LineAcknowledgmentInfo
{
    /// <summary>
    /// Acknowledgment status for this line.
    /// </summary>
    public LineAcknowledgmentStatus Status { get; set; }

    /// <summary>
    /// Acknowledged quantity (if different from ordered).
    /// </summary>
    public decimal? AcknowledgedQuantity { get; set; }

    /// <summary>
    /// Acknowledged unit price (if different from ordered).
    /// </summary>
    public decimal? AcknowledgedPrice { get; set; }

    /// <summary>
    /// Estimated ship date for this line.
    /// </summary>
    public DateTime? EstimatedShipDate { get; set; }

    /// <summary>
    /// Reason code for changes or rejection.
    /// </summary>
    public string? ReasonCode { get; set; }
}

/// <summary>
/// Result of sending an EDI document.
/// </summary>
public class EdiSendResult
{
    /// <summary>
    /// Whether the send was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The file name used on the SFTP server.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// The remote path where the file was uploaded.
    /// </summary>
    public string? RemotePath { get; set; }

    /// <summary>
    /// When the file was sent.
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Error message if send failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of sending multiple EDI documents.
/// </summary>
public class EdiBatchSendResult
{
    /// <summary>
    /// Whether all sends were successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of documents sent successfully.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of documents that failed to send.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Individual send results.
    /// </summary>
    public List<EdiSendResult> Results { get; set; } = new();

    /// <summary>
    /// Error message if the entire batch failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
