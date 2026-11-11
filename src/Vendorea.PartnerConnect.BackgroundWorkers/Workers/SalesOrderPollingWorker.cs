using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.BackgroundWorkers.Workers;

/// <summary>
/// Polls every connected marketplace account for changed orders.
/// </summary>
/// <remarks>
/// Connections are served least-recently-attempted first. A worker that simply took the first
/// batch it found would serve the same few every cycle and starve the rest, and with a provider
/// budget of roughly one call every three minutes a starved connection is one whose orders never
/// arrive at all. Each connection's attempt is recorded whatever the outcome — a connection that
/// was only rate limited has still had its turn, and without that it would be picked first again
/// on every cycle.
///
/// Concurrency is bounded and each connection runs in its own scope, so a slow or failing seller
/// cannot hold the others behind it or share their DbContext.
/// </remarks>
public class SalesOrderPollingWorker : BackgroundService
{
    private const IntegrationCapabilities Capability = IntegrationCapabilities.SalesOrderIngest;

    private readonly ILogger<SalesOrderPollingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;

    public SalesOrderPollingWorker(
        ILogger<SalesOrderPollingWorker> logger,
        IConfiguration configuration,
        IServiceProvider services)
    {
        _logger = logger;
        _configuration = configuration;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Workers:SalesOrderPolling:Enabled", false))
        {
            // Off by default. It must not start calling Amazon on a deployment where the
            // integration has not been configured and approved.
            _logger.LogInformation("Sales-order polling is disabled.");
            return;
        }

        // Five minutes is the floor, not a preference: searchOrders allows roughly one call
        // every three minutes per seller, so a shorter interval only earns 429s.
        var interval = TimeSpan.FromMinutes(
            Math.Max(5, _configuration.GetValue("Workers:SalesOrderPolling:IntervalMinutes", 15)));
        var maxConcurrency = Math.Max(1,
            _configuration.GetValue("Workers:SalesOrderPolling:MaxConcurrency", 4));
        var batchSize = Math.Max(1,
            _configuration.GetValue("Workers:SalesOrderPolling:BatchSize", 50));

        _logger.LogInformation(
            "Sales-order polling worker started: every {Interval}, up to {Batch} connections, {Concurrency} at a time",
            interval, batchSize, maxConcurrency);

        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(batchSize, maxConcurrency, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad cycle must not stop the worker for the life of the process.
                _logger.LogError(ex, "The sales-order polling cycle failed.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunCycleAsync(int batchSize, int maxConcurrency, CancellationToken stoppingToken)
    {
        List<IntegrationConnection> connections;

        using (var scope = _services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IIntegrationConnectionRepository>();
            var syncStates = scope.ServiceProvider.GetRequiredService<IIntegrationSyncStateRepository>();

            var candidates = (await repository.GetByStatusAsync(
                    IntegrationConnectionStatus.Connected, batchSize, stoppingToken))
                .Where(c => c.GrantedCapabilities.HasFlag(Capability))
                .ToList();

            if (candidates.Count == 0)
            {
                return;
            }

            var order = await syncStates.OrderByStalenessAsync(
                Capability, candidates.Select(c => c.Id).ToList(), stoppingToken);

            var byId = candidates.ToDictionary(c => c.Id);
            connections = order.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        }

        using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var work = connections.Select(async connection =>
        {
            await gate.WaitAsync(stoppingToken);
            try
            {
                await PollOneAsync(connection, stoppingToken);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(work);
    }

    private async Task PollOneAsync(IntegrationConnection connection, CancellationToken stoppingToken)
    {
        // Its own scope, so one seller's DbContext and its failures stay its own.
        using var scope = _services.CreateScope();
        var polling = scope.ServiceProvider.GetRequiredService<ISalesOrderPollingService>();
        var syncStates = scope.ServiceProvider.GetRequiredService<IIntegrationSyncStateRepository>();

        try
        {
            var result = await polling.PollAsync(connection, stoppingToken);

            if (result.Total > 0 || result.Outcome == SalesOrderPollOutcome.Failed)
            {
                _logger.LogInformation(
                    "Connection {ConnectionId}: {Outcome} — {Created} created, {Updated} updated, {Stale} stale. {Error}",
                    connection.Id, result.Outcome, result.Created, result.Updated, result.SkippedStale,
                    result.Error);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Polling connection {ConnectionId} threw.", connection.Id);
        }
        finally
        {
            // Always, including on failure: a connection that errors has still had its turn, and
            // not recording it would put it at the head of the queue for ever.
            try
            {
                await syncStates.RecordAttemptAsync(connection.Id, Capability, null, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not record the poll attempt for {ConnectionId}.", connection.Id);
            }
        }
    }
}
