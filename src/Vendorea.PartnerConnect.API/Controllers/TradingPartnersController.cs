using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;

namespace Vendorea.PartnerConnect.API.Controllers;

/// <summary>
/// API controller for managing trading partners.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class TradingPartnersController : ControllerBase
{
    private readonly ITradingPartnerService _tradingPartnerService;
    private readonly ILogger<TradingPartnersController> _logger;

    public TradingPartnersController(
        ITradingPartnerService tradingPartnerService,
        ILogger<TradingPartnersController> logger)
    {
        _tradingPartnerService = tradingPartnerService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all trading partners.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TradingPartnerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var partners = await _tradingPartnerService.GetAllAsync(cancellationToken);
        return Ok(partners);
    }

    /// <summary>
    /// Gets a trading partner by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TradingPartnerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var partner = await _tradingPartnerService.GetByIdAsync(id, cancellationToken);

        if (partner is null)
        {
            return NotFound();
        }

        return Ok(partner);
    }

    /// <summary>
    /// Gets a trading partner by code.
    /// </summary>
    [HttpGet("by-code/{code}")]
    [ProducesResponseType(typeof(TradingPartnerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken)
    {
        var partner = await _tradingPartnerService.GetByCodeAsync(code, cancellationToken);

        if (partner is null)
        {
            return NotFound();
        }

        return Ok(partner);
    }

    /// <summary>
    /// Creates a new trading partner.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TradingPartnerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTradingPartnerCommand command,
        CancellationToken cancellationToken)
    {
        var partner = await _tradingPartnerService.CreateAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = partner.Id }, partner);
    }
}
