using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;
using Vendorea.PartnerConnect.Contracts.Interfaces;
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

    public async Task UpdateStatusAsync(int id, TradingPartnerStatus status, CancellationToken cancellationToken = default)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Trading partner {id} not found");

        partner.Status = status;
        partner.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(partner, cancellationToken);
        _logger.LogInformation("Updated trading partner {Id} status to {Status}", id, status);
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
