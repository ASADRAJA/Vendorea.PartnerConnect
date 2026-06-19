using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin endpoint to ingest an inbound SPR XML document (POACK, ASN, invoice) for a connection and
/// inspect the resulting Merchant360 callbacks — a self-contained way to simulate the SPR feedback
/// loop without a live SPR connection. Gated by the "SprSimulation" config toggles, which default
/// OFF so this is inert against the live SPR system.
/// In production these documents arrive over SFTP; this endpoint accepts the same payload directly.
/// </summary>
[ApiController]
[Route("api/admin/spr/inbound")]
public class AdminSprInboundController : ControllerBase
{
    private readonly ISprXmlDocumentProcessingService _processingService;
    private readonly IOutboxRepository _outboxRepository;
    private readonly SprSimulationOptions _simulation;
    private readonly ILogger<AdminSprInboundController> _logger;

    private static readonly HashSet<string> Merchant360CallbackTypes = new(StringComparer.Ordinal)
    {
        Merchant360OutboxMessageTypes.OrderStatus,
        Merchant360OutboxMessageTypes.Shipment,
        Merchant360OutboxMessageTypes.Invoice
    };

    public AdminSprInboundController(
        ISprXmlDocumentProcessingService processingService,
        IOutboxRepository outboxRepository,
        IOptions<SprSimulationOptions> simulationOptions,
        ILogger<AdminSprInboundController> logger)
    {
        _processingService = processingService;
        _outboxRepository = outboxRepository;
        _simulation = simulationOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Ingests a raw SPR XML document. The document type is auto-detected when not supplied.
    /// Disabled unless SprSimulation:AllowInboundInjection is true.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Ingest([FromBody] IngestSprDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!_simulation.AllowInboundInjection)
            return StatusCode(403, new { error = "SPR inbound simulation is disabled (set SprSimulation:AllowInboundInjection=true to enable in a test environment)" });

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

    /// <summary>
    /// Lists the Merchant360 callbacks (order-status / shipment / invoice) enqueued for an order,
    /// looked up by its correlation id. In capture mode these are short-circuited (not delivered),
    /// so this is how you inspect exactly what would be / was sent to Merchant360.
    /// </summary>
    [HttpGet("callbacks")]
    public async Task<IActionResult> GetCallbacks([FromQuery] string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return BadRequest(new { error = "correlationId is required" });

        var messages = await _outboxRepository.GetByCorrelationIdAsync(correlationId, cancellationToken);

        var callbacks = messages
            .Where(m => Merchant360CallbackTypes.Contains(m.MessageType))
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                id = m.Id,
                messageType = m.MessageType,
                status = m.Status.ToString(),
                createdAt = m.CreatedAt,
                deliveredAt = m.DeliveredAt,
                retryCount = m.RetryCount,
                lastError = m.LastError,
                payload = m.Payload
            })
            .ToList();

        return Ok(new { captureMode = _simulation.CaptureCallbacks, count = callbacks.Count, callbacks });
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
