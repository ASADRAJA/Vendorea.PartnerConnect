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

        // Initial delay to allow services to fully start (swallow cancellation on shutdown)
        try { await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

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
        var partnerRepo = scope.ServiceProvider.GetRequiredService<ITradingPartnerRepository>();
        var pollService = scope.ServiceProvider.GetRequiredService<ISprXmlDocumentProcessingService>();

        var partners = await partnerRepo.GetByStatusAsync(TradingPartnerStatus.Active, cancellationToken);

        // Filter to partners that support EDI (SPR partner with EDI configured)
        var ediPartners = partners
            .Where(p => p.Code == "SPR")
            .Where(IsEdiEnabled)
            .ToList();

        if (ediPartners.Count == 0)
        {
            _logger.LogDebug("No EDI-enabled partners found");
            return;
        }

        _logger.LogInformation("Processing EDI for {Count} partner(s)", ediPartners.Count);

        // Process partners with limited concurrency
        using var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = ediPartners.Select(async partner =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessPartnerAsync(pollService, partner, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        _logger.LogInformation("Completed EDI document sync at {Time}", DateTimeOffset.UtcNow);
    }

    private async Task ProcessPartnerAsync(
        ISprXmlDocumentProcessingService pollService,
        TradingPartner partner,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Polling SPR XML inbound for partner {TradingPartnerId}", partner.Id);

            var result = await pollService.PollInboundAsync(partner.Id, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "SPR inbound poll for partner {TradingPartnerId}: Found={Found}, Processed={Processed}, Failed={Failed}, Deleted={Deleted}",
                    partner.Id, result.Found, result.Processed, result.Failed, result.Deleted);
            }
            else
            {
                _logger.LogWarning(
                    "SPR inbound poll failed for partner {TradingPartnerId}: {Error}",
                    partner.Id, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling SPR inbound for partner {TradingPartnerId}", partner.Id);
        }
    }

    private static bool IsEdiEnabled(TradingPartner partner)
    {
        if (string.IsNullOrEmpty(partner.TransportConfigJson))
            return false;

        try
        {
            // Enabled when the SPR XML order-exchange SFTP is configured (host + inbound path).
            var config = PartnerAdapters.SPR.SprConfiguration.FromJson(partner.TransportConfigJson);
            return !string.IsNullOrEmpty(config.SftpHost) &&
                   !string.IsNullOrEmpty(config.SprXmlInboundPath);
        }
        catch
        {
            return false;
        }
    }
}
