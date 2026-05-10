using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for document operations.
/// </summary>
[ApiController]
[Route("api/admin/documents")]
[Authorize(Policy = "RequireSystemAdmin")]
public class AdminDocumentsController : ControllerBase
{
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly ILogger<AdminDocumentsController> _logger;

    public AdminDocumentsController(
        IPartnerDocumentRepository documentRepository,
        ILogger<AdminDocumentsController> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets documents by connection ID.
    /// </summary>
    [HttpGet("connection/{connectionId:int}")]
    public async Task<IActionResult> GetDocumentsByConnection(
        int connectionId,
        CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetByConnectionIdAsync(connectionId, cancellationToken);

        return Ok(documents.Select(MapDocumentResponse));
    }

    /// <summary>
    /// Gets pending documents.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingDocuments(CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetPendingDocumentsAsync(cancellationToken);

        return Ok(documents.Select(MapDocumentResponse));
    }

    /// <summary>
    /// Gets document by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDocument(int id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document == null)
        {
            return NotFound();
        }

        return Ok(MapDocumentResponse(document));
    }

    /// <summary>
    /// Retries a failed document.
    /// </summary>
    [HttpPost("{id:int}/retry")]
    public async Task<IActionResult> RetryDocument(int id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document == null)
        {
            return NotFound();
        }

        if (document.Status != DocumentStatus.Failed)
        {
            return BadRequest("Only failed documents can be retried");
        }

        document.Status = DocumentStatus.Received;
        await _documentRepository.UpdateAsync(document, cancellationToken);

        _logger.LogInformation("Document {DocumentId} queued for retry", id);

        return Ok(new { Message = "Document queued for retry", DocumentId = id });
    }

    /// <summary>
    /// Updates document status.
    /// </summary>
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateDocumentStatus(
        int id,
        [FromBody] UpdateDocumentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document == null)
        {
            return NotFound();
        }

        if (!Enum.TryParse<DocumentStatus>(request.Status, true, out var newStatus))
        {
            return BadRequest("Invalid status");
        }

        document.Status = newStatus;
        await _documentRepository.UpdateAsync(document, cancellationToken);

        _logger.LogInformation("Document {DocumentId} status updated to {Status}", id, newStatus);

        return Ok(new { DocumentId = id, Status = newStatus.ToString() });
    }

    private static DocumentResponse MapDocumentResponse(PartnerDocument doc)
    {
        return new DocumentResponse
        {
            Id = doc.Id,
            DealerPartnerConnectionId = doc.DealerPartnerConnectionId,
            DocumentType = doc.DocumentType.ToString(),
            Direction = doc.Direction.ToString(),
            Status = doc.Status.ToString(),
            FileName = doc.FileName,
            FileSizeBytes = doc.FileSizeBytes ?? 0,
            ContentHash = doc.ContentHash,
            StoragePath = doc.StoragePath,
            ReceivedAt = doc.ReceivedAt
        };
    }
}

public class UpdateDocumentStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class DocumentResponse
{
    public int Id { get; set; }
    public int DealerPartnerConnectionId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ContentHash { get; set; }
    public string? StoragePath { get; set; }
    public DateTime ReceivedAt { get; set; }
}
