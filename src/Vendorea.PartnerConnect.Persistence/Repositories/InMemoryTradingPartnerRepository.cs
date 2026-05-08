using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

/// <summary>
/// In-memory implementation of ITradingPartnerRepository for development/testing.
/// Replace with actual database implementation in production.
/// </summary>
public class InMemoryTradingPartnerRepository : ITradingPartnerRepository
{
    private readonly List<TradingPartner> _partners = new();
    private int _nextId = 1;

    public Task<TradingPartner?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var partner = _partners.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(partner);
    }

    public Task<TradingPartner?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var partner = _partners.FirstOrDefault(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(partner);
    }

    public Task<IReadOnlyList<TradingPartner>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TradingPartner>>(_partners.ToList());
    }

    public Task<IReadOnlyList<TradingPartner>> GetByStatusAsync(TradingPartnerStatus status, CancellationToken cancellationToken = default)
    {
        var partners = _partners.Where(p => p.Status == status).ToList();
        return Task.FromResult<IReadOnlyList<TradingPartner>>(partners);
    }

    public Task<TradingPartner> AddAsync(TradingPartner partner, CancellationToken cancellationToken = default)
    {
        partner.Id = _nextId++;
        _partners.Add(partner);
        return Task.FromResult(partner);
    }

    public Task UpdateAsync(TradingPartner partner, CancellationToken cancellationToken = default)
    {
        var index = _partners.FindIndex(p => p.Id == partner.Id);
        if (index >= 0)
        {
            _partners[index] = partner;
        }
        return Task.CompletedTask;
    }
}
