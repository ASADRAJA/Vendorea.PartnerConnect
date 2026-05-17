using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.API.Controllers;

/// <summary>
/// API controller for managing enhanced content imports.
/// Content is supplier-specific but shared across all merchants.
/// </summary>
[ApiController]
[Route("api/v1/admin/spr/content/imports")]
public class SprContentImportController : ControllerBase
{
    private readonly ISprContentImportService _importService;
    private readonly ISprContentUploadRepository _uploadRepository;
    private readonly ISprProductContentRepository _productContentRepository;
    private readonly ISprCategoryRepository _categoryRepository;
    private readonly ITradingPartnerRepository _tradingPartnerRepository;
    private readonly ILogger<SprContentImportController> _logger;

    public SprContentImportController(
        ISprContentImportService importService,
        ISprContentUploadRepository uploadRepository,
        ISprProductContentRepository productContentRepository,
        ISprCategoryRepository categoryRepository,
        ITradingPartnerRepository tradingPartnerRepository,
        ILogger<SprContentImportController> logger)
    {
        _importService = importService;
        _uploadRepository = uploadRepository;
        _productContentRepository = productContentRepository;
        _categoryRepository = categoryRepository;
        _tradingPartnerRepository = tradingPartnerRepository;
        _logger = logger;
    }

    /// <summary>
    /// Imports enhanced content from a zip file.
    /// Content is supplier-specific but shared across all merchants subscribed to that supplier.
    /// </summary>
    /// <param name="file">The content zip file.</param>
    /// <param name="tradingPartnerId">Trading partner (supplier) ID.</param>
    /// <param name="contentVersion">Content version identifier.</param>
    /// <param name="locale">Locale (default: EN_US).</param>
    [HttpPost]
    [ProducesResponseType(typeof(ContentImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [RequestSizeLimit(500_000_000)] // 500MB limit for content zips
    public async Task<IActionResult> Import(
        IFormFile file,
        [FromQuery] int tradingPartnerId,
        [FromQuery] string contentVersion,
        [FromQuery] string locale = "EN_US",
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        if (tradingPartnerId <= 0)
        {
            return BadRequest("Trading partner (supplier) is required");
        }

        if (string.IsNullOrWhiteSpace(contentVersion))
        {
            return BadRequest("Content version is required");
        }

        // Get the specified trading partner
        var tradingPartner = await _tradingPartnerRepository.GetByIdAsync(tradingPartnerId, cancellationToken);
        if (tradingPartner == null)
        {
            return BadRequest($"Trading partner {tradingPartnerId} not found");
        }

        _logger.LogInformation(
            "Starting content import: Version={Version}, Locale={Locale}, File={FileName}, Size={Size}",
            contentVersion, locale, file.FileName, file.Length);

        try
        {
            using var stream = file.OpenReadStream();
            var upload = await _importService.ImportFromZipAsync(
                tradingPartner.Id,
                stream,
                file.FileName,
                contentVersion,
                locale,
                progress => _logger.LogDebug(
                    "Import progress: {Phase} - {Processed}/{Total}",
                    progress.CurrentPhase, progress.ProcessedProducts, progress.TotalProducts),
                cancellationToken);

            return Ok(new ContentImportResultDto
            {
                UploadId = upload.Id,
                Status = upload.Status.ToString(),
                TotalProducts = upload.TotalProducts,
                ProcessedProducts = upload.ProcessedProducts,
                ErrorProducts = upload.ErrorProducts,
                ContentVersion = upload.ContentVersion,
                LocaleId = upload.LocaleId,
                StartedAt = upload.ProcessingStartedAt,
                CompletedAt = upload.ProcessingCompletedAt
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already been imported"))
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets import history. Optionally filter by trading partner.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ContentImportSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int? tradingPartnerId = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var uploads = await _uploadRepository.GetAllAsync(
            limit, tradingPartnerId, cancellationToken);

        // Get trading partner names for display
        var partnerIds = uploads.Select(u => u.TradingPartnerId).Distinct().ToList();
        var partners = new Dictionary<int, string>();
        foreach (var pid in partnerIds)
        {
            var partner = await _tradingPartnerRepository.GetByIdAsync(pid, cancellationToken);
            if (partner != null)
                partners[pid] = partner.Name;
        }

        var result = uploads.Select(u => new ContentImportSummaryDto
        {
            UploadId = u.Id,
            TradingPartnerId = u.TradingPartnerId,
            TradingPartnerName = partners.GetValueOrDefault(u.TradingPartnerId, "Unknown"),
            ContentVersion = u.ContentVersion,
            LocaleId = u.LocaleId,
            Status = u.Status.ToString(),
            TotalProducts = u.TotalProducts,
            ProcessedProducts = u.ProcessedProducts,
            ErrorProducts = u.ErrorProducts,
            FileName = u.ZipFileName,
            UploadedAt = u.UploadedAt,
            CompletedAt = u.ProcessingCompletedAt,
            PushedToM360At = u.PushedToM360At
        });

        return Ok(result);
    }

    /// <summary>
    /// Gets the status of a specific import.
    /// </summary>
    [HttpGet("{uploadId:int}/status")]
    [ProducesResponseType(typeof(ContentImportStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return NotFound($"Upload {uploadId} not found");
        }

        // Try to get live progress if import is in progress
        var progress = await _importService.GetImportStatusAsync(uploadId, cancellationToken);

        return Ok(new ContentImportStatusDto
        {
            UploadId = upload.Id,
            Status = upload.Status.ToString(),
            CurrentPhase = progress?.CurrentPhase ?? GetPhaseFromStatus(upload.Status),
            TotalProducts = progress?.TotalProducts ?? upload.TotalProducts,
            ProcessedProducts = progress?.ProcessedProducts ?? upload.ProcessedProducts,
            ErrorProducts = progress?.ErrorProducts ?? upload.ErrorProducts,
            TotalFeatures = progress?.TotalFeatures ?? 0,
            ProcessedFeatures = progress?.ProcessedFeatures ?? 0,
            TotalRelationships = progress?.TotalRelationships ?? 0,
            ProcessedRelationships = progress?.ProcessedRelationships ?? 0,
            PercentComplete = progress?.PercentComplete ?? GetPercentFromStatus(upload.Status),
            Errors = progress?.Errors ?? new List<string>(),
            StartedAt = upload.ProcessingStartedAt,
            CompletedAt = upload.ProcessingCompletedAt,
            ErrorDetails = upload.ErrorDetails
        });
    }

    /// <summary>
    /// Cancels an in-progress import.
    /// </summary>
    [HttpPost("{uploadId:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return NotFound($"Upload {uploadId} not found");
        }

        await _importService.CancelImportAsync(uploadId, cancellationToken);
        return Ok(new { message = "Import cancelled" });
    }

    /// <summary>
    /// Deletes content from a specific upload.
    /// </summary>
    [HttpDelete("{uploadId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return NotFound($"Upload {uploadId} not found");
        }

        await _importService.DeleteContentByUploadAsync(uploadId, cancellationToken);
        return Ok(new { message = "Content deleted" });
    }

    /// <summary>
    /// Pushes imported content to Merchant360.
    /// </summary>
    [HttpPost("{uploadId:int}/push")]
    [ProducesResponseType(typeof(ContentPushResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PushToMerchant360(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        var upload = await _uploadRepository.GetByIdAsync(uploadId, cancellationToken);
        if (upload == null)
        {
            return NotFound($"Upload {uploadId} not found");
        }

        _logger.LogInformation("Starting content push to M360 for upload {UploadId}", uploadId);

        var result = await _importService.PushToMerchant360Async(uploadId, cancellationToken);

        if (!result.Success)
        {
            _logger.LogError("Content push failed: {Error}", result.ErrorMessage);
            return BadRequest(new ContentPushResultDto
            {
                Success = false,
                UploadId = uploadId,
                ErrorMessage = result.ErrorMessage,
                Errors = result.Errors
            });
        }

        return Ok(new ContentPushResultDto
        {
            Success = true,
            UploadId = uploadId,
            RecordsPushed = result.RecordsPushed,
            RecordsCreated = result.RecordsCreated,
            RecordsUpdated = result.RecordsUpdated,
            RecordsSkipped = result.RecordsSkipped,
            BatchCount = result.BatchCount,
            PushedAt = result.PushedAt
        });
    }

    /// <summary>
    /// Validates a zip file without importing.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ContentValidationResult), StatusCodes.Status200OK)]
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> Validate(
        IFormFile file,
        [FromQuery] string locale = "EN_US",
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        using var stream = file.OpenReadStream();
        var result = await _importService.ValidateZipAsync(stream, locale, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Debug endpoint to inspect zip structure.
    /// </summary>
    [HttpPost("inspect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [RequestSizeLimit(500_000_000)]
    public IActionResult InspectZip(
        IFormFile file,
        [FromServices] ISprContentZipExtractor zipExtractor)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        using var stream = file.OpenReadStream();
        var entries = zipExtractor.ListEntries(stream);

        var result = entries.Select(e => new
        {
            e.FullName,
            e.Name,
            e.Length,
            ContentType = e.ContentType.ToString(),
            e.IsNested,
            e.ParentZipName
        }).ToList();

        var basicEntry = entries.FirstOrDefault(e => e.ContentType == Application.Interfaces.SprContentFileType.BasicContent);

        return Ok(new
        {
            TotalEntries = entries.Count,
            BasicContentFound = basicEntry != null,
            BasicContentPath = basicEntry?.FullName,
            Entries = result
        });
    }

    /// <summary>
    /// Gets content statistics for a locale.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ContentStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] string locale = "EN_US",
        CancellationToken cancellationToken = default)
    {
        var productCount = await _productContentRepository.GetCountAsync(locale, cancellationToken);
        var brands = await _productContentRepository.GetDistinctBrandsAsync(locale, cancellationToken);
        var categories = await _categoryRepository.GetAllActiveAsync(cancellationToken);
        var latestUpload = await _uploadRepository.GetLatestCompletedAsync(locale, cancellationToken);

        return Ok(new ContentStatisticsDto
        {
            TotalProducts = productCount,
            TotalBrands = brands.Count,
            TotalCategories = categories.Count,
            LastContentVersion = latestUpload?.ContentVersion,
            LastImportDate = latestUpload?.ProcessingCompletedAt
        });
    }

    private static string GetPhaseFromStatus(Domain.Entities.ContentUploadStatus status)
    {
        return status switch
        {
            Domain.Entities.ContentUploadStatus.Pending => "Pending",
            Domain.Entities.ContentUploadStatus.Extracting => "Extracting files",
            Domain.Entities.ContentUploadStatus.Parsing => "Parsing content",
            Domain.Entities.ContentUploadStatus.Importing => "Importing to database",
            Domain.Entities.ContentUploadStatus.Completed => "Completed",
            Domain.Entities.ContentUploadStatus.PartiallyCompleted => "Completed with errors",
            Domain.Entities.ContentUploadStatus.Failed => "Failed",
            Domain.Entities.ContentUploadStatus.Cancelled => "Cancelled",
            _ => "Unknown"
        };
    }

    private static double GetPercentFromStatus(Domain.Entities.ContentUploadStatus status)
    {
        return status switch
        {
            Domain.Entities.ContentUploadStatus.Completed => 100,
            Domain.Entities.ContentUploadStatus.PartiallyCompleted => 100,
            Domain.Entities.ContentUploadStatus.Failed => 0,
            Domain.Entities.ContentUploadStatus.Cancelled => 0,
            _ => 0
        };
    }
}

/// <summary>
/// Content import result DTO.
/// </summary>
public class ContentImportResultDto
{
    public int UploadId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int ProcessedProducts { get; set; }
    public int ErrorProducts { get; set; }
    public string ContentVersion { get; set; } = string.Empty;
    public string LocaleId { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Content import summary for listing.
/// </summary>
public class ContentImportSummaryDto
{
    public int UploadId { get; set; }
    public int TradingPartnerId { get; set; }
    public string TradingPartnerName { get; set; } = string.Empty;
    public string ContentVersion { get; set; } = string.Empty;
    public string LocaleId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int ProcessedProducts { get; set; }
    public int ErrorProducts { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? PushedToM360At { get; set; }
}

/// <summary>
/// Detailed import status DTO.
/// </summary>
public class ContentImportStatusDto
{
    public int UploadId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrentPhase { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int ProcessedProducts { get; set; }
    public int ErrorProducts { get; set; }
    public int TotalFeatures { get; set; }
    public int ProcessedFeatures { get; set; }
    public int TotalRelationships { get; set; }
    public int ProcessedRelationships { get; set; }
    public double PercentComplete { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorDetails { get; set; }
}

/// <summary>
/// Content statistics DTO.
/// </summary>
public class ContentStatisticsDto
{
    public int TotalProducts { get; set; }
    public int TotalBrands { get; set; }
    public int TotalCategories { get; set; }
    public string? LastContentVersion { get; set; }
    public DateTime? LastImportDate { get; set; }
}

/// <summary>
/// Result of pushing content to Merchant360.
/// </summary>
public class ContentPushResultDto
{
    public bool Success { get; set; }
    public int UploadId { get; set; }
    public int RecordsPushed { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsSkipped { get; set; }
    public int BatchCount { get; set; }
    public DateTime? PushedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();
}
