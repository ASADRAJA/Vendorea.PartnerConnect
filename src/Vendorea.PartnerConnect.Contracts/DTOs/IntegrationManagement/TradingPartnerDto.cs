using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Contracts.DTOs.IntegrationManagement;

/// <summary>
/// DTO for trading partner information.
/// </summary>
public record TradingPartnerDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    TradingPartnerType PartnerType,
    TradingPartnerStatus Status,
    string? ContactEmail,
    string? WebsiteUrl,
    DateTime CreatedAt,
    IReadOnlyList<PartnerCapabilityDto>? Capabilities);

public record PartnerCapabilityDto(
    PartnerCapability Capability,
    bool IsEnabled,
    string? ProtocolType,
    string? FileFormat);

/// <summary>
/// Command to create a new trading partner.
/// </summary>
public record CreateTradingPartnerCommand(
    string Code,
    string Name,
    string? Description,
    TradingPartnerType PartnerType,
    string? ContactEmail,
    string? ContactPhone,
    string? WebsiteUrl);

/// <summary>
/// Command to create a dealer-partner connection.
/// </summary>
public record CreateDealerConnectionCommand(
    int DealerId,
    int TradingPartnerId,
    string? ExternalAccountId,
    string? CredentialsJson,
    string? ConfigurationJson);
