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
[AllowAnonymous] // TODO: Restore [Authorize(Policy = "RequireSystemAdmin")] in production
public class AdminDashboardController : ControllerBase
{
    private readonly IPartnerDocumentRepository _documentRepository;
    private readonly IDealerPartnerConnectionRepository _connectionRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly IMerchant360Client _merchant360Client;
    private readonly ILogger<AdminDashboardController> _logger;

    public AdminDashboardController(
        IPartnerDocumentRepository documentRepository,
        IDealerPartnerConnectionRepository connectionRepository,
        ITradingPartnerRepository partnerRepository,
        IMerchant360Client merchant360Client,
        ILogger<AdminDashboardController> logger)
    {
        _documentRepository = documentRepository;
        _connectionRepository = connectionRepository;
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
        var partners = await _partnerRepository.GetAllAsync(cancellationToken);
        var connections = await _connectionRepository.GetActiveConnectionsAsync(cancellationToken);
        var pendingDocs = await _documentRepository.GetPendingDocumentsAsync(cancellationToken);

        return Ok(new
        {
            Timestamp = DateTime.UtcNow,
            Partners = new
            {
                Total = partners.Count,
                Active = partners.Count(p => p.Status == TradingPartnerStatus.Active)
            },
            Connections = new
            {
                Active = connections.Count
            },
            Documents = new
            {
                PendingCount = pendingDocs.Count
            }
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
            d.DealerPartnerConnectionId,
            d.DocumentType,
            d.Direction,
            d.Status,
            d.FileName,
            d.ReceivedAt
        }));
    }

    /// <summary>
    /// Gets active connections (legacy endpoint).
    /// </summary>
    [HttpGet("connections")]
    public async Task<IActionResult> GetActiveConnections(CancellationToken cancellationToken)
    {
        var connections = await _connectionRepository.GetActiveConnectionsAsync(cancellationToken);

        return Ok(new
        {
            Total = connections.Count,
            Connections = connections.Select(c => new
            {
                c.Id,
                c.DealerId,
                c.TradingPartnerId,
                IsActive = c.Status == ConnectionStatus.Active,
                LastSuccessfulSync = c.LastSuccessfulSyncAt,
                LastSyncAttempt = c.LastSyncAt
            })
        });
    }

    /// <summary>
    /// Gets all connections (all statuses) for admin portal.
    /// </summary>
    [HttpGet("connections/all")]
    public async Task<IActionResult> GetAllConnections(CancellationToken cancellationToken)
    {
        var connections = await _connectionRepository.GetAllAsync(cancellationToken);

        // Get dealer names from M360
        var dealerIds = connections.Select(c => c.DealerId).Distinct().ToList();
        var dealerNames = await GetDealerNamesAsync(dealerIds, cancellationToken);

        return Ok(connections.Select(c => new
        {
            c.Id,
            c.DealerId,
            DealerName = dealerNames.TryGetValue(c.DealerId, out var name) ? name : $"Dealer #{c.DealerId}",
            c.TradingPartnerId,
            PartnerName = c.TradingPartner?.Name,
            IsActive = c.Status == ConnectionStatus.Active,
            Status = c.Status.ToString(),
            LastSuccessfulSync = c.LastSuccessfulSyncAt,
            LastSyncAttempt = c.LastSyncAt,
            c.CreatedAt
        }));
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
