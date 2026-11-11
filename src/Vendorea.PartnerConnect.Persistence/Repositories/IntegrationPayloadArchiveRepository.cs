using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class IntegrationPayloadArchiveRepository : IIntegrationPayloadArchiveRepository
{
    private readonly PartnerConnectDbContext _context;

    public IntegrationPayloadArchiveRepository(PartnerConnectDbContext context) => _context = context;

    public Task<string?> GetLatestHashAsync(
        Guid connectionId, IntegrationPayloadKind kind, string externalReference,
        CancellationToken cancellationToken = default)
        => _context.IntegrationPayloadArchives
            .AsNoTracking()
            .Where(a => a.IntegrationConnectionId == connectionId
                        && a.PayloadKind == kind
                        && a.ExternalReference == externalReference)
            .OrderByDescending(a => a.CapturedAt)
            .Select(a => a.ContentHash)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(
        IntegrationPayloadArchive archive, CancellationToken cancellationToken = default)
    {
        _context.IntegrationPayloadArchives.Add(archive);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IntegrationPayloadArchive>> GetForReferenceAsync(
        int tenantId, string externalReference, int limit, CancellationToken cancellationToken = default)
        => await _context.IntegrationPayloadArchives
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.ExternalReference == externalReference)
            .OrderByDescending(a => a.CapturedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Deletes a bounded batch of expired rows.
    /// </summary>
    /// <remarks>
    /// Batched rather than one sweeping DELETE. A long backlog deleted in a single statement holds
    /// a transaction and its locks open for as long as it takes, on a database shared with
    /// Merchant360 — the purge is housekeeping and must never be the reason something else waits.
    /// </remarks>
    public Task<int> PurgeExpiredAsync(
        DateTime asOfUtc, int batchSize, CancellationToken cancellationToken = default)
        => _context.IntegrationPayloadArchives
            .Where(a => _context.IntegrationPayloadArchives
                .Where(x => x.ExpiresAt < asOfUtc)
                .OrderBy(x => x.ExpiresAt)
                .Take(batchSize)
                .Select(x => x.Id)
                .Contains(a.Id))
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => _context.IntegrationPayloadArchives.CountAsync(cancellationToken);
}
