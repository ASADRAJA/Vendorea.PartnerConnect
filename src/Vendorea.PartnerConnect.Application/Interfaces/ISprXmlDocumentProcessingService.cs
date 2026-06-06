using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service interface for processing SPR XML EDI documents.
/// Handles parsing, validation, transformation, and response generation.
/// </summary>
public interface ISprXmlDocumentProcessingService
{
    /// <summary>
    /// Processes an inbound SPR XML document.
    /// </summary>
    /// <param name="connectionId">The dealer-partner connection ID.</param>
    /// <param name="xmlContent">Raw XML content.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="documentType">Expected document type (if known).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result.</returns>
    Task<SprXmlProcessingResult> ProcessInboundDocumentAsync(
        int connectionId,
        string xmlContent,
        string fileName,
        SprXmlDocumentType? documentType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and queues an outbound purchase order for SPR.
    /// </summary>
    /// <param name="connectionId">The dealer-partner connection ID.</param>
    /// <param name="order">The canonical purchase order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with the created document.</returns>
    Task<SprXmlProcessingResult> CreateOutboundOrderAsync(
        int connectionId,
        PurchaseOrder order,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a pending outbound document to SPR.
    /// </summary>
    /// <param name="documentId">The SPR XML document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Send result.</returns>
    Task<SprXmlSendResult> SendOutboundDocumentAsync(
        int documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an SPR XML document by ID.
    /// </summary>
    Task<SprXmlDocument?> GetDocumentAsync(
        int documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets SPR XML documents for a connection with optional filtering.
    /// </summary>
    Task<IReadOnlyList<SprXmlDocument>> GetDocumentsAsync(
        int connectionId,
        SprXmlDocumentType? documentType = null,
        EdiDirection? direction = null,
        SprXmlProcessingStatus? status = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Links an inbound acknowledgment to its original outbound document.
    /// </summary>
    Task LinkAcknowledgmentAsync(
        int ackDocumentId,
        int originalDocumentId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of processing an SPR XML document.
/// </summary>
public class SprXmlProcessingResult
{
    public bool Success { get; set; }
    public int? SprXmlDocumentId { get; set; }
    public int? PartnerDocumentId { get; set; }
    public SprXmlDocumentType? DocumentType { get; set; }
    public string? CanonicalType { get; set; }
    public string? BusinessReference { get; set; }
    public int LineItemCount { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public long ProcessingDurationMs { get; set; }
}

/// <summary>
/// Result of sending an outbound SPR XML document.
/// </summary>
public class SprXmlSendResult
{
    public bool Success { get; set; }
    public int? DocumentId { get; set; }
    public string? PartnerOrderNumber { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
}
