using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// A SQL-paged page of the partner's current (latest applied snapshot) inventory items — with their
/// per-location quantities loaded — plus the total matching count and the snapshot's freshness.
/// Inventory is partner-level (not per-dealer): the same snapshot serves every connected tenant.
/// </summary>
public sealed record InventoryPage(
    IReadOnlyList<SupplierInventoryItem> Items,
    int Total,
    DateTime? AsOf);

/// <summary>
/// Repository for SupplierInventoryItem entities.
/// </summary>
public interface ISupplierInventoryItemRepository
{
    /// <summary>
    /// Returns a SQL-paged page of the partner's current inventory (latest applied snapshot), with
    /// per-location quantities loaded, optionally filtered by a SKU/description search term. Includes
    /// the total matching count and the snapshot's as-of timestamp.
    /// </summary>
    Task<InventoryPage> SearchCurrentInventoryAsync(
        int tradingPartnerId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets item by ID.
    /// </summary>
    Task<SupplierInventoryItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items by snapshot ID.
    /// </summary>
    Task<IReadOnlyList<SupplierInventoryItem>> GetBySnapshotIdAsync(
        int snapshotId,
        int skip = 0,
        int take = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets item by SKU from the current (latest applied) snapshot.
    /// </summary>
    Task<SupplierInventoryItem?> GetCurrentBySkuAsync(
        int tradingPartnerId,
        string sku,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items by SKUs from the current snapshot.
    /// </summary>
    Task<IReadOnlyList<SupplierInventoryItem>> GetCurrentBySkusAsync(
        int tradingPartnerId,
        IEnumerable<string> skus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a single item.
    /// </summary>
    Task<SupplierInventoryItem> AddAsync(SupplierInventoryItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple items in bulk.
    /// </summary>
    Task AddRangeAsync(IEnumerable<SupplierInventoryItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts items in a snapshot.
    /// </summary>
    Task<int> CountBySnapshotIdAsync(int snapshotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all items for a snapshot.
    /// </summary>
    Task DeleteBySnapshotIdAsync(int snapshotId, CancellationToken cancellationToken = default);
}
