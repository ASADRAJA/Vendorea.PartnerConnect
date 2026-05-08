using Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Service interface for trading partner management operations.
/// </summary>
public interface ITradingPartnerService
{
    Task<TradingPartnerDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TradingPartnerDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradingPartnerDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TradingPartnerDto> CreateAsync(CreateTradingPartnerCommand command, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int id, TradingPartnerStatus status, CancellationToken cancellationToken = default);
}
