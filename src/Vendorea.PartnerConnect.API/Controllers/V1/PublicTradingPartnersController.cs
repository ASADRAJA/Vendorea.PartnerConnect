using System.Text.Json;
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
    private readonly IPriceFeedUploadRepository _priceFeedRepository;
    private readonly ISprContentUploadRepository _contentUploadRepository;
    private readonly ILogger<PublicTradingPartnersController> _logger;

    public PublicTradingPartnersController(
        ITradingPartnerRepository partnerRepository,
        IPriceFeedUploadRepository priceFeedRepository,
        ISprContentUploadRepository contentUploadRepository,
        ILogger<PublicTradingPartnersController> logger)
    {
        _partnerRepository = partnerRepository;
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
                Status = p.Status.ToString(),
                HasPriceData = hasPriceData,
                HasEnhancedContent = hasEnhancedContent,
                IsActive = p.Status == TradingPartnerStatus.Active,
                ConnectionRequirements = ParseRequirements(p.TenantConfirmationFieldsJson)
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
            Status = partner.Status.ToString(),
            HasPriceData = hasPriceData,
            HasEnhancedContent = hasEnhancedContent,
            IsActive = partner.Status == TradingPartnerStatus.Active,
            ConnectionRequirements = ParseRequirements(partner.TenantConfirmationFieldsJson)
        });
    }

    /// <summary>
    /// Updates a trading partner's tenant connection requirements (the list of requirement names
    /// PC staff verify with the partner before approving a connection). Other partner fields are
    /// managed outside this app.
    /// </summary>
    [HttpPut("{id:int}/connection-requirements")]
    public async Task<IActionResult> UpdateConnectionRequirements(
        int id,
        [FromBody] UpdateConnectionRequirementsRequest request,
        CancellationToken cancellationToken)
    {
        var partner = await _partnerRepository.GetByIdAsync(id, cancellationToken);
        if (partner == null)
            return NotFound();

        var requirements = (request.Requirements ?? new List<string>())
            .Select(r => r?.Trim() ?? string.Empty)
            .Where(r => r.Length > 0)
            .ToList();

        partner.TenantConfirmationFieldsJson = requirements.Count > 0
            ? JsonSerializer.Serialize(requirements)
            : null;
        partner.UpdatedAt = DateTime.UtcNow;

        await _partnerRepository.UpdateAsync(partner, cancellationToken);
        _logger.LogInformation("Updated connection requirements for partner {PartnerId} ({Count} fields)", id, requirements.Count);

        return NoContent();
    }

    private static List<string> ParseRequirements(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
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

/// <summary>Request to replace a trading partner's tenant connection requirement names.</summary>
public class UpdateConnectionRequirementsRequest
{
    public List<string>? Requirements { get; set; }
}
