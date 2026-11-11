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
            .Include(o => o.Connection!).ThenInclude(c => c.Provider)
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
            // Someone else inserted this order between the lookup and here. Everything staged for
            // this order is discarded — the order, its lines, its opening history entry, and the
            // integration event and outbox message staged alongside them. Detaching every Added
            // entry is safe because the only thing Added in this scope is the order currently
            // being ingested: anything from a previous order in the same scope was committed by
            // its own SaveChanges and is no longer Added.
            foreach (var entry in _context.ChangeTracker.Entries()
                         .Where(e => e.State == EntityState.Added)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task<SalesOrderPage> SearchAsync(
        int tenantId, SalesOrderQuery query, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(query.Take, 1, 200);
        var skip = Math.Max(0, query.Skip);

        var filtered = _context.SalesOrders
            .AsNoTracking()
            // Tenant first, always. Every other filter narrows within one tenant's orders.
            .Where(o => o.TenantId == tenantId);

        if (query.ConnectionId.HasValue)
        {
            filtered = filtered.Where(o => o.IntegrationConnectionId == query.ConnectionId.Value);
        }

        if (query.Status.HasValue)
        {
            filtered = filtered.Where(o => o.Status == query.Status.Value);
        }

        if (query.MappingStatus.HasValue)
        {
            filtered = filtered.Where(o => o.MappingStatus == query.MappingStatus.Value);
        }

        if (query.UpdatedSince.HasValue)
        {
            filtered = filtered.Where(o => o.SourceLastUpdatedAt >= query.UpdatedSince.Value);
        }

        var total = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .Include(o => o.Connection!).ThenInclude(c => c.Provider)
            .OrderByDescending(o => o.PurchaseDate)
            .ThenBy(o => o.Id)   // a stable tiebreak, so paging cannot repeat or skip a row
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new SalesOrderPage(items, total, skip, take);
    }

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

/// <inheritdoc />
public class SalesOrderAdminRepository : ISalesOrderAdminRepository
{
    private readonly PartnerConnectDbContext _context;

    public SalesOrderAdminRepository(PartnerConnectDbContext context) => _context = context;

    public async Task<SalesOrderAdminPage> SearchAsync(
        SalesOrderAdminQuery query, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(query.Take, 1, 200);
        var skip = Math.Max(0, query.Skip);

        var filtered = _context.SalesOrders.AsNoTracking().AsQueryable();

        if (query.OrganizationId.HasValue)
        {
            // Through the connection, which is where organization ownership lives.
            filtered = filtered.Where(o => o.Connection!.OrganizationId == query.OrganizationId.Value);
        }

        if (query.TenantId.HasValue)
        {
            filtered = filtered.Where(o => o.TenantId == query.TenantId.Value);
        }

        if (query.ConnectionId.HasValue)
        {
            filtered = filtered.Where(o => o.IntegrationConnectionId == query.ConnectionId.Value);
        }

        if (query.MappingStatus.HasValue)
        {
            filtered = filtered.Where(o => o.MappingStatus == query.MappingStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();

            // Provider order id or any line's SKU. An operator is given one or the other and
            // should not have to know which field it belongs to.
            filtered = filtered.Where(o =>
                EF.Functions.ILike(o.ExternalOrderId, $"%{term}%")
                || o.Lines.Any(l => l.SellerSku != null && EF.Functions.ILike(l.SellerSku, $"%{term}%")));
        }

        // The status counts are computed BEFORE the status filter is applied, so the tab badges
        // keep showing the whole picture rather than collapsing to the tab you are already on.
        var countsByStatus = await filtered
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var unmapped = await filtered
            .CountAsync(o => o.MappingStatus != Domain.Entities.Sales.MappingStatus.Mapped, cancellationToken);

        if (query.Status.HasValue)
        {
            filtered = filtered.Where(o => o.Status == query.Status.Value);
        }

        var total = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .Include(o => o.Connection!).ThenInclude(c => c.Provider)
            .Include(o => o.Lines)
            .OrderByDescending(o => o.PurchaseDate)
            .ThenBy(o => o.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new SalesOrderAdminPage(items, total, skip, take, countsByStatus, unmapped);
    }

    public Task<SalesOrder?> GetAsync(Guid salesOrderId, CancellationToken cancellationToken = default)
        => _context.SalesOrders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Connection!).ThenInclude(c => c.Provider)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == salesOrderId, cancellationToken);
}
