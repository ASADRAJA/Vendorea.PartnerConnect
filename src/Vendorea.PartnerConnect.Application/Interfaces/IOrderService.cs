using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service interface for order operations.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Creates a new order with validation.
    /// </summary>
    Task<OrderResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an order by ID.
    /// </summary>
    Task<Order?> GetOrderAsync(int orderId, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an order with full details including lines.
    /// </summary>
    Task<Order?> GetOrderWithDetailsAsync(int orderId, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets orders for a tenant with pagination.
    /// </summary>
    Task<OrderListResult> GetOrdersAsync(
        int tenantId,
        OrderStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an order if possible.
    /// </summary>
    Task<OrderResult> CancelOrderAsync(int orderId, int tenantId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates order status (typically called by EDI processing).
    /// </summary>
    Task<OrderResult> UpdateOrderStatusAsync(
        int orderId,
        OrderStatus newStatus,
        string? source = null,
        string? reason = null,
        int? ediDocumentId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to create a new order.
/// </summary>
public record CreateOrderRequest
{
    public int OrganizationId { get; init; }
    public int TenantId { get; init; }
    public int TradingPartnerId { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string PoNumber { get; init; } = string.Empty;
    public DateTime? RequestedShipDate { get; init; }
    public DateTime? RequestedDeliveryDate { get; init; }
    public AddressInfo? ShipTo { get; init; }
    public AddressInfo? BillTo { get; init; }
    public string? ShippingMethod { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<CreateOrderLineRequest> Lines { get; init; } = [];
}

/// <summary>
/// Address information for shipping/billing.
/// </summary>
public record AddressInfo
{
    public string? Name { get; init; }
    public string? Company { get; init; }
    public string? Address1 { get; init; }
    public string? Address2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
}

/// <summary>
/// Request to create an order line.
/// </summary>
public record CreateOrderLineRequest
{
    public string Sku { get; init; } = string.Empty;
    public string? VendorSku { get; init; }
    public string? Upc { get; init; }
    public string? Description { get; init; }
    public decimal Quantity { get; init; }
    public string UnitOfMeasure { get; init; } = "EA";
    public decimal UnitPrice { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Result of an order operation.
/// </summary>
public record OrderResult
{
    public bool Success { get; init; }
    public Order? Order { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static OrderResult Succeeded(Order order) => new() { Success = true, Order = order };
    public static OrderResult Failed(string errorCode, string errorMessage) => new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>
/// Result of getting a list of orders.
/// </summary>
public record OrderListResult
{
    public IReadOnlyList<Order> Orders { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
