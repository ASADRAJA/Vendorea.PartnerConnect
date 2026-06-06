using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for SupplierInventoryItem entities.
/// </summary>
public interface ISupplierInventoryItemRepository
{
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
