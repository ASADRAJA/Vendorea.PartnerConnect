using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class DealerContentSubscriptionRepository : IDealerContentSubscriptionRepository
{
    private readonly PartnerConnectDbContext _context;

    public DealerContentSubscriptionRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<DealerContentSubscription?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.DealerContentSubscriptions
            .Include(s => s.TradingPartner)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<DealerContentSubscription?> GetByDealerAndPartnerAsync(
        int dealerId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DealerContentSubscriptions
            .Include(s => s.TradingPartner)
            .FirstOrDefaultAsync(
                s => s.DealerId == dealerId && s.TradingPartnerId == tradingPartnerId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<DealerContentSubscription>> GetByDealerIdAsync(
        int dealerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DealerContentSubscriptions
            .Include(s => s.TradingPartner)
            .Where(s => s.DealerId == dealerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DealerContentSubscription>> GetActiveSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.DealerContentSubscriptions
            .Include(s => s.TradingPartner)
            .Where(s => s.IsEnhancedContentEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DealerContentSubscription>> GetByTradingPartnerIdAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DealerContentSubscriptions
            .Where(s => s.TradingPartnerId == tradingPartnerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<DealerContentSubscription> CreateAsync(
        DealerContentSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        subscription.CreatedAt = DateTime.UtcNow;
        _context.DealerContentSubscriptions.Add(subscription);
        await _context.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    public async Task UpdateAsync(
        DealerContentSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        subscription.UpdatedAt = DateTime.UtcNow;
        _context.DealerContentSubscriptions.Update(subscription);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(
        int subscriptionId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        await _context.DealerContentSubscriptions
            .Where(s => s.Id == subscriptionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.IsEnhancedContentEnabled, isEnabled)
                    .SetProperty(s => s.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task UpdateLastContentVersionAsync(
        int subscriptionId,
        string contentVersion,
        CancellationToken cancellationToken = default)
    {
        await _context.DealerContentSubscriptions
            .Where(s => s.Id == subscriptionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.LastContentVersion, contentVersion)
                    .SetProperty(s => s.LastFullRefreshAt, DateTime.UtcNow)
                    .SetProperty(s => s.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task UpdateSubscribedLocalesAsync(
        int subscriptionId,
        List<string> locales,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _context.DealerContentSubscriptions
            .FindAsync(new object[] { subscriptionId }, cancellationToken);

        if (subscription != null)
        {
            subscription.SubscribedLocales = System.Text.Json.JsonSerializer.Serialize(locales);
            subscription.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(int subscriptionId, CancellationToken cancellationToken = default)
    {
        await _context.DealerContentSubscriptions
            .Where(s => s.Id == subscriptionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<bool> IsEnhancedContentEnabledAsync(
        int dealerId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DealerContentSubscriptions
            .AnyAsync(
                s => s.DealerId == dealerId &&
                     s.TradingPartnerId == tradingPartnerId &&
                     s.IsEnhancedContentEnabled,
                cancellationToken);
    }

    public async Task<IReadOnlyList<DealerContentSubscription>> GetSubscriptionsNeedingRefreshAsync(
        string currentContentVersion,
        CancellationToken cancellationToken = default)
    {
        return await _context.DealerContentSubscriptions
            .Include(s => s.TradingPartner)
            .Where(s => s.IsEnhancedContentEnabled &&
                       (s.LastContentVersion == null || s.LastContentVersion != currentContentVersion))
            .ToListAsync(cancellationToken);
    }
}
