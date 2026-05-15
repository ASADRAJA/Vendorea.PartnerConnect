using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Service implementation for trading partner management.
/// </summary>
public class TradingPartnerService : ITradingPartnerService
{
    private readonly ITradingPartnerRepository _repository;
    private readonly ILogger<TradingPartnerService> _logger;

    public TradingPartnerService(
        ITradingPartnerRepository repository,
        ILogger<TradingPartnerService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<TradingPartnerDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken);
        return partner is null ? null : MapToDto(partner);
    }

    public async Task<TradingPartnerDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var partner = await _repository.GetByCodeAsync(code, cancellationToken);
        return partner is null ? null : MapToDto(partner);
    }

    public async Task<IReadOnlyList<TradingPartnerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var partners = await _repository.GetAllAsync(cancellationToken);
        return partners.Select(MapToDto).ToList();
    }

    public async Task<TradingPartnerDto> CreateAsync(CreateTradingPartnerCommand command, CancellationToken cancellationToken = default)
    {
        var partner = new TradingPartner
        {
            Code = command.Code,
            Name = command.Name,
            Description = command.Description,
            PartnerType = command.PartnerType,
            Status = TradingPartnerStatus.Pending,
            ContactEmail = command.ContactEmail,
            ContactPhone = command.ContactPhone,
            WebsiteUrl = command.WebsiteUrl,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.AddAsync(partner, cancellationToken);
        _logger.LogInformation("Created trading partner {Code} with ID {Id}", created.Code, created.Id);

        return MapToDto(created);
    }

    public async Task<TradingPartnerDto?> UpdateAsync(int id, UpdateTradingPartnerCommand command, CancellationToken cancellationToken = default)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken);
        if (partner == null) return null;

        if (!string.IsNullOrWhiteSpace(command.Name))
            partner.Name = command.Name;
        if (!string.IsNullOrWhiteSpace(command.Description))
            partner.Description = command.Description;
        if (!string.IsNullOrWhiteSpace(command.PartnerType) &&
            Enum.TryParse<TradingPartnerType>(command.PartnerType, true, out var partnerType))
            partner.PartnerType = partnerType;
        if (!string.IsNullOrWhiteSpace(command.ContactEmail))
            partner.ContactEmail = command.ContactEmail;
        if (!string.IsNullOrWhiteSpace(command.ContactPhone))
            partner.ContactPhone = command.ContactPhone;
        if (!string.IsNullOrWhiteSpace(command.WebsiteUrl))
            partner.WebsiteUrl = command.WebsiteUrl;

        partner.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(partner, cancellationToken);
        _logger.LogInformation("Updated trading partner {Id}", id);

        return MapToDto(partner);
    }

    public async Task UpdateStatusAsync(int id, TradingPartnerStatus status, CancellationToken cancellationToken = default)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Trading partner {id} not found");

        partner.Status = status;
        partner.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(partner, cancellationToken);
        _logger.LogInformation("Updated trading partner {Id} status to {Status}", id, status);
    }

    public async Task<bool> ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken);
        if (partner == null) return false;

        partner.Status = TradingPartnerStatus.Active;
        partner.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(partner, cancellationToken);
        _logger.LogInformation("Activated trading partner {Id}", id);
        return true;
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken);
        if (partner == null) return false;

        partner.Status = TradingPartnerStatus.Inactive;
        partner.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(partner, cancellationToken);
        _logger.LogInformation("Deactivated trading partner {Id}", id);
        return true;
    }

    public async Task<PartnerCapabilityDetailDto?> AddCapabilityAsync(int partnerId, AddCapabilityRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await _repository.GetByIdAsync(partnerId, cancellationToken);
        if (partner == null) return null;

        if (!Enum.TryParse<PartnerCapability>(request.Capability, true, out var capability))
        {
            throw new ArgumentException($"Invalid capability: {request.Capability}");
        }

        var config = new PartnerCapabilityConfiguration
        {
            TradingPartnerId = partnerId,
            Capability = capability,
            IsEnabled = request.IsEnabled,
            AdapterType = request.AdapterType,
            ProtocolType = request.ProtocolType,
            FileFormat = request.FileFormat,
            PollingIntervalMinutes = request.PollingIntervalMinutes,
            ConfigurationJson = request.ConfigurationJson,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddCapabilityAsync(config, cancellationToken);
        _logger.LogInformation("Added capability {Capability} to partner {PartnerId}", capability, partnerId);

        return new PartnerCapabilityDetailDto
        {
            Id = config.Id,
            TradingPartnerId = partnerId,
            Capability = capability.ToString(),
            IsEnabled = config.IsEnabled,
            AdapterType = config.AdapterType,
            ProtocolType = config.ProtocolType,
            FileFormat = config.FileFormat,
            PollingIntervalMinutes = config.PollingIntervalMinutes,
            CreatedAt = config.CreatedAt
        };
    }

    public async Task<IReadOnlyList<PartnerCapabilityDetailDto>?> GetCapabilitiesAsync(int partnerId, CancellationToken cancellationToken = default)
    {
        var partner = await _repository.GetByIdAsync(partnerId, cancellationToken);
        if (partner == null) return null;

        return partner.Capabilities.Select(c => new PartnerCapabilityDetailDto
        {
            Id = c.Id,
            TradingPartnerId = c.TradingPartnerId,
            Capability = c.Capability.ToString(),
            IsEnabled = c.IsEnabled,
            AdapterType = c.AdapterType,
            ProtocolType = c.ProtocolType,
            FileFormat = c.FileFormat,
            PollingIntervalMinutes = c.PollingIntervalMinutes,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    private static TradingPartnerDto MapToDto(TradingPartner partner)
    {
        return new TradingPartnerDto(
            partner.Id,
            partner.Code,
            partner.Name,
            partner.Description,
            partner.PartnerType,
            partner.Status,
            partner.ContactEmail,
            partner.WebsiteUrl,
            partner.CreatedAt,
            partner.Capabilities.Select(c => new PartnerCapabilityDto(
                c.Capability,
                c.IsEnabled,
                c.ProtocolType,
                c.FileFormat)).ToList());
    }
}
