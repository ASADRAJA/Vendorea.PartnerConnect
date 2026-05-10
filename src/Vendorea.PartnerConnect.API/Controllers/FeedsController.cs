using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.API.Controllers;

/// <summary>
/// API controller for managing feed operations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class FeedsController : ControllerBase
{
    private readonly IFeedProcessingService _feedProcessingService;
    private readonly IPriceFeedBatchRepository _priceBatchRepository;
    private readonly IInventoryFeedBatchRepository _inventoryBatchRepository;
    private readonly IContentSyncJobRepository _contentSyncRepository;
    private readonly IDealerPartnerConnectionRepository _connectionRepository;
    private readonly ILogger<FeedsController> _logger;

    public FeedsController(
        IFeedProcessingService feedProcessingService,
        IPriceFeedBatchRepository priceBatchRepository,
        IInventoryFeedBatchRepository inventoryBatchRepository,
        IContentSyncJobRepository contentSyncRepository,
        IDealerPartnerConnectionRepository connectionRepository,
        ILogger<FeedsController> logger)
    {
        _feedProcessingService = feedProcessingService;
        _priceBatchRepository = priceBatchRepository;
        _inventoryBatchRepository = inventoryBatchRepository;
        _contentSyncRepository = contentSyncRepository;
        _connectionRepository = connectionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets price feed batches for a dealer.
    /// </summary>
    [HttpGet("price/dealer/{dealerId:int}")]
    [ProducesResponseType(typeof(IEnumerable<PriceFeedBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriceFeedBatches(
        int dealerId,
        [FromQuery] int? skip = null,
        [FromQuery] int? take = null,
        CancellationToken cancellationToken = default)
    {
        var batches = await _priceBatchRepository.GetByDealerIdAsync(dealerId, skip, take, cancellationToken);
        var dtos = batches.Select(MapToPriceFeedDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Gets inventory feed batches for a dealer.
    /// </summary>
    [HttpGet("inventory/dealer/{dealerId:int}")]
    [ProducesResponseType(typeof(IEnumerable<InventoryFeedBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryFeedBatches(
        int dealerId,
        [FromQuery] int? skip = null,
        [FromQuery] int? take = null,
        CancellationToken cancellationToken = default)
    {
        var batches = await _inventoryBatchRepository.GetByDealerIdAsync(dealerId, skip, take, cancellationToken);
        var dtos = batches.Select(MapToInventoryFeedDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Gets content sync jobs for a dealer.
    /// </summary>
    [HttpGet("content/dealer/{dealerId:int}")]
    [ProducesResponseType(typeof(IEnumerable<ContentSyncJobDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContentSyncJobs(
        int dealerId,
        [FromQuery] int? skip = null,
        [FromQuery] int? take = null,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _contentSyncRepository.GetByDealerIdAsync(dealerId, skip, take, cancellationToken);
        var dtos = jobs.Select(MapToContentSyncDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Gets a price feed batch by ID.
    /// </summary>
    [HttpGet("price/{id:int}")]
    [ProducesResponseType(typeof(PriceFeedBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPriceFeedBatch(int id, CancellationToken cancellationToken)
    {
        var batch = await _priceBatchRepository.GetByIdAsync(id, cancellationToken);

        if (batch is null)
        {
            return NotFound();
        }

        return Ok(MapToPriceFeedDto(batch));
    }

    /// <summary>
    /// Gets an inventory feed batch by ID.
    /// </summary>
    [HttpGet("inventory/{id:int}")]
    [ProducesResponseType(typeof(InventoryFeedBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInventoryFeedBatch(int id, CancellationToken cancellationToken)
    {
        var batch = await _inventoryBatchRepository.GetByIdAsync(id, cancellationToken);

        if (batch is null)
        {
            return NotFound();
        }

        return Ok(MapToInventoryFeedDto(batch));
    }

    /// <summary>
    /// Gets a content sync job by ID.
    /// </summary>
    [HttpGet("content/{id:int}")]
    [ProducesResponseType(typeof(ContentSyncJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContentSyncJob(int id, CancellationToken cancellationToken)
    {
        var job = await _contentSyncRepository.GetByIdAsync(id, cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        return Ok(MapToContentSyncDto(job));
    }

    /// <summary>
    /// Gets price feed statistics for a dealer.
    /// </summary>
    [HttpGet("price/dealer/{dealerId:int}/statistics")]
    [ProducesResponseType(typeof(FeedStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriceFeedStatistics(
        int dealerId,
        [FromQuery] DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var stats = await _priceBatchRepository.GetStatisticsAsync(dealerId, since, cancellationToken);

        var successRate = stats.TotalBatches > 0
            ? (decimal)stats.CompletedBatches / stats.TotalBatches * 100
            : 100;

        return Ok(new FeedStatisticsDto(
            TotalBatches: stats.TotalBatches,
            TotalItemsProcessed: stats.TotalItemsProcessed,
            TotalItemsUpdated: stats.TotalItemsUpdated,
            TotalErrors: stats.TotalErrors,
            CompletedBatches: stats.CompletedBatches,
            FailedBatches: stats.FailedBatches,
            LastSyncAt: stats.LastSyncAt,
            SuccessRate: successRate));
    }

    /// <summary>
    /// Gets inventory feed statistics for a dealer.
    /// </summary>
    [HttpGet("inventory/dealer/{dealerId:int}/statistics")]
    [ProducesResponseType(typeof(FeedStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryFeedStatistics(
        int dealerId,
        [FromQuery] DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var stats = await _inventoryBatchRepository.GetStatisticsAsync(dealerId, since, cancellationToken);

        var successRate = stats.TotalBatches > 0
            ? (decimal)stats.CompletedBatches / stats.TotalBatches * 100
            : 100;

        return Ok(new FeedStatisticsDto(
            TotalBatches: stats.TotalBatches,
            TotalItemsProcessed: stats.TotalItemsProcessed,
            TotalItemsUpdated: stats.TotalItemsUpdated,
            TotalErrors: stats.TotalErrors,
            CompletedBatches: stats.CompletedBatches,
            FailedBatches: stats.FailedBatches,
            LastSyncAt: stats.LastSyncAt,
            SuccessRate: successRate));
    }

    /// <summary>
    /// Triggers a manual feed sync for a connection.
    /// </summary>
    [HttpPost("connection/{connectionId:int}/sync")]
    [ProducesResponseType(typeof(SyncTriggerResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerSync(
        int connectionId,
        [FromBody] TriggerFeedSyncCommand command,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdAsync(connectionId, cancellationToken);

        if (connection is null)
        {
            return NotFound();
        }

        if (connection.Status != ConnectionStatus.Active)
        {
            return BadRequest(new { Error = "Connection must be active to trigger sync" });
        }

        _logger.LogInformation(
            "Triggering manual {FeedType} sync for connection {ConnectionId}",
            command.FeedType, connectionId);

        try
        {
            int? batchId = null;

            if (command.FeedType == FeedType.Price)
            {
                var batch = await _feedProcessingService.ProcessPriceFeedAsync(connectionId, cancellationToken);
                await _priceBatchRepository.AddAsync(batch, cancellationToken);
                batchId = batch.Id;
            }
            else
            {
                var batch = await _feedProcessingService.ProcessInventoryFeedAsync(connectionId, cancellationToken);
                await _inventoryBatchRepository.AddAsync(batch, cancellationToken);
                batchId = batch.Id;
            }

            return Accepted(new SyncTriggerResponse(
                Accepted: true,
                Message: $"{command.FeedType} feed sync triggered successfully",
                BatchId: batchId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering feed sync for connection {ConnectionId}", connectionId);

            return StatusCode(StatusCodes.Status500InternalServerError, new SyncTriggerResponse(
                Accepted: false,
                Message: ex.Message,
                BatchId: null));
        }
    }

    /// <summary>
    /// Schedules a content sync for a connection.
    /// </summary>
    [HttpPost("connection/{connectionId:int}/content-sync")]
    [ProducesResponseType(typeof(ContentSyncJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScheduleContentSync(
        int connectionId,
        [FromBody] ScheduleContentSyncCommand command,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetByIdAsync(connectionId, cancellationToken);

        if (connection is null)
        {
            return NotFound();
        }

        if (connection.Status != ConnectionStatus.Active)
        {
            return BadRequest(new { Error = "Connection must be active to schedule content sync" });
        }

        var job = new ContentSyncJob
        {
            DealerId = connection.DealerId,
            TradingPartnerId = connection.TradingPartnerId,
            SyncType = command.SyncType,
            Status = ContentSyncStatus.Scheduled,
            ScheduledAt = command.ScheduleAt ?? DateTime.UtcNow,
            TriggerSource = "API"
        };

        var created = await _contentSyncRepository.AddAsync(job, cancellationToken);

        _logger.LogInformation(
            "Scheduled content sync job {JobId} for connection {ConnectionId}",
            created.Id, connectionId);

        return CreatedAtAction(
            nameof(GetContentSyncJob),
            new { id = created.Id },
            MapToContentSyncDto(created));
    }

    private static PriceFeedBatchDto MapToPriceFeedDto(PriceFeedBatch batch)
    {
        return new PriceFeedBatchDto(
            Id: batch.Id,
            PartnerDocumentId: batch.PartnerDocumentId,
            DealerId: batch.DealerId,
            TradingPartnerId: batch.TradingPartnerId,
            TradingPartnerCode: null, // Would need to join with trading partner
            Status: batch.Status,
            TotalItems: batch.TotalItems,
            ProcessedItems: batch.ProcessedItems,
            MatchedItems: batch.MatchedItems,
            UpdatedItems: batch.UpdatedItems,
            SkippedItems: batch.SkippedItems,
            ErrorItems: batch.ErrorItems,
            ReceivedAt: batch.ReceivedAt,
            ProcessingStartedAt: batch.ProcessingStartedAt,
            ProcessingCompletedAt: batch.ProcessingCompletedAt,
            ErrorSummary: batch.ErrorSummary);
    }

    private static InventoryFeedBatchDto MapToInventoryFeedDto(InventoryFeedBatch batch)
    {
        return new InventoryFeedBatchDto(
            Id: batch.Id,
            PartnerDocumentId: batch.PartnerDocumentId,
            DealerId: batch.DealerId,
            TradingPartnerId: batch.TradingPartnerId,
            TradingPartnerCode: null,
            Status: batch.Status,
            TotalItems: batch.TotalItems,
            ProcessedItems: batch.ProcessedItems,
            MatchedItems: batch.MatchedItems,
            UpdatedItems: batch.UpdatedItems,
            SkippedItems: batch.SkippedItems,
            ErrorItems: batch.ErrorItems,
            ReceivedAt: batch.ReceivedAt,
            ProcessingStartedAt: batch.ProcessingStartedAt,
            ProcessingCompletedAt: batch.ProcessingCompletedAt,
            ErrorSummary: batch.ErrorSummary);
    }

    private static ContentSyncJobDto MapToContentSyncDto(ContentSyncJob job)
    {
        return new ContentSyncJobDto(
            Id: job.Id,
            DealerId: job.DealerId,
            TradingPartnerId: job.TradingPartnerId,
            TradingPartnerCode: null,
            SyncType: job.SyncType,
            Status: job.Status,
            TotalProducts: job.TotalProducts,
            ProcessedProducts: job.ProcessedProducts,
            UpdatedProducts: job.UpdatedProducts,
            NewImagesDownloaded: job.NewImagesDownloaded,
            SkippedProducts: job.SkippedProducts,
            ErrorProducts: job.ErrorProducts,
            ScheduledAt: job.ScheduledAt,
            StartedAt: job.StartedAt,
            CompletedAt: job.CompletedAt,
            ErrorDetails: job.ErrorDetails,
            TriggerSource: job.TriggerSource);
    }
}
