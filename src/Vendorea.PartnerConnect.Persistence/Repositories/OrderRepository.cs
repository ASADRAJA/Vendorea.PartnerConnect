using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly PartnerConnectDbContext _context;

    public OrderRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Order?> GetByIdWithLinesAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Lines.OrderBy(l => l.LineNumber))
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Order?> GetByIdWithFullDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Organization)
            .Include(o => o.Tenant)
            .Include(o => o.TradingPartner)
            .Include(o => o.TenantPartnerAccount)
            .Include(o => o.Lines.OrderBy(l => l.LineNumber))
            .Include(o => o.StatusHistory.OrderByDescending(h => h.ChangedAt))
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetByTenantIdAsync(
        int tenantId,
        OrderStatus? status = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Where(o => o.TenantId == tenantId);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        query = query.OrderByDescending(o => o.OrderDate);

        if (offset.HasValue)
            query = query.Skip(offset.Value);
        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetByOrganizationIdAsync(
        int organizationId,
        OrderStatus? status = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Where(o => o.OrganizationId == organizationId);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        query = query.OrderByDescending(o => o.OrderDate);

        if (offset.HasValue)
            query = query.Skip(offset.Value);
        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetByPoNumberAsync(
        int tenantId,
        string poNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.TenantId == tenantId && o.PoNumber == poNumber)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> FindByPoNumberAsync(
        string poNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Where(o => o.PoNumber == poNumber)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetByPoNumberWithLinesAsync(
        int tenantId,
        string poNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Lines.OrderBy(l => l.LineNumber))
            .Where(o => o.TenantId == tenantId && o.PoNumber == poNumber)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> FindByPoNumberWithLinesAsync(
        string poNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Lines.OrderBy(l => l.LineNumber))
            .Where(o => o.PoNumber == poNumber)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasAppliedShipmentAsync(
        int orderId,
        string manifestId,
        CancellationToken cancellationToken = default)
    {
        return await _context.OrderAppliedShipments
            .AnyAsync(s => s.OrderId == orderId && s.ManifestId == manifestId, cancellationToken);
    }

    public async Task RecordAppliedShipmentAsync(
        int orderId,
        string manifestId,
        CancellationToken cancellationToken = default)
    {
        _context.OrderAppliedShipments.Add(new OrderAppliedShipment
        {
            OrderId = orderId,
            ManifestId = manifestId,
            AppliedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Order?> GetByIdempotencyKeyAsync(
        int organizationId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .FirstOrDefaultAsync(o =>
                o.OrganizationId == organizationId &&
                o.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public async Task<Order?> GetByExternalOrderIdAsync(
        string sourcePlatform,
        string externalOrderId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .FirstOrDefaultAsync(o =>
                o.SourcePlatform == sourcePlatform &&
                o.ExternalOrderId == externalOrderId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetAllAsync(
        Func<Order, bool>? filter = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders.AsQueryable();

        query = query.OrderByDescending(o => o.OrderDate);

        if (offset.HasValue)
            query = query.Skip(offset.Value);
        if (limit.HasValue)
            query = query.Take(limit.Value);

        var results = await query.ToListAsync(cancellationToken);

        // Apply in-memory filter if provided
        if (filter != null)
            return results.Where(filter).ToList();

        return results;
    }

    public async Task<IReadOnlyList<Order>> GetAllAsync(
        OrderStatus? status = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .Include(o => o.Organization)
            .Include(o => o.Tenant)
            .Include(o => o.TradingPartner)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        query = query.OrderByDescending(o => o.OrderDate);

        if (offset.HasValue)
            query = query.Skip(offset.Value);
        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(
        int? organizationId = null,
        int? tenantId = null,
        OrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders.AsQueryable();

        if (organizationId.HasValue)
            query = query.Where(o => o.OrganizationId == organizationId.Value);
        if (tenantId.HasValue)
            query = query.Where(o => o.TenantId == tenantId.Value);
        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        return await query.CountAsync(cancellationToken);
    }

    public async Task<int> GetCountByTenantIdAsync(
        int tenantId,
        OrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders.Where(o => o.TenantId == tenantId);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        return await query.CountAsync(cancellationToken);
    }

    public async Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        order.UpdatedAt = DateTime.UtcNow;
        _context.Orders.Update(order);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddStatusHistoryAsync(OrderStatusHistory history, CancellationToken cancellationToken = default)
    {
        _context.OrderStatusHistory.Add(history);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderStatusHistory>> GetStatusHistoryAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _context.OrderStatusHistory
            .Where(h => h.OrderId == orderId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateLineAsync(OrderLine line, CancellationToken cancellationToken = default)
    {
        line.UpdatedAt = DateTime.UtcNow;
        _context.OrderLines.Update(line);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrderLine?> GetLineByIdAsync(int lineId, CancellationToken cancellationToken = default)
    {
        return await _context.OrderLines
            .FirstOrDefaultAsync(l => l.Id == lineId, cancellationToken);
    }
}
