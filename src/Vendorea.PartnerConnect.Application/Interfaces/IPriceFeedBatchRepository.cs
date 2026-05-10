using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for managing price feed batch records.
/// </summary>
public interface IPriceFeedBatchRepository
{
    /// <summary>
    /// Gets a price feed batch by its ID.
    /// </summary>
    Task<PriceFeedBatch?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all price feed batches for a specific dealer.
    /// </summary>
    Task<IReadOnlyList<PriceFeedBatch>> GetByDealerIdAsync(
        int dealerId,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all price feed batches for a specific dealer-partner connection.
    /// </summary>
    Task<IReadOnlyList<PriceFeedBatch>> GetByConnectionIdAsync(
        int connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets price feed batches by status.
    /// </summary>
    Task<IReadOnlyList<PriceFeedBatch>> GetByStatusAsync(
        FeedBatchStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent price feed batch for a connection.
    /// </summary>
    Task<PriceFeedBatch?> GetLatestByConnectionIdAsync(
        int connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets price feed batches within a date range.
    /// </summary>
    Task<IReadOnlyList<PriceFeedBatch>> GetByDateRangeAsync(
        int dealerId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new price feed batch.
    /// </summary>
    Task<PriceFeedBatch> AddAsync(PriceFeedBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing price feed batch.
    /// </summary>
    Task UpdateAsync(PriceFeedBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregate statistics for a dealer's price feeds.
    /// </summary>
    Task<PriceFeedStatistics> GetStatisticsAsync(
        int dealerId,
        DateTime? since = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for price feed batches.
/// </summary>
public record PriceFeedStatistics(
    int TotalBatches,
    int TotalItemsProcessed,
    int TotalItemsMatched,
    int TotalItemsUpdated,
    int TotalErrors,
    int CompletedBatches,
    int FailedBatches,
    DateTime? LastSyncAt);
