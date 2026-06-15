using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly PartnerConnectDbContext _context;

    public OrganizationRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<Organization?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Organization?> GetByIdWithPartnersAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .Include(o => o.Partners)
                .ThenInclude(p => p.TradingPartner)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Organization?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.Code == code, cancellationToken);
    }

    public async Task<Organization?> GetByPortalApiKeyHashAsync(string portalApiKeyHash, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.PortalApiKeyHash == portalApiKeyHash, cancellationToken);
    }

    public async Task ReplacePartnersAsync(
        int organizationId,
        IReadOnlyCollection<int> tradingPartnerIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.OrganizationPartners
            .Where(p => p.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        _context.OrganizationPartners.RemoveRange(
            existing.Where(p => !tradingPartnerIds.Contains(p.TradingPartnerId)));

        var existingIds = existing.Select(p => p.TradingPartnerId).ToHashSet();
        foreach (var partnerId in tradingPartnerIds.Distinct().Where(id => !existingIds.Contains(id)))
        {
            _context.OrganizationPartners.Add(new OrganizationPartner
            {
                OrganizationId = organizationId,
                TradingPartnerId = partnerId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GenerateNextCodeAsync(CancellationToken cancellationToken = default)
    {
        // Sequential ORG-#####, derived from the highest existing ORG- code.
        var codes = await _context.Organizations
            .Where(o => o.Code.StartsWith("ORG-"))
            .Select(o => o.Code)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var code in codes)
        {
            if (int.TryParse(code.AsSpan(4), out var n) && n > max)
            {
                max = n;
            }
        }

        return $"ORG-{(max + 1):D5}";
    }

    public async Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Organization>> GetByStatusAsync(OrganizationStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .Where(o => o.Status == status)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Organization> AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync(cancellationToken);
        return organization;
    }

    public async Task UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        organization.UpdatedAt = DateTime.UtcNow;
        _context.Organizations.Update(organization);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .AnyAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .AnyAsync(o => o.Code == code, cancellationToken);
    }
}
