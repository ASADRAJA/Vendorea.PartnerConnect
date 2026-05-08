using Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service interface for dealer-partner connection management.
/// </summary>
public interface IDealerConnectionService
{
    Task<DealerPartnerConnection?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DealerPartnerConnection>> GetByDealerIdAsync(int dealerId, CancellationToken cancellationToken = default);
    Task<DealerPartnerConnection> CreateConnectionAsync(CreateDealerConnectionCommand command, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(int connectionId, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int connectionId, ConnectionStatus status, CancellationToken cancellationToken = default);
    Task UpdateLastSyncAsync(int connectionId, CancellationToken cancellationToken = default);
}
