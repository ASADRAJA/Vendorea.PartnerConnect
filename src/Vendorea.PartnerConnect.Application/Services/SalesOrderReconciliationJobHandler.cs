using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Runs the reconciliation sweep across every connected marketplace account.
/// </summary>
/// <remarks>
/// A cron job rather than a background service, unlike the poller. The poller runs every few
/// minutes because it is chasing changes; reconciliation runs once a day because it is checking
/// the chase worked. Being a <see cref="ScheduledJob"/> also means it appears in the admin
/// portal's job list with its last run and outcome, which is where somebody would look to ask
/// whether it has been running at all.
///
/// Connections are served least-recently-attempted first, the same fairness rule the poller uses,
/// so a sweep that runs out of rate-limit budget part way through resumes with the connections it
/// did not reach.
/// </remarks>
public class SalesOrderReconciliationJobHandler : IScheduledJobHandler
{
    public const string Key = "sales-order-reconciliation";
    public string JobKey => Key;

    private const IntegrationCapabilities Capability = IntegrationCapabilities.SalesOrderIngest;
    private const int MaxConnectionsPerRun = 50;

    private readonly IIntegrationConnectionRepository _connections;
    private readonly IIntegrationSyncStateRepository _syncStates;
    private readonly ISalesOrderReconciliationService _reconciliation;
    private readonly ILogger<SalesOrderReconciliationJobHandler> _logger;

    public SalesOrderReconciliationJobHandler(
        IIntegrationConnectionRepository connections,
        IIntegrationSyncStateRepository syncStates,
        ISalesOrderReconciliationService reconciliation,
        ILogger<SalesOrderReconciliationJobHandler> logger)
    {
        _connections = connections;
        _syncStates = syncStates;
        _reconciliation = reconciliation;
        _logger = logger;
    }

    public async Task<JobExecutionResult> ExecuteAsync(
        ScheduledJob job, CancellationToken cancellationToken = default)
    {
        var candidates = (await _connections.GetByStatusAsync(
                IntegrationConnectionStatus.Connected, MaxConnectionsPerRun, cancellationToken))
            .Where(c => c.GrantedCapabilities.HasFlag(Capability))
            .ToList();

        if (candidates.Count == 0)
        {
            return JobExecutionResult.Ok("No connected accounts to reconcile.");
        }

        var order = await _syncStates.OrderByStalenessAsync(
            Capability, candidates.Select(c => c.Id).ToList(), cancellationToken);

        var byId = candidates.ToDictionary(c => c.Id);

        var examined = 0;
        var changed = 0;
        var recovered = 0;

        foreach (var connectionId in order.Where(byId.ContainsKey))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await _reconciliation.ReconcileAsync(byId[connectionId], cancellationToken);
                examined += result.Examined;
                changed += result.Changed;
                recovered += result.Recovered;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One account's failure must not end the sweep for the others.
                _logger.LogError(ex, "Reconciling connection {ConnectionId} threw.", connectionId);
            }
        }

        var detail = $"Examined {examined}, updated {changed}, recovered {recovered}.";

        if (recovered > 0)
        {
            // Surfaced on the job row itself, so the admin portal shows it without anybody
            // reading logs. Recovered orders mean the live path lost something.
            _logger.LogWarning(
                "Reconciliation recovered {Recovered} order(s) the live path never ingested.", recovered);
        }

        return JobExecutionResult.Ok(detail);
    }
}
