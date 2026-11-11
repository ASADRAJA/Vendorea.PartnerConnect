using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Deletes payload captures past their expiry.
/// </summary>
/// <remarks>
/// The half of the archive that makes it bounded. Everything written carries a non-null
/// <c>ExpiresAt</c>, and this is what honours it — an archive with an expiry nobody collects is
/// just an archive.
/// </remarks>
public class IntegrationPayloadArchivePurgeJobHandler : IScheduledJobHandler
{
    public const string Key = "integration-payload-archive-purge";
    public string JobKey => Key;

    /// <summary>Passes per run, so a large backlog drains over several runs rather than one.</summary>
    private const int MaxBatchesPerRun = 20;

    private readonly IIntegrationPayloadArchiveRepository _archive;
    private readonly PayloadArchiveOptions _options;
    private readonly ILogger<IntegrationPayloadArchivePurgeJobHandler> _logger;

    public IntegrationPayloadArchivePurgeJobHandler(
        IIntegrationPayloadArchiveRepository archive,
        IOptions<PayloadArchiveOptions> options,
        ILogger<IntegrationPayloadArchivePurgeJobHandler> logger)
    {
        _archive = archive;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JobExecutionResult> ExecuteAsync(
        ScheduledJob job, CancellationToken cancellationToken = default)
    {
        var asOf = DateTime.UtcNow;
        var removed = 0;

        for (var pass = 0; pass < MaxBatchesPerRun; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deleted = await _archive.PurgeExpiredAsync(
                asOf, _options.PurgeBatchSize, cancellationToken);

            removed += deleted;

            if (deleted < _options.PurgeBatchSize)
            {
                // Caught up.
                break;
            }
        }

        if (removed > 0)
        {
            _logger.LogInformation("Purged {Count} expired provider payload captures.", removed);
        }

        return JobExecutionResult.Ok($"Purged {removed} expired capture(s).");
    }
}
