using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class TenantPartnerAccountRepository : ITenantPartnerAccountRepository
{
    private readonly PartnerConnectDbContext _context;

    public TenantPartnerAccountRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<TenantPartnerAccount?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.TenantPartnerAccounts
            .Include(a => a.Tenant)
            .Include(a => a.TradingPartner)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<TenantPartnerAccount?> GetByTenantPartnerAccountAsync(
        int tenantId,
        int tradingPartnerId,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.TenantPartnerAccounts
            .Include(a => a.Tenant)
            .Include(a => a.TradingPartner)
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId &&
                     a.TradingPartnerId == tradingPartnerId &&
                     a.AccountNumber == accountNumber &&
                     a.IsActive,
                cancellationToken);
    }

    public async Task<IReadOnlyList<TenantPartnerAccount>> GetByTenantIdAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.TenantPartnerAccounts
            .Include(a => a.TradingPartner)
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.TradingPartner!.Name)
            .ThenBy(a => a.AccountNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TenantPartnerAccount>> GetByTradingPartnerIdAsync(int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        return await _context.TenantPartnerAccounts
            .Include(a => a.Tenant)
            .Where(a => a.TradingPartnerId == tradingPartnerId)
            .OrderBy(a => a.Tenant!.Name)
            .ThenBy(a => a.AccountNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantPartnerAccount?> GetDefaultAccountAsync(int tenantId, int tradingPartnerId, CancellationToken cancellationToken = default)
    {
        return await _context.TenantPartnerAccounts
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId &&
                     a.TradingPartnerId == tradingPartnerId &&
                     a.IsDefault &&
                     a.IsActive,
                cancellationToken);
    }

    public async Task<TenantPartnerAccount> AddAsync(TenantPartnerAccount account, CancellationToken cancellationToken = default)
    {
        _context.TenantPartnerAccounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task UpdateAsync(TenantPartnerAccount account, CancellationToken cancellationToken = default)
    {
        account.UpdatedAt = DateTime.UtcNow;
        _context.TenantPartnerAccounts.Update(account);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(int tenantId, int tradingPartnerId, string accountNumber, CancellationToken cancellationToken = default)
    {
        return await _context.TenantPartnerAccounts
            .AnyAsync(
                a => a.TenantId == tenantId &&
                     a.TradingPartnerId == tradingPartnerId &&
                     a.AccountNumber == accountNumber,
                cancellationToken);
    }

    public async Task<TenantPartnerAccount?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.TenantPartnerAccounts
            .Include(a => a.Tenant)
            .Include(a => a.TradingPartner)
                .ThenInclude(tp => tp!.Capabilities)
            .Include(a => a.Organization)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TenantPartnerAccount>> GetConnectionsAsync(
        int? organizationId = null,
        ConnectionApprovalStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TenantPartnerAccounts
            .Include(a => a.Tenant)
            .Include(a => a.TradingPartner)
            .Include(a => a.Organization)
            .Where(a => a.OrganizationId != null); // connection-workflow rows only

        if (organizationId.HasValue)
            query = query.Where(a => a.OrganizationId == organizationId.Value);
        if (status.HasValue)
            query = query.Where(a => a.ApprovalStatus == status.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ConnectionExistsAsync(
        int organizationId,
        string externalTenantId,
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TenantPartnerAccounts
            .AnyAsync(
                a => a.OrganizationId == organizationId &&
                     a.ExternalTenantId == externalTenantId &&
                     a.TradingPartnerId == tradingPartnerId,
                cancellationToken);
    }
}
