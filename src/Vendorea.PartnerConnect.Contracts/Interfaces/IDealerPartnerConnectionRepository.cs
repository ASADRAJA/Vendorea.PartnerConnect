using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Contracts.Interfaces;

/// <summary>
/// Repository interface for dealer-partner connection operations.
/// </summary>
public interface IDealerPartnerConnectionRepository
{
    Task<DealerPartnerConnection?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DealerPartnerConnection?> GetByDealerAndPartnerAsync(int dealerId, int tradingPartnerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DealerPartnerConnection>> GetByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DealerPartnerConnection>> GetByPartnerIdAsync(int tradingPartnerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DealerPartnerConnection>> GetActiveConnectionsAsync(CancellationToken cancellationToken = default);
    Task<DealerPartnerConnection> AddAsync(DealerPartnerConnection connection, CancellationToken cancellationToken = default);
    Task UpdateAsync(DealerPartnerConnection connection, CancellationToken cancellationToken = default);
}
