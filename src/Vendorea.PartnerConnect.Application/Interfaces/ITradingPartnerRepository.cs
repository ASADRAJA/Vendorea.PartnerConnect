using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Repository interface for trading partner operations.
/// </summary>
public interface ITradingPartnerRepository
{
    Task<TradingPartner?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TradingPartner?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradingPartner>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradingPartner>> GetByStatusAsync(TradingPartnerStatus status, CancellationToken cancellationToken = default);
    Task<TradingPartner> AddAsync(TradingPartner partner, CancellationToken cancellationToken = default);
    Task UpdateAsync(TradingPartner partner, CancellationToken cancellationToken = default);
}
