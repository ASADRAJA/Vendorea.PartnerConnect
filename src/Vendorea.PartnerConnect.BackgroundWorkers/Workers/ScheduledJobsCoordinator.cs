using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Drives the generic cron-jobs framework: on a fixed tick it asks the scheduled-job service to run
/// every job whose cron schedule is due. All scheduling/claiming/recording lives in the service;
/// this worker is just the heartbeat. New cron jobs are added by registering a job handler and a
/// ScheduledJob row — no change here.
/// </summary>
public class ScheduledJobsCoordinator : BackgroundService
{
    private readonly ILogger<ScheduledJobsCoordinator> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public ScheduledJobsCoordinator(
        ILogger<ScheduledJobsCoordinator> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tickSeconds = _configuration.GetValue("Workers:ScheduledJobs:TickSeconds", 60);
        var initialDelaySeconds = _configuration.GetValue("Workers:ScheduledJobs:InitialDelaySeconds", 20);

        _logger.LogInformation("Scheduled Jobs Coordinator starting; tick every {TickSeconds}s", tickSeconds);

        try { await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(15, tickSeconds)));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IScheduledJobService>();
                await service.RunDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled Jobs Coordinator tick failed");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }
}
