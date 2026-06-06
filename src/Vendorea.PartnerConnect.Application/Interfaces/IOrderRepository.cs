using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository interface for order operations.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdWithLinesAsync(int id, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdWithFullDetailsAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByTenantIdAsync(
        int tenantId,
        OrderStatus? status = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByOrganizationIdAsync(
        int organizationId,
        OrderStatus? status = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByPoNumberAsync(
        int tenantId,
        string poNumber,
        CancellationToken cancellationToken = default);

    Task<int> GetCountByTenantIdAsync(
        int tenantId,
        OrderStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);

    Task AddStatusHistoryAsync(OrderStatusHistory history, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderStatusHistory>> GetStatusHistoryAsync(int orderId, CancellationToken cancellationToken = default);

    Task UpdateLineAsync(OrderLine line, CancellationToken cancellationToken = default);
    Task<OrderLine?> GetLineByIdAsync(int lineId, CancellationToken cancellationToken = default);
}
