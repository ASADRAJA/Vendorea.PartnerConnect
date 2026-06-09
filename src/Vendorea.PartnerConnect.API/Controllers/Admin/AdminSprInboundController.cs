using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin endpoint to ingest an inbound SPR XML document (POACK, ASN, invoice) for a connection.
/// This is the minimal real inbound path used to drive and verify the document pipeline —
/// e.g. posting an ERROR POACK and observing the order move to Failed with a normalized message.
/// In production these documents arrive over SFTP; this endpoint accepts the same payload directly.
/// </summary>
[ApiController]
[Route("api/admin/spr/inbound")]
[AllowAnonymous] // TODO: Restore [Authorize(Policy = "RequireSystemAdmin")] in production
public class AdminSprInboundController : ControllerBase
{
    private readonly ISprXmlDocumentProcessingService _processingService;
    private readonly ILogger<AdminSprInboundController> _logger;

    public AdminSprInboundController(
        ISprXmlDocumentProcessingService processingService,
        ILogger<AdminSprInboundController> logger)
    {
        _processingService = processingService;
        _logger = logger;
    }

    /// <summary>
    /// Ingests a raw SPR XML document. The document type is auto-detected when not supplied.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Ingest([FromBody] IngestSprDocumentRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Xml))
            return BadRequest(new { error = "xml is required" });

        if (request.ConnectionId <= 0)
            return BadRequest(new { error = "connectionId is required" });

        var fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? $"inbound_{request.ConnectionId}.xml"
            : request.FileName!;

        // Inbound documents arrive in a known context (POACK/ASN/invoice path), so the caller
        // may state the type explicitly; otherwise it is auto-detected.
        Domain.Entities.SprXmlDocumentType? documentType = null;
        if (!string.IsNullOrWhiteSpace(request.DocumentType)
            && Enum.TryParse<Domain.Entities.SprXmlDocumentType>(request.DocumentType, ignoreCase: true, out var parsed))
        {
            documentType = parsed;
        }

        var result = await _processingService.ProcessInboundDocumentAsync(
            request.ConnectionId, request.Xml!, fileName, documentType, cancellationToken);

        _logger.LogInformation(
            "Ingested SPR inbound document for connection {ConnectionId}: type={Type}, success={Success}",
            request.ConnectionId, result.DocumentType, result.Success);

        return Ok(new
        {
            success = result.Success,
            documentType = result.DocumentType?.ToString(),
            sprXmlDocumentId = result.SprXmlDocumentId,
            partnerDocumentId = result.PartnerDocumentId,
            businessReference = result.BusinessReference,
            canonicalType = result.CanonicalType,
            errors = result.Errors,
            warnings = result.Warnings,
            errorMessage = result.ErrorMessage
        });
    }
}

/// <summary>
/// Request to ingest an inbound SPR XML document.
/// </summary>
public class IngestSprDocumentRequest
{
    public int ConnectionId { get; set; }
    public string? FileName { get; set; }
    public string? Xml { get; set; }

    /// <summary>Optional explicit type (EZPOACK, EZASNS, EZINV4); auto-detected when omitted.</summary>
    public string? DocumentType { get; set; }
}
