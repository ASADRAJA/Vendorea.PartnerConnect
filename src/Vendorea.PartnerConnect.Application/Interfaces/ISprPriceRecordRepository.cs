using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for managing SPR price records.
/// </summary>
public interface ISprPriceRecordRepository
{
    /// <summary>
    /// Gets a price record by ID.
    /// </summary>
    Task<SprPriceRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all price records for a specific upload.
    /// </summary>
    Task<IReadOnlyList<SprPriceRecord>> GetByUploadIdAsync(
        int uploadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets price records for a dealer by stock number.
    /// </summary>
    Task<IReadOnlyList<SprPriceRecord>> GetByStockNumberAsync(
        int dealerId,
        string stockNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest price record for a dealer and SKU (from most recent upload).
    /// </summary>
    Task<SprPriceRecord?> GetLatestByStockNumberAsync(
        int dealerId,
        string stockNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets price records for a dealer by UPC.
    /// </summary>
    Task<IReadOnlyList<SprPriceRecord>> GetByUpcAsync(
        int dealerId,
        string upc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets price records for a dealer by category.
    /// </summary>
    Task<IReadOnlyList<SprPriceRecord>> GetByCategoryAsync(
        int dealerId,
        string categoryCode,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all current price records for a dealer (from most recent upload).
    /// </summary>
    Task<IReadOnlyList<SprPriceRecord>> GetCurrentPricesAsync(
        int dealerId,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk inserts price records for an upload.
    /// </summary>
    Task BulkInsertAsync(
        IEnumerable<SprPriceRecord> records,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all price records for an upload.
    /// </summary>
    Task DeleteByUploadIdAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of records for an upload.
    /// </summary>
    Task<int> GetCountByUploadIdAsync(int uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches price records by description.
    /// </summary>
    Task<IReadOnlyList<SprPriceRecord>> SearchByDescriptionAsync(
        int dealerId,
        string searchTerm,
        int? limit = null,
        CancellationToken cancellationToken = default);
}
