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
    Task<TradingPartnerDto?> UpdateAsync(int id, UpdateTradingPartnerCommand command, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int id, TradingPartnerStatus status, CancellationToken cancellationToken = default);
    Task<bool> ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<PartnerCapabilityDetailDto?> AddCapabilityAsync(int partnerId, AddCapabilityRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartnerCapabilityDetailDto>?> GetCapabilitiesAsync(int partnerId, CancellationToken cancellationToken = default);
}

public class AddCapabilityRequest
{
    public string Capability { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? AdapterType { get; set; }
    public string? ProtocolType { get; set; }
    public string? FileFormat { get; set; }
    public int? PollingIntervalMinutes { get; set; }
    public string? ConfigurationJson { get; set; }
}

public class PartnerCapabilityDetailDto
{
    public int Id { get; set; }
    public int TradingPartnerId { get; set; }
    public string Capability { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? AdapterType { get; set; }
    public string? ProtocolType { get; set; }
    public string? FileFormat { get; set; }
    public int? PollingIntervalMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateTradingPartnerCommand
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? PartnerType { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? WebsiteUrl { get; set; }
}
