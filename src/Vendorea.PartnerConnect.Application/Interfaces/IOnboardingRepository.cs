using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository for onboarding request operations.
/// </summary>
public interface IOnboardingRepository
{
    Task AddAsync(DealerOnboardingRequest request, CancellationToken cancellationToken = default);
    Task<DealerOnboardingRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DealerOnboardingRequest>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DealerOnboardingRequest>> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task UpdateAsync(DealerOnboardingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for external dealer operations.
/// </summary>
public interface IExternalDealerRepository
{
    Task AddAsync(ExternalDealer dealer, CancellationToken cancellationToken = default);
    Task<ExternalDealer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ExternalDealer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExternalDealer>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(ExternalDealer dealer, CancellationToken cancellationToken = default);
}
