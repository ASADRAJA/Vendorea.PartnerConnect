using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.API.Controllers;

/// <summary>
/// API controller for managing EDI X12 documents.
/// </summary>
[ApiController]
[Route("api/v1/edi/documents")]
public class EdiDocumentsController : ControllerBase
{
    private readonly IEdiDocumentProcessingService _processingService;
    private readonly IEdiResponseService _responseService;
    private readonly IEdiDocumentRepository _ediDocumentRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ILogger<EdiDocumentsController> _logger;

    public EdiDocumentsController(
        IEdiDocumentProcessingService processingService,
        IEdiResponseService responseService,
        IEdiDocumentRepository ediDocumentRepository,
        ITradingPartnerRepository partnerRepository,
        ILogger<EdiDocumentsController> logger)
    {
        _processingService = processingService;
        _responseService = responseService;
        _ediDocumentRepository = ediDocumentRepository;
        _partnerRepository = partnerRepository;
        _logger = logger;
    }

    /// <summary>
    /// Uploads and processes an EDI document.
    /// </summary>
    /// <param name="tradingPartnerId">The dealer-partner connection ID.</param>
    /// <param name="file">The EDI file to upload.</param>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(EdiProcessingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(10_000_000)] // 10MB limit for EDI files
    public async Task<IActionResult> Upload(
        [FromQuery] int tradingPartnerId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        var partner = await _partnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);
        if (partner == null)
        {
            return NotFound($"Trading partner {tradingPartnerId} not found");
        }

        _logger.LogInformation(
            "Uploading EDI document for connection {ConnectionId}: {FileName}",
            tradingPartnerId, file.FileName);

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync(cancellationToken);

        var result = await _processingService.ProcessDocumentAsync(
            tradingPartnerId, content, file.FileName, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Uploads and processes raw EDI content.
    /// </summary>
    /// <param name="tradingPartnerId">The dealer-partner connection ID.</param>
    /// <param name="request">The EDI content to process.</param>
    [HttpPost("process")]
    [ProducesResponseType(typeof(EdiProcessingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Process(
        [FromQuery] int tradingPartnerId,
        [FromBody] EdiProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EdiContent))
        {
            return BadRequest("EDI content is required");
        }

        var partner = await _partnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);
        if (partner == null)
        {
            return NotFound($"Trading partner {tradingPartnerId} not found");
        }

        var fileName = request.FileName ?? $"upload_{DateTime.UtcNow:yyyyMMddHHmmss}.edi";
        var result = await _processingService.ProcessDocumentAsync(
            tradingPartnerId, request.EdiContent, fileName, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Gets an EDI document by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EdiDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var document = await _ediDocumentRepository.GetByIdWithRelationsAsync(id, cancellationToken);
        if (document == null)
        {
            return NotFound($"EDI document {id} not found");
        }

        return Ok(MapToDto(document));
    }

    /// <summary>
    /// Gets EDI documents for a connection.
    /// </summary>
    [HttpGet("partner/{tradingPartnerId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<EdiDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTradingPartner(
        int tradingPartnerId,
        [FromQuery] string? transactionSetCode = null,
        [FromQuery] string? direction = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        EdiDirection? directionEnum = null;
        if (!string.IsNullOrEmpty(direction) && Enum.TryParse<EdiDirection>(direction, true, out var dir))
        {
            directionEnum = dir;
        }

        var documents = await _processingService.GetDocumentsAsync(
            tradingPartnerId, transactionSetCode, directionEnum, skip, take, cancellationToken);

        return Ok(documents.Select(MapToDto));
    }

    /// <summary>
    /// Gets the parsed canonical data for an EDI document.
    /// </summary>
    [HttpGet("{id:int}/canonical")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCanonical(int id, CancellationToken cancellationToken = default)
    {
        var document = await _ediDocumentRepository.GetByIdAsync(id, cancellationToken);
        if (document == null)
        {
            return NotFound($"EDI document {id} not found");
        }

        if (string.IsNullOrEmpty(document.CanonicalJson))
        {
            return NotFound("No canonical data available for this document");
        }

        var canonical = JsonSerializer.Deserialize<JsonElement>(document.CanonicalJson);
        return Ok(new
        {
            document.Id,
            document.CanonicalType,
            Data = canonical
        });
    }

    /// <summary>
    /// Gets the raw EDI content for a document.
    /// </summary>
    [HttpGet("{id:int}/raw")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRaw(int id, CancellationToken cancellationToken = default)
    {
        var document = await _ediDocumentRepository.GetByIdAsync(id, cancellationToken);
        if (document == null)
        {
            return NotFound($"EDI document {id} not found");
        }

        if (string.IsNullOrEmpty(document.RawEdiContent))
        {
            return NotFound("No raw content available for this document");
        }

        return Content(document.RawEdiContent, "application/edi-x12");
    }

    /// <summary>
    /// Downloads the raw EDI content as a file.
    /// </summary>
    [HttpGet("{id:int}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken = default)
    {
        var document = await _ediDocumentRepository.GetByIdAsync(id, cancellationToken);
        if (document == null)
        {
            return NotFound($"EDI document {id} not found");
        }

        if (string.IsNullOrEmpty(document.RawEdiContent))
        {
            return NotFound("No raw content available for this document");
        }

        var fileName = $"{document.TransactionSetCode}_{document.InterchangeControlNumber}.edi";
        var bytes = System.Text.Encoding.UTF8.GetBytes(document.RawEdiContent);
        return File(bytes, "application/edi-x12", fileName);
    }

    /// <summary>
    /// Generates a 997 Functional Acknowledgment for a document.
    /// </summary>
    [HttpPost("{id:int}/generate-997")]
    [ProducesResponseType(typeof(EdiResponseResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Generate997(int id, CancellationToken cancellationToken = default)
    {
        var document = await _ediDocumentRepository.GetByIdAsync(id, cancellationToken);
        if (document == null)
        {
            return NotFound($"EDI document {id} not found");
        }

        if (document.Direction != EdiDirection.Inbound)
        {
            return BadRequest("997 can only be generated for inbound documents");
        }

        var result = await _responseService.Generate997Async(id, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Generates an 855 Purchase Order Acknowledgment for an 850 document.
    /// </summary>
    [HttpPost("{id:int}/generate-855")]
    [ProducesResponseType(typeof(EdiResponseResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Generate855(
        int id,
        [FromBody] Edi855Options? options = null,
        CancellationToken cancellationToken = default)
    {
        var document = await _ediDocumentRepository.GetByIdAsync(id, cancellationToken);
        if (document == null)
        {
            return NotFound($"EDI document {id} not found");
        }

        if (document.TransactionSetCode != "850")
        {
            return BadRequest("855 can only be generated for 850 Purchase Order documents");
        }

        var result = await _responseService.Generate855Async(id, options, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Sends a pending outbound document.
    /// </summary>
    [HttpPost("{id:int}/send")]
    [ProducesResponseType(typeof(EdiSendResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send(int id, CancellationToken cancellationToken = default)
    {
        var document = await _ediDocumentRepository.GetByIdAsync(id, cancellationToken);
        if (document == null)
        {
            return NotFound($"EDI document {id} not found");
        }

        if (document.Direction != EdiDirection.Outbound)
        {
            return BadRequest("Can only send outbound documents");
        }

        var result = await _responseService.SendResponseAsync(id, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Gets pending outbound documents.
    /// </summary>
    [HttpGet("pending-outbound")]
    [ProducesResponseType(typeof(IReadOnlyList<EdiDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingOutbound(
        [FromQuery] int? tradingPartnerId = null,
        CancellationToken cancellationToken = default)
    {
        var documents = await _responseService.GetPendingResponsesAsync(tradingPartnerId, cancellationToken);
        return Ok(documents.Select(MapToDto));
    }

    /// <summary>
    /// Sends all pending outbound documents for a connection.
    /// </summary>
    [HttpPost("partner/{tradingPartnerId:int}/send-pending")]
    [ProducesResponseType(typeof(EdiBatchSendResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendPending(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        var result = await _responseService.SendPendingResponsesAsync(tradingPartnerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Triggers a sync of EDI documents for a connection.
    /// </summary>
    [HttpPost("partner/{tradingPartnerId:int}/sync")]
    [ProducesResponseType(typeof(EdiSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sync(int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        var partner = await _partnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);
        if (partner == null)
        {
            return NotFound($"Trading partner {tradingPartnerId} not found");
        }

        _logger.LogInformation("Manually triggered EDI sync for connection {ConnectionId}", tradingPartnerId);

        var result = await _processingService.SyncEdiDocumentsAsync(tradingPartnerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets document counts by transaction set type for a connection.
    /// </summary>
    [HttpGet("partner/{tradingPartnerId:int}/stats")]
    [ProducesResponseType(typeof(Dictionary<string, int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        var counts = await _ediDocumentRepository.GetCountsByTransactionSetAsync(tradingPartnerId, cancellationToken);
        return Ok(counts);
    }

    private static EdiDocumentDto MapToDto(EdiDocument document)
    {
        return new EdiDocumentDto
        {
            Id = document.Id,
            PartnerDocumentId = document.PartnerDocumentId,
            TransactionSetCode = document.TransactionSetCode,
            InterchangeControlNumber = document.InterchangeControlNumber,
            GroupControlNumber = document.GroupControlNumber,
            TransactionControlNumber = document.TransactionControlNumber,
            SenderId = document.SenderId,
            ReceiverId = document.ReceiverId,
            SenderQualifier = document.SenderQualifier,
            ReceiverQualifier = document.ReceiverQualifier,
            Direction = document.Direction.ToString(),
            CanonicalType = document.CanonicalType,
            BusinessReference = document.BusinessReference,
            LineItemCount = document.LineItemCount,
            TotalAmount = document.TotalAmount,
            AcknowledgmentGenerated = document.AcknowledgmentGenerated,
            AcknowledgmentSent = document.AcknowledgmentSent,
            AcknowledgmentSentAt = document.AcknowledgmentSentAt,
            ResponseDocumentId = document.ResponseDocumentId,
            OriginalDocumentId = document.OriginalDocumentId,
            ProcessingErrors = document.ProcessingErrors,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };
    }
}

/// <summary>
/// Request DTO for processing raw EDI content.
/// </summary>
public class EdiProcessRequest
{
    /// <summary>
    /// Raw EDI X12 content to process.
    /// </summary>
    public string EdiContent { get; set; } = string.Empty;

    /// <summary>
    /// Optional file name for tracking.
    /// </summary>
    public string? FileName { get; set; }
}

/// <summary>
/// DTO for EDI document responses.
/// </summary>
public class EdiDocumentDto
{
    public int Id { get; set; }
    public int PartnerDocumentId { get; set; }
    public string TransactionSetCode { get; set; } = string.Empty;
    public string InterchangeControlNumber { get; set; } = string.Empty;
    public string GroupControlNumber { get; set; } = string.Empty;
    public string TransactionControlNumber { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string? SenderQualifier { get; set; }
    public string? ReceiverQualifier { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string? CanonicalType { get; set; }
    public string? BusinessReference { get; set; }
    public int? LineItemCount { get; set; }
    public decimal? TotalAmount { get; set; }
    public bool AcknowledgmentGenerated { get; set; }
    public bool AcknowledgmentSent { get; set; }
    public DateTime? AcknowledgmentSentAt { get; set; }
    public int? ResponseDocumentId { get; set; }
    public int? OriginalDocumentId { get; set; }
    public string? ProcessingErrors { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
