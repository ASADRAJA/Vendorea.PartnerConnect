using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Assembles the customer-portal dashboard's per-tenant signals from the real operational tables
/// (there is no dedicated summary table). Each signal is a single tenant-scoped SQL aggregate; no
/// rows are materialized into memory.
/// </summary>
public class TenantSummaryRepository : ITenantSummaryRepository
{
    private readonly PartnerConnectDbContext _context;

    public TenantSummaryRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<TenantSummarySignals> GetSummarySignalsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        // Last successful price feed: the newest completed/published upload for the dealer.
        var lastPriceSyncAt = await _context.PriceFeedUploads
            .Where(u => u.DealerId == tenantId &&
                        (u.Status == PriceFeedUploadStatus.Completed ||
                         u.Status == PriceFeedUploadStatus.PushedToMerchant360))
            .Select(u => (DateTime?)(u.ProcessedAt ?? u.UploadedAt))
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync(cancellationToken);

        // Last content refresh across the dealer's content subscriptions.
        var lastContentSyncAt = await _context.DealerContentSubscriptions
            .Where(s => s.DealerId == tenantId && s.LastFullRefreshAt != null)
            .MaxAsync(s => (DateTime?)s.LastFullRefreshAt, cancellationToken);

        // Open errors = failed feeds + failed orders + unresolved quarantines.
        var failedFeeds = await _context.PriceFeedUploads
            .CountAsync(u => u.DealerId == tenantId &&
                             (u.Status == PriceFeedUploadStatus.Failed ||
                              u.Status == PriceFeedUploadStatus.PushFailed), cancellationToken);

        var failedOrders = await _context.Orders
            .CountAsync(o => o.TenantId == tenantId && o.Status == OrderStatus.Failed, cancellationToken);

        var openQuarantines = await _context.QuarantinedDocuments
            .CountAsync(d => d.TenantId == tenantId && d.Resolution == null, cancellationToken);

        return new TenantSummarySignals
        {
            LastPriceSyncAt = lastPriceSyncAt,
            LastContentSyncAt = lastContentSyncAt,
            OpenErrorCount = failedFeeds + failedOrders + openQuarantines
        };
    }
}
