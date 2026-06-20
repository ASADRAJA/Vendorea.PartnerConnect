using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Background worker that processes outbox messages.
/// </summary>
public class OutboxProcessorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorWorker> _logger;
    private readonly OutboxWorkerOptions _options;

    public OutboxProcessorWorker(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorWorker> logger,
        IOptions<OutboxWorkerOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox processor worker");
            }

            try
            {
                await Task.Delay(_options.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Outbox processor worker stopping...");
    }

    private async Task ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        // Process pending messages
        var pendingProcessed = await outboxService.ProcessPendingAsync(
            _options.BatchSize,
            cancellationToken);

        if (pendingProcessed > 0)
        {
            _logger.LogDebug("Processed {Count} pending outbox messages", pendingProcessed);
        }

        // Process retries
        var retriesProcessed = await outboxService.ProcessRetriesAsync(
            _options.BatchSize,
            cancellationToken);

        if (retriesProcessed > 0)
        {
            _logger.LogDebug("Processed {Count} retry outbox messages", retriesProcessed);
        }

        // Periodic cleanup (every 100 runs or so)
        if (Random.Shared.Next(100) == 0)
        {
            var cleaned = await outboxService.CleanupAsync(
                _options.CleanupAge,
                cancellationToken);

            if (cleaned > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old outbox messages", cleaned);
            }
        }
    }
}

/// <summary>
/// Options for the outbox processor worker.
/// </summary>
public class OutboxWorkerOptions
{
    /// <summary>
    /// Interval between polling for new messages.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Number of messages to process per batch.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Age threshold for cleaning up delivered messages.
    /// </summary>
    public TimeSpan CleanupAge { get; set; } = TimeSpan.FromDays(7);
}
