using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Public API v1 controller for document operations.
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Vendorea.PartnerConnect.Api.Authorization.RequireScope(ApiScopes.DocumentsRead)]
public class PublicDocumentsController : ControllerBase
{
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly ILogger<PublicDocumentsController> _logger;

    public PublicDocumentsController(
        IPartnerDocumentRepository documentRepository,
        ILogger<PublicDocumentsController> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets documents for the authenticated dealer.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] DocumentQueryRequest request,
        CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        // Get the dealer's (tenant's) documents
        var allDocuments = await _documentRepository.GetByTenantAsync(dealerId.Value, cancellationToken);

        // Apply filters
        var filtered = allDocuments.AsEnumerable();

        if (request.TradingPartnerId.HasValue)
        {
            filtered = filtered.Where(d => d.TradingPartnerId == request.TradingPartnerId.Value);
        }

        if (!string.IsNullOrEmpty(request.DocumentType))
        {
            if (Enum.TryParse<DocumentType>(request.DocumentType, true, out var docType))
            {
                filtered = filtered.Where(d => d.DocumentType == docType);
            }
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            if (Enum.TryParse<DocumentStatus>(request.Status, true, out var status))
            {
                filtered = filtered.Where(d => d.Status == status);
            }
        }

        if (!string.IsNullOrEmpty(request.Direction))
        {
            if (Enum.TryParse<DocumentDirection>(request.Direction, true, out var direction))
            {
                filtered = filtered.Where(d => d.Direction == direction);
            }
        }

        if (request.Since.HasValue)
        {
            filtered = filtered.Where(d => d.ReceivedAt >= request.Since.Value);
        }

        // Apply pagination
        var total = filtered.Count();
        var results = filtered
            .OrderByDescending(d => d.ReceivedAt)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(MapDocumentResponse)
            .ToList();

        return Ok(new
        {
            Total = total,
            Skip = request.Skip,
            Take = request.Take,
            Results = results
        });
    }

    /// <summary>
    /// Gets a specific document by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDocument(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document == null)
        {
            return NotFound();
        }

        // Verify ownership by tenant
        if (document.TenantId != dealerId.Value)
        {
            return NotFound();
        }

        return Ok(MapDocumentResponse(document));
    }

    /// <summary>
    /// Gets document status.
    /// </summary>
    [HttpGet("{id:int}/status")]
    public async Task<IActionResult> GetDocumentStatus(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document == null)
        {
            return NotFound();
        }

        // Verify ownership
        if (document.TenantId != dealerId.Value)
        {
            return NotFound();
        }

        return Ok(new
        {
            document.Id,
            Status = document.Status.ToString(),
            document.ReceivedAt,
            ProcessedAt = document.ProcessingCompletedAt,
            document.SentAt,
            ErrorMessage = document.ErrorDetails,
            document.RetryCount
        });
    }

    /// <summary>
    /// Requests reprocessing of a failed document.
    /// </summary>
    [HttpPost("{id:int}/reprocess")]
    [Vendorea.PartnerConnect.Api.Authorization.RequireScope(ApiScopes.DocumentsWrite)]
    public async Task<IActionResult> ReprocessDocument(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var document = await _documentRepository.GetByIdAsync(id, cancellationToken);

        if (document == null)
        {
            return NotFound();
        }

        // Verify ownership
        if (document.TenantId != dealerId.Value)
        {
            return NotFound();
        }

        // Only allow reprocessing of failed documents
        if (document.Status != DocumentStatus.Failed)
        {
            return BadRequest("Only failed documents can be reprocessed");
        }

        document.Status = DocumentStatus.Received;
        document.ErrorDetails = null;
        document.RetryCount++;

        await _documentRepository.UpdateAsync(document, cancellationToken);

        _logger.LogInformation(
            "Document {DocumentId} queued for reprocessing by dealer {DealerId}",
            id,
            dealerId.Value);

        return Accepted(new
        {
            document.Id,
            Status = document.Status.ToString(),
            Message = "Document queued for reprocessing"
        });
    }

    /// <summary>
    /// Gets document statistics for the authenticated dealer.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetDocumentStats(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        // Get the dealer's (tenant's) documents
        var allDocuments = await _documentRepository.GetByTenantAsync(dealerId.Value, cancellationToken);

        var since = DateTime.UtcNow.AddDays(-days);
        var recentDocs = allDocuments.Where(d => d.ReceivedAt >= since).ToList();

        var stats = new
        {
            Period = new { StartDate = since, EndDate = DateTime.UtcNow },
            Total = recentDocs.Count,
            ByStatus = recentDocs
                .GroupBy(d => d.Status)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
            ByDirection = recentDocs
                .GroupBy(d => d.Direction)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
            ByDocumentType = recentDocs
                .GroupBy(d => d.DocumentType)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByPartner = recentDocs
                .GroupBy(d => d.TradingPartnerId)
                .Select(g => new { TradingPartnerId = g.Key, DocumentCount = g.Count() }),
            DailyActivity = recentDocs
                .GroupBy(d => d.ReceivedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(a => a.Date)
                .ToList()
        };

        return Ok(stats);
    }

    private int? GetDealerIdFromClaims()
    {
        var dealerIdClaim = User.FindFirst("DealerId")?.Value;
        if (int.TryParse(dealerIdClaim, out var dealerId))
        {
            return dealerId;
        }
        return null;
    }

    private static object MapDocumentResponse(PartnerDocument document)
    {
        return new
        {
            document.Id,
            document.TradingPartnerId,
            document.TenantId,
            document.DocumentType,
            Direction = document.Direction.ToString(),
            Status = document.Status.ToString(),
            document.FileName,
            document.FileSizeBytes,
            document.ContentHash,
            document.ReceivedAt,
            ProcessedAt = document.ProcessingCompletedAt,
            document.SentAt,
            ErrorMessage = document.ErrorDetails
        };
    }
}

public class DocumentQueryRequest
{
    public int? TradingPartnerId { get; set; }
    public string? DocumentType { get; set; }
    public string? Status { get; set; }
    public string? Direction { get; set; }
    public DateTime? Since { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
}
