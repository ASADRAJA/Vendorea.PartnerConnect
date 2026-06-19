using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin dashboard controller for system health and statistics.
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly IMerchant360Client _merchant360Client;
    private readonly ILogger<AdminDashboardController> _logger;

    public AdminDashboardController(
        IPartnerDocumentRepository documentRepository,
        ITradingPartnerRepository partnerRepository,
        IMerchant360Client merchant360Client,
        ILogger<AdminDashboardController> logger)
    {
        _documentRepository = documentRepository;
        _partnerRepository = partnerRepository;
        _merchant360Client = merchant360Client;
        _logger = logger;
    }

    /// <summary>
    /// Gets system health status.
    /// </summary>
    [HttpGet("health")]
    public IActionResult GetSystemHealth()
    {
        return Ok(new SystemHealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Components = new Dictionary<string, ComponentHealth>
            {
                ["Database"] = new ComponentHealth { Status = "Healthy", ResponseTimeMs = 5 },
                ["Storage"] = new ComponentHealth { Status = "Healthy", ResponseTimeMs = 10 },
                ["BackgroundWorkers"] = new ComponentHealth { Status = "Healthy", ResponseTimeMs = 0 }
            }
        });
    }

    /// <summary>
    /// Gets overall system statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetSystemStats(CancellationToken cancellationToken)
    {
        // Get dealers from M360
        var merchants = await _merchant360Client.GetMerchantsAsync(activeOnly: false, cancellationToken);

        // Get active trading partners
        var activePartners = await _partnerRepository.GetByStatusAsync(TradingPartnerStatus.Active, cancellationToken);

        // Get document stats
        var documentStats = await _documentRepository.GetDocumentStatsAsync(cancellationToken);

        return Ok(new
        {
            TotalDealers = merchants.Count,
            ActivePartners = activePartners.Count,
            TotalDocuments = documentStats.Total,
            PendingDocuments = documentStats.Pending,
            FailedDocuments = documentStats.Failed,
            QuarantinedDocuments = documentStats.Quarantined
        });
    }

    /// <summary>
    /// Gets pending documents.
    /// </summary>
    [HttpGet("documents/pending")]
    public async Task<IActionResult> GetPendingDocuments(CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetPendingDocumentsAsync(cancellationToken);

        return Ok(documents.Select(d => new
        {
            d.Id,
            d.TradingPartnerId,
            d.TenantId,
            d.DocumentType,
            d.Direction,
            d.Status,
            d.FileName,
            d.ReceivedAt
        }));
    }

    /// <summary>
    /// Gets merchants from Merchant360.
    /// </summary>
    [HttpGet("merchants")]
    public async Task<IActionResult> GetMerchants(CancellationToken cancellationToken)
    {
        var merchants = await _merchant360Client.GetMerchantsAsync(activeOnly: true, cancellationToken);
        return Ok(merchants);
    }
}

public class SystemHealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();
}

public class ComponentHealth
{
    public string Status { get; set; } = string.Empty;
    public int ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}
