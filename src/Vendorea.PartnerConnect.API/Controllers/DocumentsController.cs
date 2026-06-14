using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Storage.Interfaces;

namespace Vendorea.PartnerConnect.API.Controllers;

/// <summary>
/// API controller for managing partner documents.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly IDocumentStorage _documentStorage;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IPartnerDocumentRepository documentRepository,
        ITradingPartnerRepository partnerRepository,
        IDocumentStorage documentStorage,
        ILogger<DocumentsController> logger)
    {
        _documentRepository = documentRepository;
        _partnerRepository = partnerRepository;
        _documentStorage = documentStorage;
        _logger = logger;
    }

    /// <summary>
    /// Gets documents for a trading partner.
    /// </summary>
    [HttpGet("partner/{tradingPartnerId:int}")]
    [ProducesResponseType(typeof(IEnumerable<DocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTradingPartner(int tradingPartnerId, CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetByTradingPartnerAsync(tradingPartnerId, cancellationToken);
        var dtos = await MapToDtosAsync(documents, cancellationToken);
        return Ok(dtos);
    }

    /// <summary>
    /// Gets a document by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        return Ok(await MapToDtoAsync(document, cancellationToken));
    }

    /// <summary>
    /// Gets pending documents.
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IEnumerable<DocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetPendingDocumentsAsync(cancellationToken);
        var dtos = await MapToDtosAsync(documents, cancellationToken);
        return Ok(dtos);
    }

    /// <summary>
    /// Downloads the raw document content.
    /// </summary>
    [HttpGet("{id:int}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document is null || string.IsNullOrEmpty(document.StoragePath))
        {
            return NotFound();
        }

        try
        {
            var exists = await _documentStorage.ExistsAsync(document.StoragePath, cancellationToken);
            if (!exists)
            {
                return NotFound(new { Error = "Document file not found in storage" });
            }

            var stream = await _documentStorage.RetrieveAsync(document.StoragePath, cancellationToken);
            var contentType = document.ContentType ?? "application/octet-stream";
            var fileName = document.FileName ?? $"document_{id}";

            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {DocumentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { Error = "Failed to download document" });
        }
    }

    /// <summary>
    /// Replays/reprocesses a document.
    /// </summary>
    [HttpPost("{id:int}/replay")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Replay(int id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        // Only allow replay for completed or failed documents
        if (document.Status != DocumentStatus.Completed && document.Status != DocumentStatus.Failed)
        {
            return BadRequest(new { Error = "Can only replay completed or failed documents" });
        }

        // Reset the document status
        document.Status = DocumentStatus.Queued;
        document.ProcessedCount = 0;
        document.ErrorCount = 0;
        document.ProcessingStartedAt = null;
        document.ProcessingCompletedAt = null;

        await _documentRepository.UpdateAsync(document, cancellationToken);

        _logger.LogInformation("Document {DocumentId} queued for replay", id);

        return Ok(await MapToDtoAsync(document, cancellationToken));
    }

    /// <summary>
    /// Cancels a pending document.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        // Only allow cancel for queued or received documents
        if (document.Status != DocumentStatus.Queued && document.Status != DocumentStatus.Received)
        {
            return BadRequest(new { Error = "Can only cancel queued or received documents" });
        }

        document.Status = DocumentStatus.Failed;
        document.ProcessingCompletedAt = DateTime.UtcNow;

        await _documentRepository.UpdateAsync(document, cancellationToken);

        _logger.LogInformation("Document {DocumentId} cancelled", id);

        return Ok(await MapToDtoAsync(document, cancellationToken));
    }

    private async Task<IReadOnlyList<DocumentDto>> MapToDtosAsync(
        IReadOnlyList<PartnerDocument> documents,
        CancellationToken cancellationToken)
    {
        var result = new List<DocumentDto>();
        foreach (var doc in documents)
        {
            result.Add(await MapToDtoAsync(doc, cancellationToken));
        }
        return result;
    }

    private async Task<DocumentDto> MapToDtoAsync(PartnerDocument document, CancellationToken cancellationToken)
    {
        string? partnerCode = null;

        if (document.TradingPartnerId > 0)
        {
            var partner = await _partnerRepository.GetByIdAsync(document.TradingPartnerId, cancellationToken);
            partnerCode = partner?.Code;
        }

        return new DocumentDto(
            Id: document.Id,
            TradingPartnerId: document.TradingPartnerId,
            TenantId: document.TenantId,
            TradingPartnerCode: partnerCode,
            DocumentType: document.DocumentType,
            Direction: document.Direction,
            Status: document.Status,
            FileName: document.FileName,
            ContentType: document.ContentType,
            FileSizeBytes: document.FileSizeBytes,
            RecordCount: document.RecordCount,
            ProcessedCount: document.ProcessedCount,
            ErrorCount: document.ErrorCount,
            ReceivedAt: document.ReceivedAt,
            ProcessingStartedAt: document.ProcessingStartedAt,
            ProcessingCompletedAt: document.ProcessingCompletedAt,
            SentAt: document.SentAt,
            ErrorMessage: null);
    }
}
