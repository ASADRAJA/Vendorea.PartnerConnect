using Vendorea.PartnerConnect.WorkerProcesses.Configuration;

namespace Vendorea.PartnerConnect.WorkerProcesses.Services;

/// <summary>
/// Runs the full SPR content ingestion pipeline (Download → Import → Transform) for a single
/// queued <c>FtpIngestionRun</c>, updating the run record's Status/Phase and stat counters as it
/// progresses. Shared by the queue worker so the heavy work runs off the API request thread.
/// </summary>
public interface ISprContentIngestionExecutor
{
    /// <summary>
    /// Executes the ingestion pipeline for the already-claimed run <paramref name="runId"/> using
    /// the per-run <paramref name="options"/> (built from the partner's stored config).
    /// </summary>
    Task ExecuteAsync(int runId, SprContentIngestionOptions options, CancellationToken ct);
}
