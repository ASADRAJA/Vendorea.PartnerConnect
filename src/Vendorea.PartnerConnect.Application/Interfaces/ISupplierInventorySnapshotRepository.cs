using Vendorea.PartnerConnect.Domain.Entities.Supplier;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for SupplierInventorySnapshot entities.
/// </summary>
public interface ISupplierInventorySnapshotRepository
{
    /// <summary>
    /// Gets snapshot by ID.
    /// </summary>
    Task<SupplierInventorySnapshot?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshot by ID with items loaded.
    /// </summary>
    Task<SupplierInventorySnapshot?> GetByIdWithItemsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest Applied snapshot for a trading partner.
    /// </summary>
    Task<SupplierInventorySnapshot?> GetLatestAppliedAsync(int tradingPartnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshots by trading partner.
    /// </summary>
    Task<IReadOnlyList<SupplierInventorySnapshot>> GetByTradingPartnerAsync(
        int tradingPartnerId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshots by status.
    /// </summary>
    Task<IReadOnlyList<SupplierInventorySnapshot>> GetByStatusAsync(
        InventorySnapshotStatus status,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshots by multiple statuses with optional partner filter.
    /// </summary>
    Task<IReadOnlyList<SupplierInventorySnapshot>> GetByStatusAsync(
        IEnumerable<InventorySnapshotStatus> statuses,
        int? tradingPartnerId = null,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new snapshot.
    /// </summary>
    Task<SupplierInventorySnapshot> AddAsync(SupplierInventorySnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a snapshot.
    /// </summary>
    Task UpdateAsync(SupplierInventorySnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supersedes all snapshots for a trading partner except the specified one.
    /// </summary>
    Task SupersedeAllExceptAsync(int tradingPartnerId, int excludeSnapshotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all but the newest <paramref name="retain"/> snapshots for a trading partner
    /// (cascades to items and per-location quantities). Returns the number of snapshots deleted.
    /// </summary>
    Task<int> DeleteOldSnapshotsAsync(int tradingPartnerId, int retain, CancellationToken cancellationToken = default);
}
