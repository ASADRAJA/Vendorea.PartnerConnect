using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.StateMachine;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for document operations.
/// </summary>
[ApiController]
[Route("api/admin/documents")]
public class AdminDocumentsController : ControllerBase
{
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IMerchant360Client _merchant360Client;
    private readonly IDocumentCorrelationRepository _correlationRepository;
    private readonly IDocumentStateHistoryRepository _stateHistoryRepository;
    private readonly IDocumentProcessingOrchestrator _orchestrator;
    private readonly ILogger<AdminDocumentsController> _logger;

    public AdminDocumentsController(
        IPartnerDocumentRepository documentRepository,
        ITradingPartnerRepository partnerRepository,
        ITenantRepository tenantRepository,
        IMerchant360Client merchant360Client,
        IDocumentCorrelationRepository correlationRepository,
        IDocumentStateHistoryRepository stateHistoryRepository,
        IDocumentProcessingOrchestrator orchestrator,
        ILogger<AdminDocumentsController> logger)
    {
        _documentRepository = documentRepository;
        _partnerRepository = partnerRepository;
        _tenantRepository = tenantRepository;
        _merchant360Client = merchant360Client;
        _correlationRepository = correlationRepository;
        _stateHistoryRepository = stateHistoryRepository;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Gets all documents with filters for admin portal.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllDocuments(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? status = null,
        [FromQuery] string? documentType = null,
        [FromQuery] int? dealerId = null,
        [FromQuery] int? tradingPartnerId = null,
        CancellationToken cancellationToken = default)
    {
        // Get all trading partners to map documents to partners
        var allPartners = await _partnerRepository.GetAllAsync(cancellationToken);
        var partnerMap = allPartners.ToDictionary(p => p.Id);

        // Get all documents across all partners
        var allDocuments = new List<PartnerDocument>();
        foreach (var partner in allPartners)
        {
            var docs = await _documentRepository.GetByTradingPartnerAsync(partner.Id, cancellationToken);
            allDocuments.AddRange(docs);
        }

        // Apply filters
        var filtered = allDocuments.AsEnumerable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DocumentStatus>(status, true, out var statusEnum))
        {
            filtered = filtered.Where(d => d.Status == statusEnum);
        }

        if (!string.IsNullOrEmpty(documentType) && Enum.TryParse<DocumentType>(documentType, true, out var docTypeEnum))
        {
            filtered = filtered.Where(d => d.DocumentType == docTypeEnum);
        }

        if (dealerId.HasValue)
        {
            // dealer == tenant under the converged model
            filtered = filtered.Where(d => d.TenantId == dealerId.Value);
        }

        if (tradingPartnerId.HasValue)
        {
            filtered = filtered.Where(d => d.TradingPartnerId == tradingPartnerId.Value);
        }

        var total = filtered.Count();
        var results = filtered
            .OrderByDescending(d => d.ReceivedAt)
            .Skip(skip)
            .Take(take)
            .ToList();

        // Resolve tenant (dealer) names for the documents on this page.
        var tenants = await _tenantRepository.GetAllAsync(cancellationToken);
        var tenantMap = tenants.ToDictionary(t => t.Id);

        // Map to response with partner info
        var mappedResults = results.Select(doc =>
        {
            partnerMap.TryGetValue(doc.TradingPartnerId, out var partner);
            Tenant? tenant = null;
            if (doc.TenantId.HasValue)
                tenantMap.TryGetValue(doc.TenantId.Value, out tenant);

            return new AdminDocumentResponse
            {
                Id = doc.Id,
                TenantId = doc.TenantId,
                DealerId = doc.TenantId ?? 0,
                DealerName = tenant?.Name,
                TradingPartnerId = doc.TradingPartnerId,
                PartnerName = partner?.Name ?? $"Partner #{doc.TradingPartnerId}",
                DocumentType = doc.DocumentType.ToString(),
                Direction = doc.Direction.ToString(),
                Status = doc.Status.ToString(),
                FileName = doc.FileName,
                FileSizeBytes = doc.FileSizeBytes ?? 0,
                ContentHash = doc.ContentHash,
                StoragePath = doc.StoragePath,
                ReceivedAt = doc.ReceivedAt
            };
        }).ToList();

        // Get unique document types and partners for filter options
        var documentTypes = allDocuments.Select(d => d.DocumentType.ToString()).Distinct().OrderBy(x => x).ToList();
        var partners = allPartners.Select(p => new { Id = p.Id, Name = p.Name }).ToList();

        return Ok(new
        {
            Total = total,
            Skip = skip,
            Take = take,
            Results = mappedResults,
            FilterOptions = new
            {
                DocumentTypes = documentTypes,
                Partners = partners
            }
        });
    }

    /// <summary>
    /// Gets documents by trading partner ID.
    /// </summary>
    [HttpGet("partner/{tradingPartnerId:int}")]
    public async Task<IActionResult> GetDocumentsByTradingPartner(
        int tradingPartnerId,
        CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetByTradingPartnerAsync(tradingPartnerId, cancellationToken);

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

        var response = MapDocumentResponse(document);
        if (document.TenantId.HasValue)
        {
            var tenant = await _tenantRepository.GetByIdAsync(document.TenantId.Value, cancellationToken);
            response.DealerName = tenant?.Name;
        }
        return Ok(response);
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

    /// <summary>
    /// Gets document processing history (state transitions).
    /// </summary>
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetDocumentHistory(int id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        var history = await _stateHistoryRepository.GetByDocumentIdAsync(id, cancellationToken);

        return Ok(new DocumentHistoryResponse
        {
            DocumentId = id,
            CurrentState = document.State.ToString(),
            CurrentStatus = document.Status.ToString(),
            RetryCount = document.RetryCount,
            ReceivedAt = document.ReceivedAt,
            ProcessingStartedAt = document.ProcessingStartedAt,
            ProcessingCompletedAt = document.ProcessingCompletedAt,
            StateTransitions = history.Select(h => new StateTransitionDto
            {
                Id = h.Id,
                FromState = h.FromState.ToString(),
                ToState = h.ToState.ToString(),
                TransitionedAt = h.OccurredAt,
                TransitionedBy = h.PerformedBy,
                Reason = h.Reason
            }).ToList()
        });
    }

    /// <summary>
    /// Gets correlated documents (related PO, ACK, ASN, Invoice chain).
    /// </summary>
    [HttpGet("{id:int}/correlations")]
    public async Task<IActionResult> GetDocumentCorrelations(int id, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        var correlatedDocuments = await _correlationRepository.GetCorrelatedDocumentsAsync(id, cancellationToken);

        return Ok(new DocumentCorrelationResponse
        {
            DocumentId = id,
            DocumentType = document.DocumentType.ToString(),
            ExternalReference = document.ExternalReference,
            CorrelatedDocuments = correlatedDocuments.Select(d => new CorrelatedDocumentDto
            {
                Id = d.Id,
                DocumentType = d.DocumentType.ToString(),
                Direction = d.Direction.ToString(),
                Status = d.Status.ToString(),
                ExternalReference = d.ExternalReference,
                ReceivedAt = d.ReceivedAt
            }).ToList()
        });
    }

    /// <summary>
    /// Batch retry all failed documents.
    /// </summary>
    [HttpPost("batch-retry")]
    public async Task<IActionResult> BatchRetryFailedDocuments(
        [FromQuery] int maxAttempts = 3,
        [FromQuery] int batchSize = 25,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting batch retry of failed documents (maxAttempts={MaxAttempts}, batchSize={BatchSize})",
            maxAttempts, batchSize);

        var result = await _orchestrator.RetryFailedDocumentsAsync(maxAttempts, batchSize, cancellationToken);

        return Ok(new BatchRetryResponse
        {
            TotalAttempted = result.TotalAttempted,
            Succeeded = result.Succeeded,
            Failed = result.Failed,
            Exhausted = result.Exhausted,
            ProcessingTimeMs = result.ProcessingTimeMs,
            Results = result.Results.Select(r => new RetryResultDto
            {
                DocumentId = r.DocumentId,
                AttemptNumber = r.AttemptNumber,
                Success = r.Success,
                IsExhausted = r.IsExhausted,
                ErrorMessage = r.ErrorMessage
            }).ToList()
        });
    }

    /// <summary>
    /// Gets document statistics for dashboard.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetDocumentStats(CancellationToken cancellationToken)
    {
        var stats = await _documentRepository.GetDocumentStatsAsync(cancellationToken);

        return Ok(new DocumentStatsResponse
        {
            Total = stats.Total,
            Pending = stats.Pending,
            Failed = stats.Failed,
            Quarantined = stats.Quarantined,
            RetrievedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Process pending inbound documents.
    /// </summary>
    [HttpPost("process-inbound")]
    public async Task<IActionResult> ProcessInboundDocuments(
        [FromQuery] int? tradingPartnerId = null,
        [FromQuery] int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing inbound documents (partner={PartnerId}, batchSize={BatchSize})",
            tradingPartnerId?.ToString() ?? "all", batchSize);

        var result = await _orchestrator.ProcessInboundDocumentsAsync(tradingPartnerId, batchSize, cancellationToken);

        return Ok(new ProcessingBatchResponse
        {
            TotalProcessed = result.TotalProcessed,
            Succeeded = result.Succeeded,
            Failed = result.Failed,
            ProcessingTimeMs = result.ProcessingTimeMs,
            StartedAt = result.StartedAt,
            CompletedAt = result.CompletedAt
        });
    }

    /// <summary>
    /// Process pending outbound documents.
    /// </summary>
    [HttpPost("process-outbound")]
    public async Task<IActionResult> ProcessOutboundDocuments(
        [FromQuery] int? tradingPartnerId = null,
        [FromQuery] int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing outbound documents (partner={PartnerId}, batchSize={BatchSize})",
            tradingPartnerId?.ToString() ?? "all", batchSize);

        var result = await _orchestrator.ProcessOutboundDocumentsAsync(tradingPartnerId, batchSize, cancellationToken);

        return Ok(new ProcessingBatchResponse
        {
            TotalProcessed = result.TotalProcessed,
            Succeeded = result.Succeeded,
            Failed = result.Failed,
            ProcessingTimeMs = result.ProcessingTimeMs,
            StartedAt = result.StartedAt,
            CompletedAt = result.CompletedAt
        });
    }

    private static DocumentResponse MapDocumentResponse(PartnerDocument doc)
    {
        return new DocumentResponse
        {
            Id = doc.Id,
            TradingPartnerId = doc.TradingPartnerId,
            TenantId = doc.TenantId,
            DealerId = doc.TenantId ?? 0,
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
    public int TradingPartnerId { get; set; }
    public int? TenantId { get; set; }
    public int DealerId { get; set; }
    public string? DealerName { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ContentHash { get; set; }
    public string? StoragePath { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class AdminDocumentResponse
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public int DealerId { get; set; }
    public string? DealerName { get; set; }
    public int TradingPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ContentHash { get; set; }
    public string? StoragePath { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class DocumentHistoryResponse
{
    public int DocumentId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }
    public List<StateTransitionDto> StateTransitions { get; set; } = new();
}

public class StateTransitionDto
{
    public int Id { get; set; }
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    public DateTime TransitionedAt { get; set; }
    public string? TransitionedBy { get; set; }
    public string? Reason { get; set; }
}

public class DocumentCorrelationResponse
{
    public int DocumentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
    public List<CorrelatedDocumentDto> CorrelatedDocuments { get; set; } = new();
}

public class CorrelatedDocumentDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class BatchRetryResponse
{
    public int TotalAttempted { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Exhausted { get; set; }
    public long ProcessingTimeMs { get; set; }
    public List<RetryResultDto> Results { get; set; } = new();
}

public class RetryResultDto
{
    public int DocumentId { get; set; }
    public int AttemptNumber { get; set; }
    public bool Success { get; set; }
    public bool IsExhausted { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DocumentStatsResponse
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Failed { get; set; }
    public int Quarantined { get; set; }
    public DateTime RetrievedAt { get; set; }
}

public class ProcessingBatchResponse
{
    public int TotalProcessed { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public long ProcessingTimeMs { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
