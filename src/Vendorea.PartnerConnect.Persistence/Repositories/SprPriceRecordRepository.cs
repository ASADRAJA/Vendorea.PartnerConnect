using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class SprPriceRecordRepository : ISprPriceRecordRepository
{
    private readonly PartnerConnectDbContext _context;

    public SprPriceRecordRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<SprPriceRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> GetByUploadIdAsync(
        int uploadId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == uploadId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> GetByStockNumberAsync(
        int dealerId,
        string stockNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .Where(r => r.DealerId == dealerId && r.StockNumber == stockNumber)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<SprPriceRecord?> GetLatestByStockNumberAsync(
        int dealerId,
        string stockNumber,
        CancellationToken cancellationToken = default)
    {
        // Get the latest completed upload for this dealer
        var latestUpload = await _context.PriceFeedUploads
            .Where(u => u.DealerId == dealerId &&
                        u.TradingPartner != null &&
                        u.TradingPartner.Code == "SPR" &&
                        u.Status == PriceFeedUploadStatus.Completed)
            .OrderByDescending(u => u.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestUpload == null)
            return null;

        return await _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == latestUpload.Id && r.StockNumber == stockNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> GetByUpcAsync(
        int dealerId,
        string upc,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .Where(r => r.DealerId == dealerId && r.Upc == upc)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> GetByCategoryAsync(
        int dealerId,
        string categoryCode,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprPriceRecords
            .Where(r => r.DealerId == dealerId && r.CategoryCode == categoryCode)
            .OrderBy(r => r.StockNumber);

        if (offset.HasValue)
        {
            query = (IOrderedQueryable<SprPriceRecord>)query.Skip(offset.Value);
        }

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<SprPriceRecord>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> GetCurrentPricesAsync(
        int dealerId,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        // Get the latest completed upload for this dealer
        var latestUpload = await _context.PriceFeedUploads
            .Where(u => u.DealerId == dealerId &&
                        u.TradingPartner != null &&
                        u.TradingPartner.Code == "SPR" &&
                        u.Status == PriceFeedUploadStatus.Completed)
            .OrderByDescending(u => u.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestUpload == null)
            return Array.Empty<SprPriceRecord>();

        var query = _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == latestUpload.Id)
            .OrderBy(r => r.StockNumber);

        if (offset.HasValue)
        {
            query = (IOrderedQueryable<SprPriceRecord>)query.Skip(offset.Value);
        }

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<SprPriceRecord>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PricePage> GetCurrentPricePageAsync(
        int dealerId,
        string partnerCode,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Latest completed upload for this dealer + partner drives "current" prices.
        var latestUpload = await _context.PriceFeedUploads
            .Where(u => u.DealerId == dealerId &&
                        u.TradingPartner != null &&
                        u.TradingPartner.Code == partnerCode &&
                        u.Status == PriceFeedUploadStatus.Completed)
            .OrderByDescending(u => u.UploadedAt)
            .Select(u => new { u.Id, u.UploadedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestUpload == null)
            return new PricePage(Array.Empty<PriceRow>(), 0);

        var query = _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == latestUpload.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.StockNumber.Contains(term) || r.ProductDescription.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(r => r.StockNumber)
            .Skip(skip)
            .Take(take)
            .Select(r => new PriceRow(
                r.StockNumber,
                r.ProductDescription,
                r.NetCostNonCcp,
                r.CatalogListPrice,
                r.SellingUnitOfMeasure,
                r.PricingStartDate,
                latestUpload.UploadedAt))
            .ToListAsync(cancellationToken);

        return new PricePage(items, total);
    }

    public async Task<IReadOnlyList<PriceHistoryRow>> GetPriceHistoryAsync(
        int dealerId,
        string partnerCode,
        string stockNumber,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .Where(r => r.DealerId == dealerId && r.StockNumber == stockNumber)
            .Join(
                _context.PriceFeedUploads.Where(u =>
                    u.TradingPartner != null &&
                    u.TradingPartner.Code == partnerCode &&
                    u.Status == PriceFeedUploadStatus.Completed),
                r => r.PriceFeedUploadId,
                u => u.Id,
                (r, u) => new { r, u })
            .OrderByDescending(x => x.u.UploadedAt)
            .Take(take)
            .Select(x => new PriceHistoryRow(
                x.r.NetCostNonCcp,
                x.r.CatalogListPrice,
                x.r.SellingUnitOfMeasure,
                x.r.PricingStartDate,
                x.r.PricingEndDate,
                x.u.UploadedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ContentCoverage> GetContentCoverageAsync(
        int dealerId,
        string partnerCode,
        string localeId,
        CancellationToken cancellationToken = default)
    {
        var latestUploadId = await _context.PriceFeedUploads
            .Where(u => u.DealerId == dealerId &&
                        u.TradingPartner != null &&
                        u.TradingPartner.Code == partnerCode &&
                        u.Status == PriceFeedUploadStatus.Completed)
            .OrderByDescending(u => u.UploadedAt)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestUploadId == null)
            return new ContentCoverage(0, 0);

        var total = await _context.SprPriceRecords
            .CountAsync(r => r.PriceFeedUploadId == latestUploadId.Value, cancellationToken);

        // Count dealer SKUs that have shared partner content (join on StockNumber == ProductId).
        var withContent = await _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == latestUploadId.Value)
            .Where(r => _context.SprProductContent.Any(c =>
                c.ProductId == r.StockNumber && c.LocaleId == localeId))
            .CountAsync(cancellationToken);

        return new ContentCoverage(total, withContent);
    }

    public async Task<SkuContentPage> GetSkuContentPageAsync(
        int dealerId,
        string partnerCode,
        string localeId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var latestUploadId = await _context.PriceFeedUploads
            .Where(u => u.DealerId == dealerId &&
                        u.TradingPartner != null &&
                        u.TradingPartner.Code == partnerCode &&
                        u.Status == PriceFeedUploadStatus.Completed)
            .OrderByDescending(u => u.UploadedAt)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestUploadId == null)
            return new SkuContentPage(Array.Empty<SkuContentRow>(), 0);

        var query = _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == latestUploadId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.StockNumber.Contains(term) || r.ProductDescription.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(r => r.StockNumber)
            .Skip(skip)
            .Take(take)
            .Select(r => new
            {
                r.StockNumber,
                r.ProductDescription,
                Content = _context.SprProductContent
                    .Where(c => c.ProductId == r.StockNumber && c.LocaleId == localeId)
                    .Select(c => new { c.BrandName, c.Description1 })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var rows = items
            .Select(x => new SkuContentRow(
                x.StockNumber,
                x.ProductDescription,
                x.Content != null,
                x.Content?.BrandName,
                x.Content?.Description1))
            .ToList();

        return new SkuContentPage(rows, total);
    }

    public async Task BulkInsertAsync(
        IEnumerable<SprPriceRecord> records,
        CancellationToken cancellationToken = default)
    {
        var recordList = records as IReadOnlyList<SprPriceRecord> ?? records.ToList();
        if (recordList.Count == 0)
        {
            return;
        }

        // Use SqlBulkCopy rather than EF AddRange/SaveChanges. EF tracks every entity and
        // re-runs change detection over the whole (growing) set on each batch, which is
        // O(n^2) and far too slow over Azure SQL's network latency for full price files —
        // it blows past the App Service 230s request limit. SqlBulkCopy streams the rows
        // directly and finishes in seconds.
        var entityType = _context.Model.FindEntityType(typeof(SprPriceRecord))
            ?? throw new InvalidOperationException("SprPriceRecord is not mapped.");

        // All mapped scalar columns except the store-generated identity key (Id).
        var properties = entityType.GetProperties()
            .Where(p => !p.IsPrimaryKey() && p.PropertyInfo != null)
            .ToList();

        var table = new DataTable();
        foreach (var p in properties)
        {
            var columnType = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
            table.Columns.Add(p.GetColumnName(), columnType);
        }

        foreach (var record in recordList)
        {
            var row = table.NewRow();
            for (int c = 0; c < properties.Count; c++)
            {
                row[c] = properties[c].PropertyInfo!.GetValue(record) ?? DBNull.Value;
            }
            table.Rows.Add(row);
        }

        var schema = entityType.GetSchema();
        var tableName = entityType.GetTableName()!;
        var destination = schema != null ? $"[{schema}].[{tableName}]" : $"[{tableName}]";

        var connection = (SqlConnection)_context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = destination,
                BatchSize = 5000,
                BulkCopyTimeout = 300
            };

            // Map by name so column order in the DataTable doesn't matter.
            foreach (DataColumn column in table.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulkCopy.WriteToServerAsync(table, cancellationToken);
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task DeleteByUploadIdAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        await _context.SprPriceRecords
            .Where(r => r.PriceFeedUploadId == uploadId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> GetCountByUploadIdAsync(int uploadId, CancellationToken cancellationToken = default)
    {
        return await _context.SprPriceRecords
            .CountAsync(r => r.PriceFeedUploadId == uploadId, cancellationToken);
    }

    public async Task<IReadOnlyList<SprPriceRecord>> SearchByDescriptionAsync(
        int dealerId,
        string searchTerm,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SprPriceRecords
            .Where(r => r.DealerId == dealerId &&
                        r.ProductDescription.Contains(searchTerm))
            .OrderBy(r => r.ProductDescription);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<SprPriceRecord>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
