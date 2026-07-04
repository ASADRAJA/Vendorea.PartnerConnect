using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for FTP ingestion run history.
/// </summary>
public interface IFtpIngestionRunRepository
{
    /// <summary>
    /// Saves a new ingestion run record.
    /// </summary>
    Task SaveRunAsync(FtpIngestionRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent ingestion run.
    /// </summary>
    Task<FtpIngestionRun?> GetLastRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the run history.
    /// </summary>
    Task<List<FtpIngestionRun>> GetRunHistoryAsync(int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific run by ID.
    /// </summary>
    Task<FtpIngestionRun?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets runs with the given status, oldest first, up to <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<FtpIngestionRun>> GetByStatusAsync(string status, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a Queued run for processing (Queued -> Running), stamping
    /// StartedAt and an initial Phase. Returns true if this caller won the claim.
    /// </summary>
    Task<bool> TryClaimAsync(int runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Un-sticks runs stranded in Running by a crashed/restarted worker: marks any Running
    /// run started before <paramref name="olderThanUtc"/> as Failed. Returns the number reclaimed.
    /// Content runs are heavy, so these are NOT auto-requeued.
    /// </summary>
    Task<int> ReclaimStaleAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);
}
