using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Public API v1 controller for trading partner operations.
/// Used by dealers (API key auth) and Merchant360 (OAuth2) to get partner catalog.
/// </summary>
[ApiController]
[Route("api/v1/partners")]
[AllowAnonymous] // TODO: Restore [Authorize] in production - supports both ApiKey and OAuth2
public class PublicTradingPartnersController : ControllerBase
{
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly IDealerPartnerConnectionRepository _connectionRepository;
    private readonly IPriceFeedUploadRepository _priceFeedRepository;
    private readonly ISprContentUploadRepository _contentUploadRepository;
    private readonly ILogger<PublicTradingPartnersController> _logger;

    public PublicTradingPartnersController(
        ITradingPartnerRepository partnerRepository,
        IDealerPartnerConnectionRepository connectionRepository,
        IPriceFeedUploadRepository priceFeedRepository,
        ISprContentUploadRepository contentUploadRepository,
        ILogger<PublicTradingPartnersController> logger)
    {
        _partnerRepository = partnerRepository;
        _connectionRepository = connectionRepository;
        _priceFeedRepository = priceFeedRepository;
        _contentUploadRepository = contentUploadRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets available trading partners with data availability info.
    /// Used by Merchant360 to display partner catalog for subscriptions.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailablePartners(CancellationToken cancellationToken)
    {
        var partners = await _partnerRepository.GetAllAsync(cancellationToken);
        var activePartners = partners.Where(p => p.Status == TradingPartnerStatus.Active).ToList();

        var result = new List<object>();
        foreach (var p in activePartners)
        {
            var hasPriceData = await _priceFeedRepository.HasDataForPartnerAsync(p.Id, cancellationToken);
            var hasEnhancedContent = await _contentUploadRepository.HasDataForPartnerAsync(p.Id, cancellationToken);

            result.Add(new
            {
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.LogoUrl,
                HasPriceData = hasPriceData,
                HasEnhancedContent = hasEnhancedContent,
                IsActive = p.Status == TradingPartnerStatus.Active
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Gets a specific trading partner with data availability info.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPartner(int id, CancellationToken cancellationToken)
    {
        var partner = await _partnerRepository.GetByIdAsync(id, cancellationToken);

        if (partner == null)
        {
            return NotFound();
        }

        var hasPriceData = await _priceFeedRepository.HasDataForPartnerAsync(partner.Id, cancellationToken);
        var hasEnhancedContent = await _contentUploadRepository.HasDataForPartnerAsync(partner.Id, cancellationToken);

        return Ok(new
        {
            partner.Id,
            partner.Code,
            partner.Name,
            partner.Description,
            partner.LogoUrl,
            HasPriceData = hasPriceData,
            HasEnhancedContent = hasEnhancedContent,
            IsActive = partner.Status == TradingPartnerStatus.Active
        });
    }

    /// <summary>
    /// Gets connections for the authenticated dealer.
    /// </summary>
    [HttpGet("connections")]
    public async Task<IActionResult> GetDealerConnections(CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var connections = await _connectionRepository.GetByDealerIdAsync(dealerId.Value, cancellationToken);

        return Ok(connections.Select(c => new
        {
            c.Id,
            c.TradingPartnerId,
            PartnerName = c.TradingPartner?.Name,
            IsActive = c.Status == ConnectionStatus.Active,
            LastSuccessfulSync = c.LastSuccessfulSyncAt,
            LastSyncAttempt = c.LastSyncAt,
            c.CreatedAt
        }));
    }

    /// <summary>
    /// Gets a specific connection.
    /// </summary>
    [HttpGet("connections/{id:int}")]
    public async Task<IActionResult> GetConnection(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);

        if (connection == null || connection.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        return Ok(new
        {
            connection.Id,
            connection.TradingPartnerId,
            PartnerName = connection.TradingPartner?.Name,
            IsActive = connection.Status == ConnectionStatus.Active,
            LastSuccessfulSync = connection.LastSuccessfulSyncAt,
            LastSyncAttempt = connection.LastSyncAt,
            connection.CreatedAt
        });
    }

    /// <summary>
    /// Activates a connection.
    /// </summary>
    [HttpPost("connections/{id:int}/activate")]
    public async Task<IActionResult> ActivateConnection(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);

        if (connection == null || connection.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        connection.Status = ConnectionStatus.Active;
        await _connectionRepository.UpdateAsync(connection, cancellationToken);

        _logger.LogInformation("Connection {ConnectionId} activated", id);

        return Ok(new { ConnectionId = id, IsActive = true, Status = connection.Status.ToString() });
    }

    /// <summary>
    /// Deactivates a connection.
    /// </summary>
    [HttpPost("connections/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateConnection(int id, CancellationToken cancellationToken)
    {
        var dealerId = GetDealerIdFromClaims();
        if (!dealerId.HasValue)
        {
            return Unauthorized("Dealer ID not found in API key claims");
        }

        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);

        if (connection == null || connection.DealerId != dealerId.Value)
        {
            return NotFound();
        }

        connection.Status = ConnectionStatus.Inactive;
        await _connectionRepository.UpdateAsync(connection, cancellationToken);

        _logger.LogInformation("Connection {ConnectionId} deactivated", id);

        return Ok(new { ConnectionId = id, IsActive = false, Status = connection.Status.ToString() });
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
}
