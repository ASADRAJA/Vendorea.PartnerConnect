using FluentFTP;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Api.Controllers.Admin;

/// <summary>
/// Admin controller for SPR FTP content ingestion configuration and management.
/// </summary>
[ApiController]
[Route("api/admin/ftp-ingestion")]
public class AdminFtpIngestionController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IFtpIngestionRunRepository _runRepository;
    private readonly IPartnerIngestionConfigRepository _configRepository;
    private readonly ILogger<AdminFtpIngestionController> _logger;

    public AdminFtpIngestionController(
        IConfiguration configuration,
        IFtpIngestionRunRepository runRepository,
        IPartnerIngestionConfigRepository configRepository,
        ILogger<AdminFtpIngestionController> logger)
    {
        _configuration = configuration;
        _runRepository = runRepository;
        _configRepository = configRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current FTP ingestion configuration.
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig([FromQuery] string partnerCode = "SPR", CancellationToken cancellationToken = default)
    {
        // Try to load from database first
        var dbConfig = await _configRepository.GetByPartnerCodeAsync(partnerCode, cancellationToken);

        if (dbConfig != null)
        {
            return Ok(new FtpIngestionConfigResponse
            {
                FtpHost = dbConfig.FtpHost,
                FtpPort = dbConfig.FtpPort,
                FtpUsername = dbConfig.FtpUsername,
                FtpPassword = dbConfig.FtpPassword, // Return actual password so UI can use it
                LocalDownloadPath = dbConfig.LocalDownloadPath,
                Locale = dbConfig.Locale,
                DatabaseType = dbConfig.DatabaseType,
                Enabled = dbConfig.Enabled,
                EnableScheduledRun = dbConfig.EnableScheduledRun,
                ScheduledRunHourUtc = dbConfig.ScheduledRunHourUtc,
                CheckIntervalMinutes = dbConfig.CheckIntervalMinutes,
                ConnectionTimeoutSeconds = dbConfig.ConnectionTimeoutSeconds,
                BulkInsertBatchSize = dbConfig.BulkInsertBatchSize,
                CleanupAfterImport = dbConfig.CleanupAfterImport
            });
        }

        // Fall back to appsettings.json
        var section = _configuration.GetSection("SprContentIngestion");
        var config = new FtpIngestionConfigResponse
        {
            FtpHost = section["FtpHost"] ?? "ftp.etilize.com",
            FtpPort = int.TryParse(section["FtpPort"], out var port) ? port : 21,
            FtpUsername = section["FtpUsername"] ?? "",
            FtpPassword = section["FtpPassword"] ?? "",
            LocalDownloadPath = section["LocalDownloadPath"] ?? Path.GetTempPath(),
            Locale = section["Locale"] ?? "EN_US",
            DatabaseType = section["DatabaseType"] ?? "mssql",
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            EnableScheduledRun = bool.TryParse(section["EnableScheduledRun"], out var scheduled) && scheduled,
            ScheduledRunHourUtc = int.TryParse(section["ScheduledRunHourUtc"], out var hour) ? hour : 2,
            CheckIntervalMinutes = int.TryParse(section["CheckIntervalMinutes"], out var interval) ? interval : 60,
            ConnectionTimeoutSeconds = int.TryParse(section["ConnectionTimeoutSeconds"], out var timeout) ? timeout : 30,
            BulkInsertBatchSize = int.TryParse(section["BulkInsertBatchSize"], out var batch) ? batch : 10000,
            CleanupAfterImport = bool.TryParse(section["CleanupAfterImport"], out var cleanup) && cleanup
        };

        return Ok(config);
    }

    /// <summary>
    /// Updates the FTP ingestion configuration.
    /// </summary>
    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig([FromBody] FtpIngestionConfigRequest request, [FromQuery] string partnerCode = "SPR", CancellationToken cancellationToken = default)
    {
        var config = new PartnerIngestionConfig
        {
            PartnerCode = partnerCode,
            FtpHost = request.FtpHost,
            FtpPort = request.FtpPort,
            FtpUsername = request.FtpUsername,
            FtpPassword = request.FtpPassword,
            LocalDownloadPath = request.LocalDownloadPath,
            Locale = request.Locale,
            DatabaseType = request.DatabaseType,
            Enabled = request.Enabled,
            EnableScheduledRun = request.EnableScheduledRun,
            ScheduledRunHourUtc = request.ScheduledRunHourUtc,
            CheckIntervalMinutes = request.CheckIntervalMinutes,
            ConnectionTimeoutSeconds = request.ConnectionTimeoutSeconds,
            BulkInsertBatchSize = request.BulkInsertBatchSize,
            CleanupAfterImport = request.CleanupAfterImport
        };

        await _configRepository.SaveAsync(config, cancellationToken);

        _logger.LogInformation("FTP ingestion configuration saved for partner {PartnerCode}", partnerCode);

        return Ok(new { message = "Configuration saved successfully." });
    }

    /// <summary>
    /// Tests the FTP connection with the provided configuration.
    /// </summary>
    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection([FromBody] FtpIngestionConfigRequest? request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrEmpty(request.FtpHost) || string.IsNullOrEmpty(request.FtpUsername))
        {
            return BadRequest(new FtpConnectionTestResponse
            {
                Success = false,
                ErrorMessage = "FTP host and username are required"
            });
        }

        try
        {
            // Create a temporary FTP client with the provided credentials
            using var client = new AsyncFtpClient(
                request.FtpHost,
                request.FtpUsername,
                request.FtpPassword,
                request.FtpPort);

            // Use plain FTP - many traditional FTP servers don't support FTPS
            client.Config.EncryptionMode = FtpEncryptionMode.None;
            client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
            client.Config.ConnectTimeout = (request.ConnectionTimeoutSeconds > 0 ? request.ConnectionTimeoutSeconds : 30) * 1000;
            client.Config.ReadTimeout = 60000;

            await client.Connect(cancellationToken);
            _logger.LogInformation("FTP connection test successful: {Host}", request.FtpHost);

            // Try to list files in the root directory
            var files = await client.GetListing("/", cancellationToken);
            var fileCount = files?.Length ?? 0;

            await client.Disconnect(cancellationToken);

            return Ok(new FtpConnectionTestResponse
            {
                Success = true,
                FilesFound = fileCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FTP connection test failed: {Host}", request.FtpHost);
            return Ok(new FtpConnectionTestResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Gets the current ingestion status.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var lastRun = await _runRepository.GetLastRunAsync(cancellationToken);

        var section = _configuration.GetSection("SprContentIngestion");
        var enableScheduled = bool.TryParse(section["EnableScheduledRun"], out var scheduled) && scheduled;
        var scheduledHour = int.TryParse(section["ScheduledRunHourUtc"], out var hour) ? hour : 2;

        DateTime? nextScheduledRun = null;
        if (enableScheduled)
        {
            var now = DateTime.UtcNow;
            nextScheduledRun = new DateTime(now.Year, now.Month, now.Day, scheduledHour, 0, 0, DateTimeKind.Utc);
            if (nextScheduledRun <= now)
            {
                nextScheduledRun = nextScheduledRun.Value.AddDays(1);
            }
        }

        var isRunning = lastRun != null && (lastRun.Status == "Queued" || lastRun.Status == "Running");

        return Ok(new FtpIngestionStatusResponse
        {
            IsRunning = isRunning,
            LastRunAt = lastRun?.StartedAt,
            LastRunSuccess = lastRun?.Success,
            NextScheduledRun = nextScheduledRun,
            CurrentPhase = isRunning ? lastRun?.Phase : null
        });
    }

    /// <summary>
    /// Manually queues an FTP ingestion run. The heavy Download → Import → Transform pipeline is
    /// drained by the BackgroundWorkers <c>FtpIngestionQueueWorker</c>, so this returns immediately.
    /// </summary>
    [HttpPost("run")]
    public async Task<IActionResult> RunIngestion([FromQuery] string partnerCode = "SPR", CancellationToken cancellationToken = default)
    {
        // Load configuration from database
        var dbConfig = await _configRepository.GetByPartnerCodeAsync(partnerCode, cancellationToken);
        if (dbConfig == null || string.IsNullOrEmpty(dbConfig.FtpUsername))
        {
            return BadRequest(new { message = "FTP configuration not found. Please save your settings first." });
        }

        // Reject if a run is already Queued or Running so we don't stack concurrent ingestions.
        var queued = await _runRepository.GetByStatusAsync("Queued", 1, cancellationToken);
        var running = await _runRepository.GetByStatusAsync("Running", 1, cancellationToken);
        if (queued.Count > 0 || running.Count > 0)
        {
            return Conflict(new { message = "An ingestion is already queued or running" });
        }

        // Create a Queued run record; the queue worker will claim and process it.
        var runRecord = new FtpIngestionRun
        {
            StartedAt = DateTime.UtcNow,
            Status = "Queued",
            Phase = "Queued",
            TriggeredBy = "Manual"
        };
        await _runRepository.SaveRunAsync(runRecord, cancellationToken);

        return Accepted(new
        {
            runRecord.Id,
            Status = "Queued",
            Message = "Ingestion queued; the worker will process it."
        });
    }

    /// <summary>
    /// Gets the ingestion run history.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var runs = await _runRepository.GetRunHistoryAsync(limit, cancellationToken);
        return Ok(runs.Select(MapToResponse));
    }

    /// <summary>
    /// Lists files/directories on the FTP server for exploration.
    /// </summary>
    [HttpGet("ftp-browse")]
    public async Task<IActionResult> BrowseFtp([FromQuery] string path = "/", [FromQuery] string partnerCode = "SPR", CancellationToken cancellationToken = default)
    {
        var dbConfig = await _configRepository.GetByPartnerCodeAsync(partnerCode, cancellationToken);
        if (dbConfig == null || string.IsNullOrEmpty(dbConfig.FtpUsername))
        {
            return BadRequest(new { message = "FTP configuration not found. Please save your settings first." });
        }

        try
        {
            using var client = new AsyncFtpClient(
                dbConfig.FtpHost,
                dbConfig.FtpUsername,
                dbConfig.FtpPassword,
                dbConfig.FtpPort);

            // Use plain FTP - many traditional FTP servers don't support FTPS
            client.Config.EncryptionMode = FtpEncryptionMode.None;
            client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;

            await client.Connect(cancellationToken);

            var items = await client.GetListing(path, cancellationToken);

            await client.Disconnect(cancellationToken);

            var result = items?.Select(i => new FtpBrowseItem
            {
                Name = i.Name,
                FullPath = i.FullName,
                Type = i.Type.ToString(),
                Size = i.Size,
                Modified = i.Modified
            }).OrderBy(i => i.Type).ThenBy(i => i.Name).ToList() ?? new List<FtpBrowseItem>();

            return Ok(new
            {
                CurrentPath = path,
                Items = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FTP browse failed for path: {Path}", path);
            return Ok(new
            {
                CurrentPath = path,
                Error = ex.Message,
                Items = new List<FtpBrowseItem>()
            });
        }
    }

    private static string MaskPassword(string? password)
    {
        if (string.IsNullOrEmpty(password)) return "";
        if (password.Length <= 4) return "****";
        return password.Substring(0, 2) + new string('*', password.Length - 4) + password.Substring(password.Length - 2);
    }

    private static FtpIngestionRunResponse MapToResponse(FtpIngestionRun run)
    {
        return new FtpIngestionRunResponse
        {
            Id = run.Id,
            Success = run.Success,
            Status = run.Status,
            Phase = run.Phase,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Duration = run.CompletedAt.HasValue
                ? (run.CompletedAt.Value - run.StartedAt).ToString(@"hh\:mm\:ss")
                : "-",
            ProductsTransformed = run.ProductsTransformed,
            CategoriesTransformed = run.CategoriesTransformed,
            FeaturesTransformed = run.FeaturesTransformed,
            RelationshipsTransformed = run.RelationshipsTransformed,
            SpecificationsTransformed = run.SpecificationsTransformed,
            FilesDownloaded = run.FilesDownloaded,
            BytesDownloaded = run.BytesDownloaded,
            TablesImported = run.TablesImported,
            RowsImported = run.RowsImported,
            Errors = run.Errors
        };
    }
}

// Request/Response DTOs
public class FtpIngestionConfigRequest
{
    public string FtpHost { get; set; } = string.Empty;
    public int FtpPort { get; set; } = 21;
    public string FtpUsername { get; set; } = string.Empty;
    public string FtpPassword { get; set; } = string.Empty;
    public string LocalDownloadPath { get; set; } = string.Empty;
    public string Locale { get; set; } = "EN_US";
    public string DatabaseType { get; set; } = "mssql";
    public bool Enabled { get; set; }
    public bool EnableScheduledRun { get; set; }
    public int ScheduledRunHourUtc { get; set; }
    public int CheckIntervalMinutes { get; set; }
    public int ConnectionTimeoutSeconds { get; set; }
    public int BulkInsertBatchSize { get; set; }
    public bool CleanupAfterImport { get; set; }
}

public class FtpIngestionConfigResponse : FtpIngestionConfigRequest { }

public class FtpConnectionTestResponse
{
    public bool Success { get; set; }
    public int FilesFound { get; set; }
    public string? ErrorMessage { get; set; }
}

public class FtpIngestionStatusResponse
{
    public bool IsRunning { get; set; }
    public DateTime? LastRunAt { get; set; }
    public bool? LastRunSuccess { get; set; }
    public DateTime? NextScheduledRun { get; set; }
    public string? CurrentPhase { get; set; }
}

public class FtpIngestionRunResponse
{
    public int Id { get; set; }
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Phase { get; set; }
    public string? Message { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Duration { get; set; } = string.Empty;
    public int ProductsTransformed { get; set; }
    public int CategoriesTransformed { get; set; }
    public int FeaturesTransformed { get; set; }
    public int RelationshipsTransformed { get; set; }
    public int SpecificationsTransformed { get; set; }
    public int FilesDownloaded { get; set; }
    public long BytesDownloaded { get; set; }
    public int TablesImported { get; set; }
    public long RowsImported { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class FtpBrowseItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime Modified { get; set; }
}
