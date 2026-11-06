using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class IntegrationOAuthStateRepository : IIntegrationOAuthStateRepository
{
    private readonly PartnerConnectDbContext _context;
    public IntegrationOAuthStateRepository(PartnerConnectDbContext context) => _context = context;

    public async Task AddAsync(IntegrationOAuthState state, CancellationToken cancellationToken = default)
    {
        _context.IntegrationOAuthStates.Add(state);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IntegrationOAuthState?> GetByHashAsync(
        string tokenHash, CancellationToken cancellationToken = default)
        => await _context.IntegrationOAuthStates
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

    public async Task MarkUsedAsync(IntegrationOAuthState state, CancellationToken cancellationToken = default)
    {
        state.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> PurgeExpiredAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
        => await _context.IntegrationOAuthStates
            .Where(s => s.ExpiresAt < olderThanUtc)
            .ExecuteDeleteAsync(cancellationToken);
}
