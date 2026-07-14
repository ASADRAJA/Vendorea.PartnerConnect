using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>A single current-price row projected for the org/customer catalog.</summary>
public sealed record PriceRow(
    string Sku,
    string? Description,
    decimal Cost,
    decimal ListPrice,
    string? Uom,
    DateTime? EffectiveDate,
    DateTime? LastUpdatedAt);

/// <summary>A page of current prices (paged in SQL) plus the total matching count.</summary>
public sealed record PricePage(IReadOnlyList<PriceRow> Items, int Total);

/// <summary>One historical price point for a SKU (one per completed price upload that contained it).</summary>
public sealed record PriceHistoryRow(
    decimal Cost,
    decimal ListPrice,
    string? Uom,
    DateTime? EffectiveDate,
    DateTime? EndDate,
    DateTime UploadedAt);

/// <summary>Content-coverage counts for a dealer's current catalog against shared partner content.</summary>
public sealed record ContentCoverage(int TotalSkus, int WithContent);

/// <summary>One SKU's content-availability row for the dealer's current catalog.</summary>
public sealed record SkuContentRow(string Sku, string? Description, bool HasContent, string? Brand, string? ContentDescription);

/// <summary>A page of per-SKU content availability plus the total matching count.</summary>
public sealed record SkuContentPage(IReadOnlyList<SkuContentRow> Items, int Total);

/// <summary>
/// Repository for managing SPR price records.
/// </summary>
public interface ISprPriceRecordRepository
{
    /// <summary>
    /// Returns a SQL-paged page of the dealer's current prices (from the latest completed upload for
    /// the given partner), optionally filtered by a SKU/description search term, plus the total count.
    /// </summary>
    Task<PricePage> GetCurrentPricePageAsync(
        int dealerId,
        string partnerCode,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the price history for one SKU: one row per completed price upload for the given
    /// partner that contained the SKU, newest first (capped by <paramref name="take"/>).
    /// </summary>
    Task<IReadOnlyList<PriceHistoryRow>> GetPriceHistoryAsync(
        int dealerId,
        string partnerCode,
        string stockNumber,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Content-coverage counts (computed in SQL) for the dealer's current catalog: total SKUs in the
    /// latest completed price upload, and how many of those SKUs have shared partner content.
    /// </summary>
    Task<ContentCoverage> GetContentCoverageAsync(
        int dealerId,
        string partnerCode,
        string localeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A SQL-paged page of the dealer's current-catalog SKUs left-joined to shared partner content,
    /// indicating whether each SKU has content (and its brand/description when it does).
    /// </summary>
    Task<SkuContentPage> GetSkuContentPageAsync(
        int dealerId,
        string partnerCode,
        string localeId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

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
