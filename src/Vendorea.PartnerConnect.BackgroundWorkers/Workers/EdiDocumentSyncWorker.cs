using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Background worker that periodically syncs EDI documents from trading partners.
/// Polls SFTP for inbound documents and sends pending outbound responses.
/// </summary>
public class EdiDocumentSyncWorker : BackgroundService
{
    private readonly ILogger<EdiDocumentSyncWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public EdiDocumentSyncWorker(
        ILogger<EdiDocumentSyncWorker> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue<int>("Workers:EdiDocumentSync:IntervalMinutes", 15);
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        var maxConcurrentConnections = _configuration.GetValue<int>("Workers:EdiDocumentSync:MaxConcurrentConnections", 3);
        var initialDelaySeconds = _configuration.GetValue<int>("Workers:EdiDocumentSync:InitialDelaySeconds", 60);
        var enabled = _configuration.GetValue<bool>("Workers:EdiDocumentSync:Enabled", true);

        if (!enabled)
        {
            _logger.LogInformation("EDI Document Sync Worker is disabled");
            return;
        }

        _logger.LogInformation(
            "EDI Document Sync Worker starting with interval: {Interval} minutes, max concurrent: {MaxConcurrent}",
            intervalMinutes, maxConcurrentConnections);

        // Initial delay to allow services to fully start
        await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEdiDocumentsAsync(maxConcurrentConnections, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing EDI documents");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("EDI Document Sync Worker stopping");
    }

    private async Task ProcessEdiDocumentsAsync(int maxConcurrent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting EDI document sync at {Time}", DateTimeOffset.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var connectionRepo = scope.ServiceProvider.GetRequiredService<IDealerPartnerConnectionRepository>();
        var ediProcessingService = scope.ServiceProvider.GetRequiredService<IEdiDocumentProcessingService>();

        var connections = await connectionRepo.GetActiveConnectionsAsync(cancellationToken);

        // Filter to connections that support EDI (SPR partner with EDI configured)
        var ediConnections = connections
            .Where(c => c.TradingPartner?.Code == "SPR")
            .Where(c => IsEdiEnabled(c))
            .ToList();

        if (ediConnections.Count == 0)
        {
            _logger.LogDebug("No EDI-enabled connections found");
            return;
        }

        _logger.LogInformation("Processing EDI for {Count} connection(s)", ediConnections.Count);

        // Process connections with limited concurrency
        using var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = ediConnections.Select(async connection =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessConnectionAsync(ediProcessingService, connection, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("Completed EDI document sync at {Time}", DateTimeOffset.UtcNow);
    }

    private async Task ProcessConnectionAsync(
        IEdiDocumentProcessingService ediProcessingService,
        DealerPartnerConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(
                "Processing EDI for connection {ConnectionId} (Dealer: {DealerId})",
                connection.Id, connection.DealerId);

            var result = await ediProcessingService.SyncEdiDocumentsAsync(connection.Id, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "EDI sync completed for connection {ConnectionId}: Found={Found}, Processed={Processed}, Failed={Failed}, Sent={Sent}",
                    connection.Id, result.FilesFound, result.FilesProcessed, result.FilesFailed, result.OutboundDocumentsSent);
            }
            else
            {
                _logger.LogWarning(
                    "EDI sync completed with errors for connection {ConnectionId}: {Error}",
                    connection.Id, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing EDI for connection {ConnectionId}", connection.Id);
        }
    }

    private bool IsEdiEnabled(DealerPartnerConnection connection)
    {
        if (string.IsNullOrEmpty(connection.ConfigurationJson))
            return false;

        try
        {
            // Check if EDI paths are configured
            var config = PartnerAdapters.SPR.SprConfiguration.FromJson(connection.ConfigurationJson);
            return !string.IsNullOrEmpty(config.SftpHost) &&
                   !string.IsNullOrEmpty(config.EdiInboundPath);
        }
        catch
        {
            return false;
        }
    }
}
