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
}
