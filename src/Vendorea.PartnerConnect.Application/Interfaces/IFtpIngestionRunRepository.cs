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
}
