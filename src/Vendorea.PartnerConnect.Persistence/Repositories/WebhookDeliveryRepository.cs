using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository for webhook delivery operations.
/// </summary>
public class WebhookDeliveryRepository : IWebhookDeliveryRepository
{
    private readonly PartnerConnectDbContext _context;

    public WebhookDeliveryRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        await _context.WebhookDeliveries.AddAsync(delivery, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WebhookDelivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WebhookDeliveries
            .Include(d => d.Subscription)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebhookDelivery>> GetBySubscriptionIdAsync(
        int subscriptionId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _context.WebhookDeliveries
            .Where(d => d.WebhookSubscriptionId == subscriptionId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebhookDelivery>> GetPendingAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Pending)
            .OrderBy(d => d.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebhookDelivery>> GetRetryDueAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Retry
                && d.NextRetryAt.HasValue
                && d.NextRetryAt <= now)
            .OrderBy(d => d.NextRetryAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        _context.WebhookDeliveries.Update(delivery);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CleanupDeliveredAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        var deliveries = await _context.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Delivered
                && d.CompletedAt.HasValue
                && d.CompletedAt < cutoff)
            .ToListAsync(cancellationToken);

        _context.WebhookDeliveries.RemoveRange(deliveries);
        await _context.SaveChangesAsync(cancellationToken);

        return deliveries.Count;
    }

    /// <inheritdoc />
    public async Task<WebhookDeliveryStatistics> GetStatisticsAsync(
        int? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WebhookDeliveries.AsQueryable();

        if (subscriptionId.HasValue)
        {
            query = query.Where(d => d.WebhookSubscriptionId == subscriptionId.Value);
        }

        var now = DateTime.UtcNow;
        var last24Hours = now.AddHours(-24);

        var pendingCount = await query.CountAsync(d => d.Status == WebhookDeliveryStatus.Pending, cancellationToken);
        var retryCount = await query.CountAsync(d => d.Status == WebhookDeliveryStatus.Retry, cancellationToken);
        var failedCount = await query.CountAsync(d => d.Status == WebhookDeliveryStatus.Failed, cancellationToken);
        var deliveredLast24Hours = await query.CountAsync(
            d => d.Status == WebhookDeliveryStatus.Delivered && d.CompletedAt >= last24Hours,
            cancellationToken);

        // Calculate average delivery time for successful deliveries in the last 24 hours
        var deliveryTimes = await query
            .Where(d => d.Status == WebhookDeliveryStatus.Delivered
                && d.CompletedAt >= last24Hours
                && d.DurationMs.HasValue)
            .Select(d => d.DurationMs!.Value)
            .ToListAsync(cancellationToken);

        var averageDeliveryTimeMs = deliveryTimes.Count > 0 ? deliveryTimes.Average() : 0;

        // Calculate success rate
        var totalAttempted = await query.CountAsync(
            d => d.Status == WebhookDeliveryStatus.Delivered || d.Status == WebhookDeliveryStatus.Failed,
            cancellationToken);
        var successfulDeliveries = await query.CountAsync(
            d => d.Status == WebhookDeliveryStatus.Delivered,
            cancellationToken);
        var successRate = totalAttempted > 0 ? (double)successfulDeliveries / totalAttempted : 0;

        return new WebhookDeliveryStatistics
        {
            PendingCount = pendingCount,
            RetryCount = retryCount,
            FailedCount = failedCount,
            DeliveredLast24Hours = deliveredLast24Hours,
            AverageDeliveryTimeMs = averageDeliveryTimeMs,
            SuccessRate = successRate
        };
    }
}
