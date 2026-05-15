using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository implementation for merchant subscription requests.
/// </summary>
public class MerchantSubscriptionRequestRepository : IMerchantSubscriptionRequestRepository
{
    private readonly PartnerConnectDbContext _context;

    public MerchantSubscriptionRequestRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<MerchantSubscriptionRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.MerchantSubscriptionRequests
            .Include(r => r.TradingPartner)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MerchantSubscriptionRequest>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MerchantSubscriptionRequests
            .Include(r => r.TradingPartner)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MerchantSubscriptionRequest>> GetByStatusAsync(
        SubscriptionRequestStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _context.MerchantSubscriptionRequests
            .Include(r => r.TradingPartner)
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MerchantSubscriptionRequest>> GetByTenantIdAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.MerchantSubscriptionRequests
            .Include(r => r.TradingPartner)
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MerchantSubscriptionRequest>> GetByTradingPartnerIdAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.MerchantSubscriptionRequests
            .Include(r => r.TradingPartner)
            .Where(r => r.TradingPartnerId == tradingPartnerId)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<MerchantSubscriptionRequest?> GetByTenantAndPartnerAsync(
        int tenantId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.MerchantSubscriptionRequests
            .Include(r => r.TradingPartner)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.TradingPartnerId == tradingPartnerId, cancellationToken);
    }

    public async Task<MerchantSubscriptionRequest> AddAsync(
        MerchantSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        _context.MerchantSubscriptionRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task UpdateAsync(
        MerchantSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        _context.MerchantSubscriptionRequests.Update(request);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MerchantSubscriptionRequests
            .CountAsync(r => r.Status == SubscriptionRequestStatus.Pending, cancellationToken);
    }
}
