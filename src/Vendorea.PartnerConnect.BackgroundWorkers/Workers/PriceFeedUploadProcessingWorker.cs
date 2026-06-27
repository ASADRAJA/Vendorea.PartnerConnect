using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Drains queued (Pending) price feed uploads. The HTTP upload endpoint only stores the raw file
/// and creates a Pending row; the heavy parse + bulk insert runs here, off the request thread, so
/// large files never hit the Azure App Service 230s request limit.
/// </summary>
public class PriceFeedUploadProcessingWorker : BackgroundService
{
    private readonly ILogger<PriceFeedUploadProcessingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public PriceFeedUploadProcessingWorker(
        ILogger<PriceFeedUploadProcessingWorker> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollSeconds = _configuration.GetValue<int>("Workers:PriceFeedUploadProcessing:PollSeconds", 15);
        var batchSize = _configuration.GetValue<int>("Workers:PriceFeedUploadProcessing:BatchSize", 5);
        var initialDelaySeconds = _configuration.GetValue<int>("Workers:PriceFeedUploadProcessing:InitialDelaySeconds", 15);
        var poll = TimeSpan.FromSeconds(pollSeconds);

        _logger.LogInformation(
            "Price Feed Upload Processing Worker starting (poll every {PollSeconds}s, batch {BatchSize})",
            pollSeconds, batchSize);

        try { await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainPendingUploadsAsync(batchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error draining pending price feed uploads");
            }

            try
            {
                await Task.Delay(poll, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Price Feed Upload Processing Worker stopping");
    }

    private async Task DrainPendingUploadsAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var uploadRepository = scope.ServiceProvider.GetRequiredService<IPriceFeedUploadRepository>();
        var priceFeedService = scope.ServiceProvider.GetRequiredService<IPriceFeedService>();

        var pending = await uploadRepository.GetByStatusAsync(
            PriceFeedUploadStatus.Pending, batchSize, cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} pending price feed upload(s) to process", pending.Count);

        // Process sequentially: each upload is a large bulk insert into Azure SQL, and serial
        // processing keeps load predictable. The atomic claim inside the service guards against
        // another worker instance picking up the same row.
        foreach (var upload in pending)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var result = await priceFeedService.ProcessPendingUploadAsync(upload.Id, cancellationToken);

            _logger.LogInformation(
                "Processed price feed upload {UploadId}: status={Status}, records={Records}, errors={Errors}",
                upload.Id, result.Status, result.RecordCount, result.ErrorCount);
        }
    }
}
