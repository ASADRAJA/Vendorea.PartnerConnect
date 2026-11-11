using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Sales;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class SalesOrderRepository : ISalesOrderRepository
{
    private readonly PartnerConnectDbContext _context;

    public SalesOrderRepository(PartnerConnectDbContext context) => _context = context;

    public Task<SalesOrder?> FindAsync(
        Guid connectionId, string externalOrderId, CancellationToken cancellationToken = default)
        => _context.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(
                o => o.IntegrationConnectionId == connectionId && o.ExternalOrderId == externalOrderId,
                cancellationToken);

    /// <summary>
    /// Tenant-scoped by argument, like every other read in the integration surface.
    /// </summary>
    public Task<SalesOrder?> GetAsync(
        int tenantId, Guid salesOrderId, CancellationToken cancellationToken = default)
        => _context.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == salesOrderId && o.TenantId == tenantId, cancellationToken);

    public async Task<bool> TryAddAsync(SalesOrder order, CancellationToken cancellationToken = default)
    {
        _context.SalesOrders.Add(order);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Someone else inserted this order between the lookup and here. Detach what we tried
            // to add so the context stays usable for the update that follows.
            _context.Entry(order).State = EntityState.Detached;
            foreach (var line in order.Lines)
            {
                _context.Entry(line).State = EntityState.Detached;
            }
            foreach (var history in order.StatusHistory)
            {
                _context.Entry(history).State = EntityState.Detached;
            }

            return false;
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<SalesOrder>> ListAsync(
        int tenantId, int skip, int take, CancellationToken cancellationToken = default)
        => await _context.SalesOrders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.PurchaseDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
}

/// <inheritdoc />
public class SalesOrderRetentionRepository : ISalesOrderRetentionRepository
{
    private readonly PartnerConnectDbContext _context;

    public SalesOrderRetentionRepository(PartnerConnectDbContext context) => _context = context;

    public async Task<IReadOnlyList<SalesOrder>> GetDueForRedactionAsync(
        DateTime settledBefore, DateTime hardLimitBefore,
        IReadOnlyList<SalesOrderStatus> settledStatuses, int batchSize,
        CancellationToken cancellationToken = default)
        => await _context.SalesOrders
            // Already-redacted orders are skipped by the filtered index behind this predicate,
            // so a sweep over a long-lived table does not re-walk everything it has done before.
            .Where(o => o.PiiRedactedAt == null)
            .Where(o =>
                (settledStatuses.Contains(o.Status) && o.PurchaseDate < settledBefore)
                || o.PurchaseDate < hardLimitBefore)
            .OrderBy(o => o.PurchaseDate)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}

/// <inheritdoc />
public class SalesOrderReconciliationRepository : ISalesOrderReconciliationRepository
{
    private readonly PartnerConnectDbContext _context;

    public SalesOrderReconciliationRepository(PartnerConnectDbContext context) => _context = context;

    public async Task<IReadOnlyList<SalesOrder>> GetStaleAsync(
        Guid connectionId, DateTime notSyncedSince,
        IReadOnlyList<SalesOrderStatus> terminalStatuses, int limit,
        CancellationToken cancellationToken = default)
        => await _context.SalesOrders
            .AsNoTracking()
            .Where(o => o.IntegrationConnectionId == connectionId
                        && !terminalStatuses.Contains(o.Status)
                        && o.LastSyncedAt < notSyncedSince)
            .OrderBy(o => o.LastSyncedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetKnownExternalIdsAsync(
        Guid connectionId, IReadOnlyList<string> externalOrderIds,
        CancellationToken cancellationToken = default)
    {
        if (externalOrderIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        // One query for the whole page rather than a lookup per order. A page is up to a hundred
        // orders and the gap search runs per connection, so the difference is a hundredfold.
        return await _context.SalesOrders
            .AsNoTracking()
            .Where(o => o.IntegrationConnectionId == connectionId
                        && externalOrderIds.Contains(o.ExternalOrderId))
            .Select(o => o.ExternalOrderId)
            .ToListAsync(cancellationToken);
    }
}
