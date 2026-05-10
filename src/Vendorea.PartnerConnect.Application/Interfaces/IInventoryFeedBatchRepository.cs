using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for managing inventory feed batch records.
/// </summary>
public interface IInventoryFeedBatchRepository
{
    /// <summary>
    /// Gets an inventory feed batch by its ID.
    /// </summary>
    Task<InventoryFeedBatch?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all inventory feed batches for a specific dealer.
    /// </summary>
    Task<IReadOnlyList<InventoryFeedBatch>> GetByDealerIdAsync(
        int dealerId,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all inventory feed batches for a specific dealer-partner connection.
    /// </summary>
    Task<IReadOnlyList<InventoryFeedBatch>> GetByConnectionIdAsync(
        int connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets inventory feed batches by status.
    /// </summary>
    Task<IReadOnlyList<InventoryFeedBatch>> GetByStatusAsync(
        FeedBatchStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent inventory feed batch for a connection.
    /// </summary>
    Task<InventoryFeedBatch?> GetLatestByConnectionIdAsync(
        int connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets inventory feed batches within a date range.
    /// </summary>
    Task<IReadOnlyList<InventoryFeedBatch>> GetByDateRangeAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new inventory feed batch.
    /// </summary>
    Task<InventoryFeedBatch> AddAsync(InventoryFeedBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing inventory feed batch.
    /// </summary>
    Task UpdateAsync(InventoryFeedBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregate statistics for a dealer's inventory feeds.
    /// </summary>
    Task<InventoryFeedStatistics> GetStatisticsAsync(
        int dealerId,
        DateTime? since = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for inventory feed batches.
/// </summary>
public record InventoryFeedStatistics(
    int TotalBatches,
    int TotalItemsProcessed,
    int TotalItemsMatched,
    int TotalItemsUpdated,
    int TotalErrors,
    int CompletedBatches,
    int FailedBatches,
    DateTime? LastSyncAt);
