using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for document operations.
/// </summary>
[ApiController]
[Route("api/admin/documents")]
[AllowAnonymous] // TODO: Restore [Authorize(Policy = "RequireSystemAdmin")] in production
public class AdminDocumentsController : ControllerBase
{
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly IDealerPartnerConnectionRepository _connectionRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly IMerchant360Client _merchant360Client;
    private readonly ILogger<AdminDocumentsController> _logger;

    public AdminDocumentsController(
        IPartnerDocumentRepository documentRepository,
        IDealerPartnerConnectionRepository connectionRepository,
        ITradingPartnerRepository partnerRepository,
        IMerchant360Client merchant360Client,
        ILogger<AdminDocumentsController> logger)
    {
        _documentRepository = documentRepository;
        _connectionRepository = connectionRepository;
        _partnerRepository = partnerRepository;
        _merchant360Client = merchant360Client;
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
        // Get all connections to map documents to dealers/partners
        var allConnections = await _connectionRepository.GetAllAsync(cancellationToken);
        var connectionMap = allConnections.ToDictionary(c => c.Id);

        // Get all documents from all connections
        var allDocuments = new List<PartnerDocument>();
        foreach (var connection in allConnections)
        {
            var docs = await _documentRepository.GetByConnectionIdAsync(connection.Id, cancellationToken);
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
            var dealerConnectionIds = allConnections.Where(c => c.DealerId == dealerId.Value).Select(c => c.Id).ToHashSet();
            filtered = filtered.Where(d => dealerConnectionIds.Contains(d.DealerPartnerConnectionId));
        }

        if (tradingPartnerId.HasValue)
        {
            var partnerConnectionIds = allConnections.Where(c => c.TradingPartnerId == tradingPartnerId.Value).Select(c => c.Id).ToHashSet();
            filtered = filtered.Where(d => partnerConnectionIds.Contains(d.DealerPartnerConnectionId));
        }

        var total = filtered.Count();
        var results = filtered
            .OrderByDescending(d => d.ReceivedAt)
            .Skip(skip)
            .Take(take)
            .ToList();

        // Get dealer names
        var dealerIds = results.Select(d => connectionMap.TryGetValue(d.DealerPartnerConnectionId, out var c) ? c.DealerId : 0).Distinct().ToList();
        var dealerNames = await GetDealerNamesAsync(dealerIds, cancellationToken);

        // Map to response with dealer/partner info
        var mappedResults = results.Select(doc =>
        {
            connectionMap.TryGetValue(doc.DealerPartnerConnectionId, out var connection);
            dealerNames.TryGetValue(connection?.DealerId ?? 0, out var dealerName);

            return new AdminDocumentResponse
            {
                Id = doc.Id,
                DealerPartnerConnectionId = doc.DealerPartnerConnectionId,
                DealerId = connection?.DealerId ?? 0,
                DealerName = dealerName ?? $"Dealer #{connection?.DealerId}",
                TradingPartnerId = connection?.TradingPartnerId ?? 0,
                PartnerName = connection?.TradingPartner?.Name ?? $"Partner #{connection?.TradingPartnerId}",
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

        // Get unique document types, dealers, and partners for filter options
        var documentTypes = allDocuments.Select(d => d.DocumentType.ToString()).Distinct().OrderBy(x => x).ToList();
        var dealers = allConnections.GroupBy(c => c.DealerId).Select(g => new { Id = g.Key, Name = dealerNames.TryGetValue(g.Key, out var n) ? n : $"Dealer #{g.Key}" }).ToList();
        var partners = allConnections.Where(c => c.TradingPartner != null).GroupBy(c => c.TradingPartnerId).Select(g => new { Id = g.Key, Name = g.First().TradingPartner?.Name ?? $"Partner #{g.Key}" }).ToList();

        return Ok(new
        {
            Total = total,
            Skip = skip,
            Take = take,
            Results = mappedResults,
            FilterOptions = new
            {
                DocumentTypes = documentTypes,
                Dealers = dealers,
                Partners = partners
            }
        });
    }

    private async Task<Dictionary<int, string>> GetDealerNamesAsync(List<int> dealerIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, string>();
        try
        {
            var merchants = await _merchant360Client.GetMerchantsAsync(activeOnly: false, cancellationToken);
            foreach (var id in dealerIds)
            {
                var merchant = merchants.FirstOrDefault(m => m.Id == id);
                if (merchant != null)
                {
                    result[id] = merchant.Name;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get dealer names from M360");
        }
        return result;
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

public class AdminDocumentResponse
{
    public int Id { get; set; }
    public int DealerPartnerConnectionId { get; set; }
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
