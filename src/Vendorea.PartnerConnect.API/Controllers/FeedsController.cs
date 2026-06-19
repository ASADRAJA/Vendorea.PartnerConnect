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
[Vendorea.PartnerConnect.Api.Authorization.RequireScope(ApiScopes.FeedsRead)]
public class FeedsController : ControllerBase
{
    private readonly IFeedProcessingService _feedProcessingService;
    private readonly IPriceFeedBatchRepository _priceBatchRepository;
    private readonly IInventoryFeedBatchRepository _inventoryBatchRepository;
    private readonly IContentSyncJobRepository _contentSyncRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ILogger<FeedsController> _logger;

    public FeedsController(
        IFeedProcessingService feedProcessingService,
        IPriceFeedBatchRepository priceBatchRepository,
        IInventoryFeedBatchRepository inventoryBatchRepository,
        IContentSyncJobRepository contentSyncRepository,
        ITradingPartnerRepository partnerRepository,
        ILogger<FeedsController> logger)
    {
        _feedProcessingService = feedProcessingService;
        _priceBatchRepository = priceBatchRepository;
        _inventoryBatchRepository = inventoryBatchRepository;
        _contentSyncRepository = contentSyncRepository;
        _partnerRepository = partnerRepository;
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
    /// Triggers a manual feed sync for a trading partner.
    /// </summary>
    [HttpPost("partner/{tradingPartnerId:int}/sync")]
    [ProducesResponseType(typeof(SyncTriggerResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerSync(
        int tradingPartnerId,
        [FromBody] TriggerFeedSyncCommand command,
        CancellationToken cancellationToken)
    {
        var partner = await _partnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);

        if (partner is null)
        {
            return NotFound();
        }

        if (partner.Status != TradingPartnerStatus.Active)
        {
            return BadRequest(new { Error = "Trading partner must be active to trigger sync" });
        }

        _logger.LogInformation(
            "Triggering manual {FeedType} sync for partner {TradingPartnerId}",
            command.FeedType, tradingPartnerId);

        try
        {
            int? batchId = null;

            if (command.FeedType == FeedType.Price)
            {
                var batch = await _feedProcessingService.ProcessPriceFeedAsync(tradingPartnerId, cancellationToken);
                await _priceBatchRepository.AddAsync(batch, cancellationToken);
                batchId = batch.Id;
            }
            else
            {
                var batch = await _feedProcessingService.ProcessInventoryFeedAsync(tradingPartnerId, cancellationToken);
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
            _logger.LogError(ex, "Error triggering feed sync for partner {TradingPartnerId}", tradingPartnerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new SyncTriggerResponse(
                Accepted: false,
                Message: ex.Message,
                BatchId: null));
        }
    }

    /// <summary>
    /// Schedules a content sync for a trading partner.
    /// </summary>
    [HttpPost("partner/{tradingPartnerId:int}/content-sync")]
    [ProducesResponseType(typeof(ContentSyncJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScheduleContentSync(
        int tradingPartnerId,
        [FromBody] ScheduleContentSyncCommand command,
        CancellationToken cancellationToken)
    {
        var partner = await _partnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);

        if (partner is null)
        {
            return NotFound();
        }

        if (partner.Status != TradingPartnerStatus.Active)
        {
            return BadRequest(new { Error = "Trading partner must be active to schedule content sync" });
        }

        var job = new ContentSyncJob
        {
            TradingPartnerId = tradingPartnerId,
            SyncType = command.SyncType,
            Status = ContentSyncStatus.Scheduled,
            ScheduledAt = command.ScheduleAt ?? DateTime.UtcNow,
            TriggerSource = "API"
        };

        var created = await _contentSyncRepository.AddAsync(job, cancellationToken);

        _logger.LogInformation(
            "Scheduled content sync job {JobId} for partner {TradingPartnerId}",
            created.Id, tradingPartnerId);

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
