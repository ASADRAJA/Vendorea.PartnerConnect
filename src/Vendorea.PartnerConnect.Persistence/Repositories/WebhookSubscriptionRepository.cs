using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository for webhook subscription operations.
/// </summary>
public class WebhookSubscriptionRepository : IWebhookSubscriptionRepository
{
    private readonly PartnerConnectDbContext _context;

    public WebhookSubscriptionRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        await _context.WebhookSubscriptions.AddAsync(subscription, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WebhookSubscription?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebhookSubscription>> GetByDealerIdAsync(
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WebhookSubscriptions
            .Where(s => s.DealerId == dealerId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveForEventAsync(
        int dealerId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        return await _context.WebhookSubscriptions
            .Where(s => s.DealerId == dealerId
                && s.IsActive
                && !s.IsSuspended
                && s.Events.Contains(eventType))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        _context.WebhookSubscriptions.Update(subscription);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var subscription = await GetByIdAsync(id, cancellationToken);
        if (subscription == null)
        {
            return false;
        }

        _context.WebhookSubscriptions.Remove(subscription);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebhookSubscription>> GetFailingSubscriptionsAsync(
        int minConsecutiveFailures = 5,
        CancellationToken cancellationToken = default)
    {
        return await _context.WebhookSubscriptions
            .Where(s => s.IsActive
                && !s.IsSuspended
                && s.ConsecutiveFailures >= minConsecutiveFailures)
            .ToListAsync(cancellationToken);
    }
}
