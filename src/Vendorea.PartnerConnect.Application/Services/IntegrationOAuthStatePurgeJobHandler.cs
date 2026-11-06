using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Deletes OAuth state rows that are past their expiry.
/// </summary>
/// <remarks>
/// Expired rows are already rejected, so this is hygiene rather than security: the table would
/// otherwise grow one row per abandoned consent for ever, and its unique hash index with it.
///
/// A grace period is deliberate. Deleting a state the instant it expires turns "this link has
/// expired" into "this link never existed", which is the harder failure to explain to a seller
/// who took too long on Amazon's login page.
/// </remarks>
public class IntegrationOAuthStatePurgeJobHandler : IScheduledJobHandler
{
    public const string Key = "integration-oauth-state-purge";
    public string JobKey => Key;

    private static readonly TimeSpan Grace = TimeSpan.FromDays(1);

    private readonly IIntegrationOAuthStateRepository _states;
    private readonly ILogger<IntegrationOAuthStatePurgeJobHandler> _logger;

    public IntegrationOAuthStatePurgeJobHandler(
        IIntegrationOAuthStateRepository states,
        ILogger<IntegrationOAuthStatePurgeJobHandler> logger)
    {
        _states = states;
        _logger = logger;
    }

    public async Task<JobExecutionResult> ExecuteAsync(
        ScheduledJob job, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Subtract(Grace);
        var removed = await _states.PurgeExpiredAsync(cutoff, cancellationToken);

        if (removed > 0)
        {
            _logger.LogInformation("Purged {Count} expired integration OAuth states.", removed);
        }

        return JobExecutionResult.Ok($"Purged {removed} expired state(s).");
    }
}
