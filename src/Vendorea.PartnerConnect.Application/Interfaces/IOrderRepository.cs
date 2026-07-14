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

    /// <summary>
    /// A single page of a tenant's orders (newest first) with the trading partner eagerly loaded,
    /// filterable by partner, status, and order-date range. Paging is pushed to SQL; the total is
    /// the full filtered count. Backs the customer portal's tenant-scoped Orders list.
    /// </summary>
    Task<(IReadOnlyList<Order> Items, int Total)> GetTenantOrderPageAsync(
        int tenantId,
        int? tradingPartnerId,
        OrderStatus? status,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByOrganizationIdAsync(
        int organizationId,
        OrderStatus? status = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetAllAsync(
        OrderStatus? status = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(
        int? organizationId = null,
        int? tenantId = null,
        OrderStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByPoNumberAsync(
        int tenantId,
        string poNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds orders by PO number across all tenants (most recent first). Used to correlate inbound
    /// partner documents (POACK/ASN/invoice) when the dealer/tenant is not known from the feed.
    /// </summary>
    Task<IReadOnlyList<Order>> FindByPoNumberAsync(
        string poNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets orders for a PO number with their lines eagerly loaded (most recent first).
    /// </summary>
    Task<IReadOnlyList<Order>> GetByPoNumberWithLinesAsync(
        int tenantId,
        string poNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds orders by PO number across all tenants with lines eagerly loaded (most recent first).
    /// Used to correlate inbound ASN manifests when the dealer/tenant is not known from the feed.
    /// </summary>
    Task<IReadOnlyList<Order>> FindByPoNumberWithLinesAsync(
        string poNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the given shipment manifest has already been applied to the order
    /// (idempotency guard for re-ingested EZASNS).
    /// </summary>
    Task<bool> HasAppliedShipmentAsync(
        int orderId,
        string manifestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a shipment manifest has been applied to an order.
    /// </summary>
    Task RecordAppliedShipmentAsync(
        int orderId,
        string manifestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an order by idempotency key within an organization.
    /// </summary>
    Task<Order?> GetByIdempotencyKeyAsync(
        int organizationId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an order by external order ID and source platform.
    /// </summary>
    Task<Order?> GetByExternalOrderIdAsync(
        string sourcePlatform,
        string externalOrderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets orders with a custom filter.
    /// </summary>
    Task<IReadOnlyList<Order>> GetAllAsync(
        Func<Order, bool>? filter = null,
        int? limit = null,
        int? offset = null,
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
