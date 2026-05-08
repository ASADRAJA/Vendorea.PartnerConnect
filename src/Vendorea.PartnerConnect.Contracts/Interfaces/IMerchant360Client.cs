namespace Vendorea.PartnerConnect.Contracts.Interfaces;

/// <summary>
/// Client interface for communicating with the Merchant360 API.
/// Used to push processed data (prices, inventory, products) to dealers.
/// </summary>
public interface IMerchant360Client
{
    /// <summary>
    /// Updates product prices in Merchant360 for a specific dealer.
    /// </summary>
    Task<PriceUpdateResult> UpdatePricesAsync(int dealerId, IEnumerable<PriceUpdateItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates inventory levels in Merchant360 for a specific dealer.
    /// </summary>
    Task<InventoryUpdateResult> UpdateInventoryAsync(int dealerId, IEnumerable<InventoryUpdateItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to the Merchant360 API.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

public record PriceUpdateItem(
    string Sku,
    decimal Cost,
    decimal? ListPrice,
    string? CurrencyCode);

public record PriceUpdateResult(
    bool Success,
    int UpdatedCount,
    int SkippedCount,
    int ErrorCount,
    IReadOnlyList<string>? Errors);

public record InventoryUpdateItem(
    string Sku,
    int QuantityAvailable,
    int? QuantityOnOrder,
    string? WarehouseCode);

public record InventoryUpdateResult(
    bool Success,
    int UpdatedCount,
    int SkippedCount,
    int ErrorCount,
    IReadOnlyList<string>? Errors);
