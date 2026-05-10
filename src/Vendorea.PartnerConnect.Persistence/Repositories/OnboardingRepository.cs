using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// Repository for onboarding request operations.
/// </summary>
public class OnboardingRepository : IOnboardingRepository
{
    private readonly PartnerConnectDbContext _context;

    public OnboardingRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DealerOnboardingRequest request, CancellationToken cancellationToken = default)
    {
        await _context.DealerOnboardingRequests.AddAsync(request, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DealerOnboardingRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DealerOnboardingRequests
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<DealerOnboardingRequest>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DealerOnboardingRequests
            .Where(r => r.Status == OnboardingStatus.Submitted || r.Status == OnboardingStatus.UnderReview)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DealerOnboardingRequest>> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.DealerOnboardingRequests
            .Where(r => r.Email == email)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(DealerOnboardingRequest request, CancellationToken cancellationToken = default)
    {
        _context.DealerOnboardingRequests.Update(request);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Repository for external dealer operations.
/// </summary>
public class ExternalDealerRepository : IExternalDealerRepository
{
    private readonly PartnerConnectDbContext _context;

    public ExternalDealerRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ExternalDealer dealer, CancellationToken cancellationToken = default)
    {
        await _context.ExternalDealers.AddAsync(dealer, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExternalDealer?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.ExternalDealers
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<ExternalDealer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.ExternalDealers
            .FirstOrDefaultAsync(d => d.Email == email, cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalDealer>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExternalDealers
            .Where(d => d.Status == ExternalDealerStatus.Active)
            .OrderBy(d => d.CompanyName)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(ExternalDealer dealer, CancellationToken cancellationToken = default)
    {
        _context.ExternalDealers.Update(dealer);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
