using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.API.Controllers;

/// <summary>
/// API controller for managing dealer-partner connections.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class DealerConnectionsController : ControllerBase
{
    private readonly IDealerPartnerConnectionRepository _connectionRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly IPriceFeedBatchRepository _priceBatchRepository;
    private readonly IInventoryFeedBatchRepository _inventoryBatchRepository;
    private readonly IEnumerable<IPartnerAdapter> _partnerAdapters;
    private readonly ILogger<DealerConnectionsController> _logger;

    public DealerConnectionsController(
        IDealerPartnerConnectionRepository connectionRepository,
        ITradingPartnerRepository partnerRepository,
        IPriceFeedBatchRepository priceBatchRepository,
        IInventoryFeedBatchRepository inventoryBatchRepository,
        IEnumerable<IPartnerAdapter> partnerAdapters,
        ILogger<DealerConnectionsController> logger)
    {
        _connectionRepository = connectionRepository;
        _partnerRepository = partnerRepository;
        _priceBatchRepository = priceBatchRepository;
        _inventoryBatchRepository = inventoryBatchRepository;
        _partnerAdapters = partnerAdapters;
        _logger = logger;
    }

    /// <summary>
    /// Gets all connections for a dealer.
    /// </summary>
    [HttpGet("dealer/{dealerId:int}")]
    [ProducesResponseType(typeof(IEnumerable<DealerConnectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDealerId(int dealerId, CancellationToken cancellationToken)
    {
        var connections = await _connectionRepository.GetByDealerIdAsync(dealerId, cancellationToken);
        var dtos = connections.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Gets a connection by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DealerConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);

        if (connection is null)
        {
            return NotFound();
        }

        return Ok(MapToDto(connection));
    }

    /// <summary>
    /// Gets connection statistics.
    /// </summary>
    [HttpGet("{id:int}/statistics")]
    [ProducesResponseType(typeof(ConnectionStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatistics(int id, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);

        if (connection is null)
        {
            return NotFound();
        }

        var priceStats = await _priceBatchRepository.GetStatisticsAsync(connection.DealerId, cancellationToken: cancellationToken);
        var inventoryStats = await _inventoryBatchRepository.GetStatisticsAsync(connection.DealerId, cancellationToken: cancellationToken);

        var totalSuccess = priceStats.CompletedBatches + inventoryStats.CompletedBatches;
        var totalBatches = priceStats.TotalBatches + inventoryStats.TotalBatches;

        var statistics = new ConnectionStatisticsDto(
            TotalDocumentsProcessed: priceStats.TotalItemsProcessed + inventoryStats.TotalItemsProcessed,
            TotalPriceFeedBatches: priceStats.TotalBatches,
            TotalInventoryFeedBatches: inventoryStats.TotalBatches,
            TotalContentSyncJobs: 0, // TODO: Add content sync stats
            TotalErrors: priceStats.TotalErrors + inventoryStats.TotalErrors,
            LastSuccessfulSync: priceStats.LastSyncAt > inventoryStats.LastSyncAt ? priceStats.LastSyncAt : inventoryStats.LastSyncAt,
            SuccessRate: totalBatches > 0 ? (decimal)totalSuccess / totalBatches * 100 : 100);

        return Ok(statistics);
    }

    /// <summary>
    /// Creates a new dealer-partner connection.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DealerConnectionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDealerConnectionCommand command,
        CancellationToken cancellationToken)
    {
        // Validate partner exists
        var partner = await _partnerRepository.GetByIdAsync(command.TradingPartnerId, cancellationToken);
        if (partner is null)
        {
            return BadRequest(new { Error = "Trading partner not found" });
        }

        // Check for existing connection
        var existing = await _connectionRepository.GetByDealerAndPartnerAsync(
            command.DealerId, command.TradingPartnerId, cancellationToken);
        if (existing is not null)
        {
            return BadRequest(new { Error = "Connection already exists for this dealer and partner" });
        }

        var connection = new DealerPartnerConnection
        {
            DealerId = command.DealerId,
            TradingPartnerId = command.TradingPartnerId,
            ExternalAccountId = command.ExternalAccountId,
            CredentialsJson = command.CredentialsJson,
            ConfigurationJson = command.ConfigurationJson,
            Status = ConnectionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _connectionRepository.AddAsync(connection, cancellationToken);

        _logger.LogInformation(
            "Created dealer connection {ConnectionId} for dealer {DealerId} with partner {PartnerId}",
            created.Id, command.DealerId, command.TradingPartnerId);

        // Reload with navigation properties
        created = await _connectionRepository.GetByIdAsync(created.Id, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created!.Id }, MapToDto(created));
    }

    /// <summary>
    /// Updates a dealer-partner connection.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(DealerConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateDealerConnectionCommand command,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);

        if (connection is null)
        {
            return NotFound();
        }

        if (command.ExternalAccountId is not null)
        {
            connection.ExternalAccountId = command.ExternalAccountId;
        }

        if (command.ConfigurationJson is not null)
        {
            connection.ConfigurationJson = command.ConfigurationJson;
        }

        if (command.Status.HasValue)
        {
            connection.Status = command.Status.Value;
        }

        connection.UpdatedAt = DateTime.UtcNow;

        await _connectionRepository.UpdateAsync(connection, cancellationToken);

        _logger.LogInformation("Updated dealer connection {ConnectionId}", id);

        return Ok(MapToDto(connection));
    }

    /// <summary>
    /// Updates connection credentials.
    /// </summary>
    [HttpPut("{id:int}/credentials")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCredentials(
        int id,
        [FromBody] UpdateConnectionCredentialsCommand command,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);

        if (connection is null)
        {
            return NotFound();
        }

        connection.CredentialsJson = command.CredentialsJson;
        connection.UpdatedAt = DateTime.UtcNow;

        await _connectionRepository.UpdateAsync(connection, cancellationToken);

        _logger.LogInformation("Updated credentials for connection {ConnectionId}", id);

        return NoContent();
    }

    /// <summary>
    /// Tests a connection to verify it's working.
    /// </summary>
    [HttpPost("{id:int}/test")]
    [ProducesResponseType(typeof(TestConnectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestConnection(int id, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);

        if (connection is null)
        {
            return NotFound();
        }

        // Find the appropriate adapter
        var adapter = _partnerAdapters.FirstOrDefault(a =>
            a.PartnerCode.Equals(connection.TradingPartner?.Code, StringComparison.OrdinalIgnoreCase));

        if (adapter is null)
        {
            return Ok(new TestConnectionResponse(
                Success: false,
                ErrorMessage: $"No adapter found for partner {connection.TradingPartner?.Code}",
                TestedAt: DateTime.UtcNow));
        }

        try
        {
            var success = await adapter.TestConnectionAsync(connection, cancellationToken);

            return Ok(new TestConnectionResponse(
                Success: success,
                ErrorMessage: success ? null : "Connection test failed",
                TestedAt: DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for connection {ConnectionId}", id);

            return Ok(new TestConnectionResponse(
                Success: false,
                ErrorMessage: ex.Message,
                TestedAt: DateTime.UtcNow));
        }
    }

    /// <summary>
    /// Activates a connection.
    /// </summary>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(typeof(DealerConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);

        if (connection is null)
        {
            return NotFound();
        }

        if (connection.Status == ConnectionStatus.Active)
        {
            return BadRequest(new { Error = "Connection is already active" });
        }

        connection.Status = ConnectionStatus.Active;
        connection.UpdatedAt = DateTime.UtcNow;

        await _connectionRepository.UpdateAsync(connection, cancellationToken);

        _logger.LogInformation("Activated dealer connection {ConnectionId}", id);

        return Ok(MapToDto(connection));
    }

    /// <summary>
    /// Deactivates a connection.
    /// </summary>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(typeof(DealerConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdAsync(id, cancellationToken);

        if (connection is null)
        {
            return NotFound();
        }

        connection.Status = ConnectionStatus.Inactive;
        connection.UpdatedAt = DateTime.UtcNow;

        await _connectionRepository.UpdateAsync(connection, cancellationToken);

        _logger.LogInformation("Deactivated dealer connection {ConnectionId}", id);

        return Ok(MapToDto(connection));
    }

    private static DealerConnectionDto MapToDto(DealerPartnerConnection connection)
    {
        return new DealerConnectionDto(
            Id: connection.Id,
            DealerId: connection.DealerId,
            TradingPartnerId: connection.TradingPartnerId,
            TradingPartnerCode: connection.TradingPartner?.Code,
            TradingPartnerName: connection.TradingPartner?.Name,
            ExternalAccountId: connection.ExternalAccountId,
            Status: connection.Status,
            LastSyncAt: connection.LastSyncAt,
            LastSuccessfulSyncAt: connection.LastSuccessfulSyncAt,
            CreatedAt: connection.CreatedAt,
            UpdatedAt: connection.UpdatedAt);
    }
}
