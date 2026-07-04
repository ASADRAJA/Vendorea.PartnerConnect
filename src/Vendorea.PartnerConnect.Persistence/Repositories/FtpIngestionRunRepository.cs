using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class FtpIngestionRunRepository : IFtpIngestionRunRepository
{
    private readonly PartnerConnectDbContext _context;

    public FtpIngestionRunRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task SaveRunAsync(FtpIngestionRun run, CancellationToken cancellationToken = default)
    {
        if (run.Id == 0)
        {
            _context.FtpIngestionRuns.Add(run);
        }
        else
        {
            _context.FtpIngestionRuns.Update(run);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FtpIngestionRun?> GetLastRunAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FtpIngestionRuns
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<FtpIngestionRun>> GetRunHistoryAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        return await _context.FtpIngestionRuns
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<FtpIngestionRun?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.FtpIngestionRuns.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IReadOnlyList<FtpIngestionRun>> GetByStatusAsync(string status, int limit, CancellationToken cancellationToken = default)
    {
        return await _context.FtpIngestionRuns
            .Where(r => r.Status == status)
            .OrderBy(r => r.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryClaimAsync(int runId, CancellationToken cancellationToken = default)
    {
        // Atomic Queued -> Running transition. ExecuteUpdate issues a single UPDATE ... WHERE
        // Status = 'Queued', so only one worker can win even if several poll the same row.
        var now = DateTime.UtcNow;
        var rowsAffected = await _context.FtpIngestionRuns
            .Where(r => r.Id == runId && r.Status == "Queued")
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.Status, "Running")
                    .SetProperty(r => r.StartedAt, now)
                    .SetProperty(r => r.Phase, "Downloading"),
                cancellationToken);

        return rowsAffected == 1;
    }

    public async Task<int> ReclaimStaleAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        // A worker that crashed after claiming leaves a run stuck in Running. Content runs are heavy,
        // so we do NOT auto-requeue — just mark stranded runs Failed so they stop looking "in progress".
        var now = DateTime.UtcNow;
        return await _context.FtpIngestionRuns
            .Where(r => r.Status == "Running" && r.StartedAt < olderThanUtc)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.Status, "Failed")
                    .SetProperty(r => r.CompletedAt, now),
                cancellationToken);
    }
}
